using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace MelvorTown;

internal sealed class TownView : Control
{
    sealed record Spot(string Key, string Art, float X, float Y, bool Clickable = true);

    public sealed class Placement
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Scale { get; set; } = 1f;
    }

    readonly Dictionary<string, Texture2D?> _art;
    readonly Dictionary<string, TextureRect> _nodes = new();
    readonly Dictionary<string, Placement> _placements = new();
    readonly TextureRect _ground;
    readonly string _layoutPath;
    TownState? _state;

    TextureRect? _dragNode;
    Vector2 _dragOffset;
    bool _dirty;

    public Action<string>? Clicked;
    public bool Editing { get; private set; }

    public TownView(Dictionary<string, Texture2D?> art, Texture2D? ground, string directory)
    {
        _art = art;
        _layoutPath = Path.Combine(directory, "town-layout.json");
        LoadLayout();

        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        SetProcessInput(true);

        _ground = new TextureRect
        {
            Texture = ground,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_ground);
    }

    public void SetGroundVisible(bool visible) => _ground.Visible = visible;

    public void SetState(TownState state)
    {
        _state = state;
        Rebuild();
    }

    public void SetEditing(bool editing)
    {
        MelvorModLoader.Log.Write("[MelvorTown] editing " + editing);
        Editing = editing;
        MouseFilter = editing ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        foreach (TextureRect node in _nodes.Values)
            if (GodotObject.IsInstanceValid(node)) node.MouseFilter = editing ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

        if (!editing) SaveLayout();
    }

    public void Rebuild()
    {
        if (_state == null) return;

        List<Spot> spots = BuildSpots(_state);
        var alive = new HashSet<string>();

        foreach (Spot spot in spots)
        {
            alive.Add(spot.Key);
            if (!_nodes.TryGetValue(spot.Key, out TextureRect? node) || !GodotObject.IsInstanceValid(node))
            {
                node = new TextureRect
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Editing ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop,
                    MouseDefaultCursorShape = Editing ? CursorShape.Drag : CursorShape.PointingHand,
                };
                string key = spot.Key;
                if (spot.Clickable)
                {
                    node.GuiInput += ev =>
                    {
                        if (Editing) return;
                        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                            Clicked?.Invoke(key);
                    };
                    node.MouseEntered += () => { if (!Editing) node.Modulate = new Color(1.12f, 1.12f, 1.12f); };
                    node.MouseExited += () => node.Modulate = Colors.White;
                }
                else
                {
                    node.MouseFilter = MouseFilterEnum.Ignore;
                }
                AddChild(node);
                _nodes[spot.Key] = node;
            }

            node.Texture = _art.GetValueOrDefault(spot.Art);
            node.SetMeta("x", spot.X);
            node.SetMeta("y", spot.Y);
        }

        foreach (var pair in _nodes.ToList())
        {
            if (alive.Contains(pair.Key)) continue;
            pair.Value.QueueFree();
            _nodes.Remove(pair.Key);
        }

        Layout();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) Layout();
    }

    public override void _Input(InputEvent ev)
    {
        if (!IsInsideTree()) return;

        if (ev is InputEventKey { Pressed: true, Echo: false } key && key.Keycode == Key.F8)
        {
            SetEditing(!Editing);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!Editing) return;

        Vector2 mouse = GetLocalMousePosition();
        if (!new Rect2(Vector2.Zero, Size).HasPoint(mouse)) return;

        if (ev is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    TextureRect? node = NodeAt(mouse);
                    if (node != null)
                    {
                        _dragNode = node;
                        _dragOffset = mouse - node.Position;
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (_dragNode != null)
                {
                    _dragNode = null;
                    SaveLayout();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (button.Pressed && (button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown))
            {
                TextureRect? node = NodeAt(mouse);
                if (node != null && KeyOf(node) is { } key)
                {
                    Placement placement = PlacementFor(key);
                    float factor = button.ButtonIndex == MouseButton.WheelUp ? 1.08f : 0.92f;
                    placement.Scale = Math.Clamp(placement.Scale * factor, 0.35f, 3f);
                    Apply(node, key);
                    _dirty = true;
                    SaveLayout();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
        else if (ev is InputEventMouseMotion && _dragNode != null)
        {
            _dragNode.Position = mouse - _dragOffset;
            Clamp(_dragNode);
            Remember(_dragNode);
            _dirty = true;
            GetViewport().SetInputAsHandled();
        }
    }

    void Layout()
    {
        Vector2 size = Size;
        if (size.X < 10 || size.Y < 10) return;

        _ground.Position = Vector2.Zero;
        _ground.Size = size;

        foreach (var pair in _nodes)
        {
            if (!GodotObject.IsInstanceValid(pair.Value)) continue;
            if (pair.Value == _dragNode) continue;
            Apply(pair.Value, pair.Key);
        }
    }

    void Apply(TextureRect node, string key)
    {
        Vector2 size = Size;
        float baseSide = Math.Min(150f, size.Y * 0.36f);
        float autoScale = Math.Clamp(1f - 0.05f * (_nodes.Count - 4), 0.55f, 1f);

        float px, py, scale;
        if (_placements.TryGetValue(key, out Placement? p))
        {
            px = p.X;
            py = p.Y;
            scale = p.Scale;
        }
        else
        {
            px = (float)node.GetMeta("x", 0.5f);
            py = (float)node.GetMeta("y", 0.7f);
            scale = autoScale;
        }

        float side = baseSide * scale;
        node.Size = new Vector2(side, side);
        node.Position = new Vector2(px * size.X - side / 2f, py * size.Y - side);
    }

    void Remember(TextureRect node)
    {
        if (KeyOf(node) is not { } key) return;
        Vector2 size = Size;
        if (size.X < 10 || size.Y < 10) return;

        Placement p = PlacementFor(key);
        p.X = (node.Position.X + node.Size.X / 2f) / size.X;
        p.Y = (node.Position.Y + node.Size.Y) / size.Y;
        _placements[key] = p;
    }

    void Clamp(TextureRect node)
    {
        float x = Math.Clamp(node.Position.X, -node.Size.X * 0.25f, Size.X - node.Size.X * 0.75f);
        float y = Math.Clamp(node.Position.Y, -node.Size.Y * 0.1f, Size.Y - node.Size.Y * 0.4f);
        node.Position = new Vector2(x, y);
    }

    TextureRect? NodeAt(Vector2 point)
    {
        TextureRect? found = null;
        foreach (TextureRect node in _nodes.Values)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            if (new Rect2(node.Position, node.Size).HasPoint(point)) found = node;
        }
        return found;
    }

    string? KeyOf(TextureRect node)
    {
        foreach (var pair in _nodes)
            if (pair.Value == node) return pair.Key;
        return null;
    }

    Placement PlacementFor(string key)
    {
        if (_placements.TryGetValue(key, out Placement? p)) return p;
        p = new Placement();
        _placements[key] = p;
        return p;
    }

    void LoadLayout()
    {
        try
        {
            if (!File.Exists(_layoutPath)) return;
            Dictionary<string, Placement>? loaded = JsonSerializer.Deserialize<Dictionary<string, Placement>>(File.ReadAllText(_layoutPath));
            if (loaded == null) return;
            foreach (var pair in loaded) _placements[pair.Key] = pair.Value;
        }
        catch { }
    }

    public void SaveLayout()
    {
        if (!_dirty) return;
        try
        {
            File.WriteAllText(_layoutPath, JsonSerializer.Serialize(_placements, new JsonSerializerOptions { WriteIndented = true }));
            _dirty = false;
        }
        catch { }
    }

    static List<Spot> BuildSpots(TownState state)
    {
        var spots = new List<Spot>
        {
            new("bar", "bar", 0.18f, 0.73f, false),
        };

        if (state.Has("cottage")) spots.Add(new("house1", "house1", 0.30f, 0.74f));
        if (state.Has("timber")) spots.Add(new("house2", "house2", 0.42f, 0.73f));
        if (state.Has("manor")) spots.Add(new("house3", "house3", 0.54f, 0.72f));

        if (state.HasSchool) spots.Add(new("school", "school", 0.47f, 0.47f));
        if (state.HasSeedStore) spots.Add(new("seedstore", "seedstore", 0.62f, 0.57f));

        spots.Add(new("farm1", "farm", 0.74f, 0.78f));
        if (state.Farms > 1) spots.Add(new("farm2", "farm", 0.83f, 0.73f));

        spots.Add(new("trees1", "trees", 0.86f, 0.47f));
        if (state.Groves > 1) spots.Add(new("trees2", "trees", 0.90f, 0.58f));

        if (state.HasBalloon) spots.Add(new("balloon", "balloon", 0.79f, 0.23f));
        if (state.MerchantHere || state.AlwaysMerchant) spots.Add(new("merchant", "merchant", 0.14f, 0.43f));

        return spots;
    }
}
