using System;
using Godot;
using MelvorModLoader;

namespace TimeSkip;

/// <summary>The F4 window: pick a duration, then press Skip Forward.</summary>
internal sealed class TimeSkipWindow
{
    static readonly (string Label, double Hours)[] Options =
    {
        ("1 hour", 1), ("2 hours", 2), ("4 hours", 4),
        ("6 hours", 6), ("12 hours", 12), ("24 hours", 24),
        ("2 days", 48), ("4 days", 96), ("7 days", 168)
    };

    readonly CanvasLayer _layer;
    readonly Label _selected;
    readonly Button _skip;
    readonly Label _status;
    double _hours = 24;

    public TimeSkipWindow(ModContext ctx, Action<double> onSkip)
    {
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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(448, 0) };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.11f, 0.98f),
            BorderColor = new Color(0.85f, 0.70f, 0.30f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(14);
        panel.AddThemeStyleboxOverride("panel", style);
        right.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);

        var title = new Label { Text = "TimeSkip" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.93f, 0.75f, 0.35f));
        column.AddChild(title);

        var hint = new Label
        {
            Text = "Skip forward by simulating the selected time. The game pauses, runs the offline calculation, then shows its summary.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 10);
        hint.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        column.AddChild(hint);

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
        _status.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
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

        ctx.Tree.Root.AddChild(_layer);
    }

    public bool Visible => _layer.Visible;

    public void Toggle()
    {
        _layer.Visible = !_layer.Visible;
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
