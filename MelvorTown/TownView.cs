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
    readonly Control _editOverlay;
    readonly string _layoutPath;
    TownState? _state;

    TextureRect? _dragNode;
    TextureRect? _resizeNode;
    Vector2 _dragOffset;
    bool _dirty;
    string? _hoverName;

    public Action<string>? Clicked;
    public event Action<string?>? Hovered;
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

        _editOverlay = new Control
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            MouseDefaultCursorShape = CursorShape.Drag,
        };
        _editOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _editOverlay.GuiInput += OnEditInput;
        _editOverlay.Draw += DrawHandles;
        _editOverlay.MouseExited += () =>
        {
            _hoverName = null;
            Hovered?.Invoke(null);
        };
        AddChild(_editOverlay);
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
        foreach (TextureRect node in _nodes.Values)
            if (GodotObject.IsInstanceValid(node)) node.MouseFilter = editing ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

        _editOverlay.Visible = editing;
        _editOverlay.MouseFilter = editing ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (editing) MoveChild(_editOverlay, GetChildCount() - 1);
        _editOverlay.QueueRedraw();

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
            node.TooltipText = NameOf(spot.Key);
            node.SetMeta("x", spot.X);
            node.SetMeta("y", spot.Y);
        }

        foreach (var pair in _nodes.ToList())
        {
            if (alive.Contains(pair.Key)) continue;
            pair.Value.QueueFree();
            _nodes.Remove(pair.Key);
        }

        if (Editing) MoveChild(_editOverlay, GetChildCount() - 1);
        _editOverlay.QueueRedraw();
        Layout();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) Layout();
    }

    public override void _Input(InputEvent ev)
    {
        if (!IsInsideTree()) return;
        if (ev is InputEventKey { Pressed: true, Echo: false } f8 && f8.Keycode == Key.F8)
        {
            SetEditing(!Editing);
            GetViewport().SetInputAsHandled();
        }
    }

    void OnEditInput(InputEvent ev)
    {
        if (!Editing) return;
        Vector2 mouse = _editOverlay.GetLocalMousePosition();

        if (ev is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    TextureRect? handle = HandleAt(mouse);
                    if (handle != null)
                    {
                        _resizeNode = handle;
                        MelvorModLoader.Log.Write($"[MelvorTown] resize start {KeyOf(handle)}");
                    }
                    else
                    {
                        _dragNode = NodeAt(mouse);
                        if (_dragNode != null) _dragOffset = mouse - _dragNode.Position;
                    }
                }
                else
                {
                    if (_dragNode != null || _resizeNode != null) SaveLayout();
                    _dragNode = null;
                    _resizeNode = null;
                }
            }
            else if (button.Pressed && (button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown))
            {
                TextureRect? node = NodeAt(mouse);
                if (node != null && KeyOf(node) is { } nodeKey)
                {
                    Placement placement = PlacementFor(nodeKey);
                    float factor = button.ButtonIndex == MouseButton.WheelUp ? 1.08f : 0.92f;
                    placement.Scale = Math.Clamp(placement.Scale * factor, 0.35f, 3f);
                    Apply(node, nodeKey);
                    Remember(node);
                    _dirty = true;
                    _editOverlay.QueueRedraw();
                    SaveLayout();
                }
            }
        }
        else if (ev is InputEventMouseMotion motion)
        {
            if (_resizeNode != null && KeyOf(_resizeNode) is { } resizeKey)
            {
                float baseSide = Math.Min(150f, Size.Y * 0.36f);
                Vector2 anchor = _resizeNode.Position + new Vector2(_resizeNode.Size.X / 2f, _resizeNode.Size.Y);
                float side = Math.Max(Math.Abs(mouse.X - anchor.X) * 2f, Math.Abs(mouse.Y - anchor.Y) * 1.5f);
                side = Math.Clamp(side, 48f, 700f);

                Placement placement = PlacementFor(resizeKey);
                placement.Scale = side / baseSide;
                Apply(_resizeNode, resizeKey);
                Remember(_resizeNode);
                _dirty = true;
                _editOverlay.QueueRedraw();
            }
            else if (_dragNode != null)
            {
                _dragNode.Position = mouse - _dragOffset;
                Clamp(_dragNode);
                Remember(_dragNode);
                _dirty = true;
                _editOverlay.QueueRedraw();
            }
            else
            {
                string? name = NodeAt(mouse) is { } hovered && KeyOf(hovered) is { } hoverKey ? NameOf(hoverKey) : null;
                if (name != _hoverName)
                {
                    _hoverName = name;
                    Hovered?.Invoke(name);
                }
            }
        }
    }

    TextureRect? HandleAt(Vector2 point)
    {
        foreach (TextureRect node in _nodes.Values)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            var handle = new Rect2(node.Position + node.Size - new Vector2(16, 16), new Vector2(16, 16));
            if (handle.HasPoint(point)) return node;
        }
        return null;
    }

    void DrawHandles()
    {
        if (!Editing) return;
        var gold = new Color(0.93f, 0.75f, 0.35f);
        var dark = new Color(0.10f, 0.09f, 0.07f);
        foreach (TextureRect node in _nodes.Values)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            var rect = new Rect2(node.Position + node.Size - new Vector2(14, 14), new Vector2(14, 14));
            _editOverlay.DrawRect(rect, gold, true);
            _editOverlay.DrawRect(rect, dark, false, 2f);
        }
    }

    public static string NameOf(string key) => key switch
    {
        "bar" => "Tavern",
        "house1" => "Cottage",
        "house2" => "Timber House",
        "house3" => "Manor",
        "school" => "School",
        "seedstore" => "Seed Store",
        "farm1" or "farm2" => "Farm",
        "trees1" or "trees2" => "Tree Grove",
        "balloon" => "Hot Air Balloon",
        "merchant" => "Travelling Merchant",
        _ => key,
    };

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
