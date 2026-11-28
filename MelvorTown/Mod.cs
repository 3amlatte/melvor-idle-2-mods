using Godot;
using MelvorModLoader;

namespace MelvorTown;

public sealed class Mod : IMod
{
    ModContext _ctx = null!;
    TownState _state = null!;
    TownPage _page = null!;
    Texture2D? _tabIcon;
    Button? _button;
    long _lastMs;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _state = TownState.Load(ctx.Directory);
        _tabIcon = Ui.Texture(ctx.Directory, "tab");
        _page = new TownPage(ctx, _state);
        ctx.Tree.Root.AddChild(_page);

        ctx.AddWindow("Melvor Town", Toggle);
        ctx.Frame += OnFrame;
        _lastMs = (long)Time.GetTicksMsec();
        ctx.Log("Loaded.");
    }

    void OnFrame()
    {
        long now = (long)Time.GetTicksMsec();
        double dt = (now - _lastMs) / 1000.0;
        _lastMs = now;
        _state.Tick(dt);

        if (++_frame % 30 == 0) PlaceButton();
    }

    void Toggle()
    {
        _page.Toggle();
    }

    void PlaceButton()
    {
        if (_button != null && GodotObject.IsInstanceValid(_button) && _button.IsInsideTree())
            return;

        Node root = _ctx.Tree.Root;
        Control? bank = root.FindChild("BankNavigationButton", true, false) as Control;
        if (bank?.GetParent() is not BoxContainer box)
            return;

        // Reuse the header label style so the tab matches its neighbours.
        Label? title = bank.FindChild("Title", true, false) as Label;
        LabelSettings? settings = title?.LabelSettings;

        var button = new Button
        {
            Name = "TownNavigationButton",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(56, 36),
            SizeFlagsVertical = bank.SizeFlagsVertical,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = "Melvor Town\nGrow a small medieval town with population and food.",
        };
        var hover = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.07f) };
        hover.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        column.AddThemeConstantOverride("separation", 0);
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var icon = new TextureRect
        {
            Texture = _tabIcon,
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        column.AddChild(icon);

        var label = new Label
        {
            Text = "Town",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (settings != null) label.LabelSettings = settings;
        column.AddChild(label);

        button.AddChild(column);
        button.Pressed += Toggle;

        box.AddChild(button);
        box.MoveChild(button, bank.GetIndex() + 1);
        _button = button;
    }
}
