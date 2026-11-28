using System;
using System.Collections.Generic;
using Godot;
using MelvorModLoader;

namespace MelvorAuto;

/// <summary>
/// F3 window used when Mod Viewer is not installed: the Auto Farmer toggles.
/// With Mod Viewer installed, the same settings are edited there instead.
/// </summary>
internal sealed class AutoFarmerWindow
{
    static readonly (string Key, string Label, string Hint)[] Options =
    {
        ("NoWineCost", "No wine cost", "Remove the wine cost from Farming automation."),
        ("Automations", "Automations", "Turn on auto-plant, auto-harvest, auto-water and auto-compost for every purchased plot."),
        ("AutoMill", "Grain mill stocking", "Stock the grain mill from the bank and start it if stopped."),
    };

    static readonly Color Accent = new(0.929f, 0.749f, 0.349f);
    static readonly Color Panel = new(0.071f, 0.078f, 0.110f);
    static readonly Color Muted = new(0.690f, 0.690f, 0.690f);

    readonly CanvasLayer _layer;
    readonly Label _status;
    readonly List<(string Key, CheckBox Box)> _boxes = new();
    readonly Func<Settings> _settings;
    readonly Action<Settings> _save;
    readonly Action<string> _log;

    public AutoFarmerWindow(ModContext ctx, Func<Settings> settings, Action<Settings> save, Action<string> log)
    {
        _settings = settings;
        _save = save;
        _log = log;

        _layer = new CanvasLayer { Layer = 111, Visible = false };

        // Bottom-right corner, with a 20 px gap, like TimeSkip.
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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        var panelStyle = new StyleBoxFlat { BgColor = Panel, BorderColor = Accent };
        panelStyle.SetBorderWidthAll(1);
        panelStyle.SetCornerRadiusAll(6);
        panelStyle.SetContentMarginAll(14);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        right.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);

        var title = new Label { Text = "Melvor Auto" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Accent);
        column.AddChild(title);

        var subtitle = new Label { Text = "Auto Farmer" };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(subtitle);

        var hint = new Label
        {
            Text = "Farming automation settings. Install Mod Viewer to edit every Melvor Auto option in game.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 10);
        hint.AddThemeColorOverride("font_color", Muted);
        column.AddChild(hint);

        foreach (var (key, label, tooltip) in Options)
        {
            var check = new CheckBox { Text = label, TooltipText = tooltip };
            _boxes.Add((key, check));
            column.AddChild(check);
        }

        _status = new Label { Text = "Changes are saved to settings.json." };
        _status.AddThemeFontSizeOverride("font_size", 10);
        _status.AddThemeColorOverride("font_color", Muted);
        column.AddChild(_status);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(buttons);

        var close = new Button { Text = "Close" };
        close.Pressed += Hide;
        buttons.AddChild(close);

        var saveButton = new Button { Text = "Save" };
        saveButton.Pressed += Save;
        buttons.AddChild(saveButton);

        ctx.Tree.Root.AddChild(_layer);
        SyncFromSettings();
    }

    public bool Visible => _layer.Visible;

    public void Toggle()
    {
        if (!_layer.Visible) SyncFromSettings();
        _layer.Visible = !_layer.Visible;
    }

    public void Hide() => _layer.Visible = false;

    void Save()
    {
        Settings settings = _settings();
        foreach (var (key, box) in _boxes) Apply(settings, key, box.ButtonPressed);
        _save(settings);
        _status.Text = "Saved. Farming flags apply when a character next opens.";
        _log("settings.json saved from the F3 window.");
    }

    void SyncFromSettings()
    {
        Settings settings = _settings();
        foreach (var (key, box) in _boxes) box.ButtonPressed = Read(settings, key);
        _status.Text = "Changes are saved to settings.json.";
    }

    static bool Read(Settings settings, string key) => key switch
    {
        "NoWineCost" => settings.NoWineCost,
        "Automations" => settings.Automations,
        "AutoMill" => settings.AutoMill,
        _ => true
    };

    static void Apply(Settings settings, string key, bool value)
    {
        switch (key)
        {
            case "NoWineCost": settings.NoWineCost = value; break;
            case "Automations": settings.Automations = value; break;
            case "AutoMill": settings.AutoMill = value; break;
        }
    }
}
