using GamesByMalcs.Melvor2.GameMenus;
using Godot;
using MelvorModLoader;

namespace MelvorTown;

public sealed class Mod : IMod
{
    ModContext _ctx = null!;
    TownState _state = null!;
    TownPanel _panel = null!;
    TownGamePage _townPage = null!;
    Texture2D? _tabIcon;
    Button? _button;
    bool _tabActive;
    long _lastMs;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _state = TownState.Load(ctx.Directory);
        _tabIcon = Ui.Texture(ctx.Directory, "tab");
        _panel = new TownPanel(ctx, _state);
        _townPage = new TownGamePage(_panel, _tabIcon);

        ctx.AddWindow("Melvor Town", OpenTownPage);
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

        if (++_frame % 30 == 0)
        {
            PlaceButton();
            UpdateTabState();
        }
    }

    void OpenTownPage()
    {
        GameView? view = GameAccess.FindGameView();
        if (view == null) return;
        view.SetCurrentPage(_townPage);
    }

    void PlaceButton()
    {
        Node root = _ctx.Tree.Root;
        Control? bank = root.FindChild("BankNavigationButton", true, false) as Control;
        if (bank?.GetParent() is not BoxContainer box)
            return;

        if (_button != null && GodotObject.IsInstanceValid(_button) && _button.IsInsideTree())
            return;

        // Reuse the header label style so the tab matches its neighbours.
        Label? title = bank.FindChild("Title", true, false) as Label;
        LabelSettings? settings = title?.LabelSettings;

        // Match how the neighbouring tab label is actually drawn (settings + tint).
        Color tint = Colors.White;
        for (Node? n = title; n != null && n != bank; n = n.GetParent())
            if (n is CanvasItem item) tint *= item.Modulate * item.SelfModulate;
        Color labelColor = (settings?.FontColor ?? title?.GetThemeColor("font_color") ?? new Color(0.886f, 0.859f, 0.804f)) * tint;

        var button = new Button
        {
            Name = "TownNavigationButton",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(80, 64),
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
            CustomMinimumSize = new Vector2(40, 40),
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
        if (settings?.Font != null) label.AddThemeFontOverride("font", settings.Font);
        if (settings != null && settings.FontSize > 0)
            label.AddThemeFontSizeOverride("font_size", settings.FontSize);
        label.AddThemeColorOverride("font_color", labelColor);
        column.AddChild(label);

        button.AddChild(column);
        button.Pressed += OpenTownPage;

        box.AddChild(button);
        box.MoveChild(button, bank.GetIndex());
        _button = button;
        _tabActive = false;
    }

    void UpdateTabState()
    {
        bool active = _panel.GetParent() != null;
        if (active == _tabActive) return;
        _tabActive = active;

        if (_button == null || !GodotObject.IsInstanceValid(_button)) return;

        if (active)
        {
            var style = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.09f) };
            style.SetCornerRadiusAll(4);
            _button.AddThemeStyleboxOverride("normal", style);
        }
        else
        {
            _button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        }
    }
}
