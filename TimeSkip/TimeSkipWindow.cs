using System;
using Godot;
using MelvorModLoader;

namespace TimeSkip;

/// <summary>The TimeSkip window opened from Mod Viewer.</summary>
internal sealed class TimeSkipWindow
{
    static readonly (string Label, double Hours)[] Options =
    {
        ("1 hour", 1), ("2 hours", 2), ("4 hours", 4),
        ("6 hours", 6), ("12 hours", 12), ("24 hours", 24),
        ("2 days", 48), ("4 days", 96), ("7 days", 168)
    };

    readonly CanvasLayer _layer;
    readonly PanelContainer _panel;
    readonly StyleBoxFlat _panelStyle;
    readonly Label _title;
    readonly Label _hint;
    readonly Label _selected;
    readonly Button _skip;
    readonly Label _status;
    double _hours = 24;

    public TimeSkipWindow(ModContext ctx, Action<double> onSkip)
    {
        TimeSkipTheme.RefreshIfChanged();
        _layer = new CanvasLayer { Layer = 110, Visible = false };

        // Bottom-right corner, with a 20 px gap. The containers push the panel into place
        // without hard-coding offsets, so it adapts to any resolution.
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        _layer.AddChild(margin);

        var bottom = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddChild(bottom);

        var right = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bottom.AddChild(right);

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(448, 0) };
        _panelStyle = new StyleBoxFlat
        {
            BgColor = TimeSkipTheme.Panel,
            BorderColor = TimeSkipTheme.Accent
        };
        _panelStyle.SetBorderWidthAll(1);
        _panelStyle.SetCornerRadiusAll(6);
        _panelStyle.SetContentMarginAll(14);
        _panel.AddThemeStyleboxOverride("panel", _panelStyle);
        right.AddChild(_panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(column);

        _title = new Label { Text = "TimeSkip" };
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", TimeSkipTheme.Accent);
        column.AddChild(_title);

        _hint = new Label
        {
            Text = "Skip forward by simulating the selected time. The game pauses, runs the offline calculation, then shows its summary.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hint.AddThemeFontSizeOverride("font_size", 10);
        _hint.AddThemeColorOverride("font_color", TimeSkipTheme.Muted);
        column.AddChild(_hint);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(grid);

        var group = new ButtonGroup();
        Button? defaultButton = null;
        foreach (var (label, hours) in Options)
        {
            var button = new Button
            {
                Text = label,
                ToggleMode = true,
                ButtonGroup = group,
                CustomMinimumSize = new Vector2(0, 34),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.AddThemeFontSizeOverride("font_size", 12);
            button.Toggled += pressed =>
            {
                if (pressed) Select(hours);
            };
            grid.AddChild(button);
            if (Math.Abs(hours - _hours) < 0.01) defaultButton = button;
        }

        _selected = new Label();
        _selected.AddThemeFontSizeOverride("font_size", 11);
        column.AddChild(_selected);

        _skip = new Button { Text = "Skip Forward", CustomMinimumSize = new Vector2(0, 37) };
        _skip.AddThemeFontSizeOverride("font_size", 14);
        _skip.Pressed += () => onSkip(_hours);
        column.AddChild(_skip);

        _status = new Label { Text = "Open a character first." };
        _status.AddThemeFontSizeOverride("font_size", 10);
        _status.AddThemeColorOverride("font_color", TimeSkipTheme.Muted);
        column.AddChild(_status);

        var close = new Button
        {
            Text = "Close",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
        };
        close.Pressed += Hide;
        column.AddChild(close);

        Select(_hours);
        if (defaultButton != null) defaultButton.ButtonPressed = true;
        ApplyTheme();

        ctx.Tree.Root.AddChild(_layer);
    }

    public bool Visible => _layer.Visible;

    public void Toggle()
    {
        RefreshTheme();
        _layer.Visible = !_layer.Visible;
    }

    public void RefreshTheme()
    {
        if (TimeSkipTheme.RefreshIfChanged()) ApplyTheme();
    }

    void ApplyTheme()
    {
        _panelStyle.BgColor = TimeSkipTheme.Panel;
        _panelStyle.BorderColor = TimeSkipTheme.Accent;
        _panel.AddThemeStyleboxOverride("panel", _panelStyle);
        _title.AddThemeColorOverride("font_color", TimeSkipTheme.Accent);
        _selected.AddThemeColorOverride("font_color", TimeSkipTheme.Accent);
        _hint.AddThemeColorOverride("font_color", TimeSkipTheme.Muted);
        _status.AddThemeColorOverride("font_color", TimeSkipTheme.Muted);
    }

    public void Hide()
    {
        _layer.Visible = false;
    }

    public void SetStatus(string status)
    {
        _status.Text = status;
    }

    public void SetSkipEnabled(bool enabled)
    {
        _skip.Disabled = !enabled;
    }

    void Select(double hours)
    {
        _hours = hours;
        string text = Duration.Format(hours);
        _selected.Text = $"Selected: {text}";
        _skip.Text = $"Skip Forward - {text}";
    }
}
