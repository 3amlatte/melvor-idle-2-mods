using Godot;

namespace MelvorTown;

internal sealed class TownView : Control
{
    sealed record Spot(string Key, string Art, float X, float Y, bool Clickable = true);

    readonly Dictionary<string, Texture2D?> _art;
    readonly Dictionary<string, TextureRect> _nodes = new();
    readonly TextureRect _ground;
    TownState? _state;

    public Action<string>? Clicked;

    public TownView(Dictionary<string, Texture2D?> art, Texture2D? ground)
    {
        _art = art;
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;

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

    public void Rebuild()
    {
        if (_state == null) return;

        List<Spot> spots = BuildSpots(_state);
        var alive = new HashSet<string>();

        float scale = Math.Clamp(1f - 0.05f * (spots.Count - 4), 0.55f, 1f);
        foreach (Spot spot in spots)
        {
            alive.Add(spot.Key);
            if (!_nodes.TryGetValue(spot.Key, out TextureRect? node) || !GodotObject.IsInstanceValid(node))
            {
                node = new TextureRect
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = spot.Clickable ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore,
                    MouseDefaultCursorShape = spot.Clickable ? CursorShape.PointingHand : CursorShape.Arrow,
                };
                string key = spot.Key;
                if (spot.Clickable)
                {
                    node.GuiInput += ev =>
                    {
                        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                            Clicked?.Invoke(key);
                    };
                    node.MouseEntered += () => node.Modulate = new Color(1.12f, 1.12f, 1.12f);
                    node.MouseExited += () => node.Modulate = Colors.White;
                }
                AddChild(node);
                _nodes[spot.Key] = node;
            }

            node.Texture = _art.GetValueOrDefault(spot.Art);
            node.SetMeta("x", spot.X);
            node.SetMeta("y", spot.Y);
            node.SetMeta("scale", scale);
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

    void Layout()
    {
        Vector2 size = Size;
        if (size.X < 10 || size.Y < 10) return;

        _ground.Position = Vector2.Zero;
        _ground.Size = size;

        float townScale = Math.Clamp(1f - 0.05f * (_nodes.Count - 4), 0.55f, 1f);
        float side = Math.Min(150f, size.Y * 0.36f) * townScale;

        foreach (TextureRect node in _nodes.Values)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            float x = (float)node.GetMeta("x", 0.5f);
            float y = (float)node.GetMeta("y", 0.7f);
            node.Size = new Vector2(side, side);
            node.Position = new Vector2(x * size.X - side / 2f, y * size.Y - side);
        }
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
        if (state.MerchantHere) spots.Add(new("merchant", "merchant", 0.14f, 0.43f));

        return spots;
    }
}
