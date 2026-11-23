using System;
using System.Collections.Generic;
using Godot;
using MelvorModLoader;

namespace ModViewer;

internal sealed class ViewerWindow
{
    readonly ModContext _ctx;
    readonly Action _restart;
    readonly Action<string> _log;
    readonly CanvasLayer _layer;
    readonly PanelContainer _panel;
    readonly StyleBoxFlat _panelStyle;
    readonly Label _title;
    readonly HBoxContainer _accentGroup;
    readonly ColorPickerButton _accentPicker;
    readonly VBoxContainer _list;
    readonly VBoxContainer _settings;
    readonly ScrollContainer _listScroll;
    readonly ScrollContainer _settingsScroll;
    readonly Label _status;
    bool _collapsed3am;

    public ViewerWindow(ModContext ctx, Action restart, Action<string> log)
    {
        _ctx = ctx;
        _restart = restart;
        _log = log;

        ViewerTheme.Load(ctx.Directory, log);
        _layer = new CanvasLayer { Layer = 120, Visible = false };

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(center);

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(820, 620) };
        _panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.11f, 0.98f),
            BorderColor = ViewerTheme.Accent
        };
        _panelStyle.SetBorderWidthAll(1);
        _panelStyle.SetCornerRadiusAll(8);
        _panelStyle.SetContentMarginAll(18);
        _panel.AddThemeStyleboxOverride("panel", _panelStyle);
        center.AddChild(_panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(column);

        var header = new HBoxContainer();
        _title = new Label { Text = "Mod Viewer" };
        _title.AddThemeFontSizeOverride("font_size", 22);
        _title.AddThemeColorOverride("font_color", ViewerTheme.Accent);
        header.AddChild(_title);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _accentGroup = new HBoxContainer();
        _accentGroup.AddThemeConstantOverride("separation", 6);
        var accentLabel = new Label { Text = "Accent" };
        accentLabel.AddThemeFontSizeOverride("font_size", 12);
        accentLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        _accentGroup.AddChild(accentLabel);

        _accentPicker = new ColorPickerButton
        {
            Color = ViewerTheme.Accent,
            EditAlpha = false,
            CustomMinimumSize = new Vector2(46, 0),
            TooltipText = "Choose the Mod Viewer accent colour"
        };
        _accentPicker.ColorChanged += OnAccentChanged;
        _accentPicker.PopupClosed += OnAccentPopupClosed;
        _accentGroup.AddChild(_accentPicker);
        header.AddChild(_accentGroup);

        var close = new Button { Text = "Close" };
        close.Pressed += Hide;
        header.AddChild(close);
        column.AddChild(header);

        var hint = new Label
        {
            Text = "Every folder in mods and mods-disabled. Click a mod's name for its settings or window. Turn a mod off to move it out of the loader's path; restart to apply.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        column.AddChild(hint);

        var body = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddChild(body);

        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 4);
        _listScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _listScroll.AddChild(_list);
        body.AddChild(_listScroll);

        _settings = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _settings.AddThemeConstantOverride("separation", 6);
        _settingsScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false
        };
        _settingsScroll.AddChild(_settings);
        body.AddChild(_settingsScroll);

        var footer = new HBoxContainer();
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeFontSizeOverride("font_size", 12);
        _status.AddThemeColorOverride("font_color", new Color(0.80f, 0.80f, 0.80f));
        footer.AddChild(_status);

        var restartButton = new Button { Text = "Restart Game" };
        restartButton.Pressed += () => _restart();
        footer.AddChild(restartButton);
        column.AddChild(footer);

        ctx.Tree.Root.AddChild(_layer);
    }

    public bool Visible => _layer.Visible;

    public void Toggle()
    {
        if (_layer.Visible)
        {
            _layer.Visible = false;
            return;
        }

        _settingsScroll.Visible = false;
        _listScroll.Visible = true;
        _accentGroup.Visible = true;
        Refresh();
        _layer.Visible = true;
    }

    public void Hide()
    {
        _layer.Visible = false;
    }

    void OnAccentChanged(Color color)
    {
        ViewerTheme.SetAccent(color);
        _panelStyle.BorderColor = color;
        _panel.AddThemeStyleboxOverride("panel", _panelStyle);
        _title.AddThemeColorOverride("font_color", color);
    }

    void OnAccentPopupClosed()
    {
        Refresh();
        ViewerTheme.Save(_log);
    }

    void Refresh()
    {
        Clear(_list);

        List<ModEntry> entries = Mods.Scan();

        AddGroupHeader("3am Mods", _collapsed3am, () =>
        {
            _collapsed3am = !_collapsed3am;
            Refresh();
        });

        if (!_collapsed3am)
        {
            bool any = false;
            foreach (ModEntry entry in entries)
            {
                if (!entry.Is3am) continue;
                _list.AddChild(Row(entry));
                any = true;
            }
            if (!any) _list.AddChild(new Label { Text = "No 3am mods found." });
        }

        AddHeader("Other mods");
        bool anyOther = false;
        foreach (ModEntry entry in entries)
        {
            if (entry.Is3am) continue;
            _list.AddChild(Row(entry));
            anyOther = true;
        }
        if (!anyOther) _list.AddChild(new Label { Text = "No other mods found." });
    }

    void AddGroupHeader(string text, bool collapsed, Action toggle)
    {
        _list.AddChild(new HSeparator());

        var button = new Button
        {
            Text = (collapsed ? "▶  " : "▼  ") + text,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", ViewerTheme.Accent);
        button.AddThemeColorOverride("font_hover_color", ViewerTheme.Accent.Lightened(0.16f));
        button.Pressed += toggle;
        _list.AddChild(button);
    }

    void AddHeader(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", ViewerTheme.Accent);
        _list.AddChild(new HSeparator());
        _list.AddChild(label);
    }

    Control Row(ModEntry entry)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddChild(NameControl(entry));

        string detail = entry.Version == "" ? "" : $"v{entry.Version}  ";
        detail += entry.Enabled ? "enabled" : "disabled";
        var sub = new Label { Text = detail };
        sub.AddThemeFontSizeOverride("font_size", 11);
        sub.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        info.AddChild(sub);
        row.AddChild(info);

        var enabled = new CheckBox { Text = "Enabled", ButtonPressed = entry.Enabled };
        enabled.Toggled += pressed =>
        {
            if (Mods.SetEnabled(entry, pressed, out string error))
            {
                SetStatus("Restart required to apply the change.");
                Refresh();
            }
            else
            {
                SetStatus("Could not move the mod: " + error);
                enabled.SetPressedNoSignal(!pressed);
            }
        };
        row.AddChild(enabled);

        return row;
    }

    Control NameControl(ModEntry entry)
    {
        if (!UiAccess.CanOpen(entry))
        {
            var label = new Label { Text = entry.Name };
            label.AddThemeFontSizeOverride("font_size", 15);
            return label;
        }

        var button = new Button
        {
            Text = entry.Name,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = entry.HasSettings ? "Open settings" : "Open this mod's window"
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", new Color(0.90f, 0.90f, 0.90f));
        button.AddThemeColorOverride("font_hover_color", ViewerTheme.Accent);
        button.AddThemeColorOverride("font_pressed_color", ViewerTheme.Accent);
        button.Pressed += () => Open(entry);
        return button;
    }

    void Open(ModEntry entry)
    {
        if (entry.HasSettings)
        {
            ShowSettings(entry);
            return;
        }

        if (UiAccess.TryOpenWindow(entry, out string message))
        {
            // Get out of the way of the mod's own window.
            Hide();
        }
        else
        {
            SetStatus(message);
        }
    }

    void ShowSettings(ModEntry entry)
    {
        Clear(_settings);
        _listScroll.Visible = false;
        _settingsScroll.Visible = true;
        _accentGroup.Visible = false;

        var back = new Button { Text = "Back to mods", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        back.Pressed += Back;
        _settings.AddChild(back);

        var title = new Label { Text = entry.Name };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", ViewerTheme.Accent);
        _settings.AddChild(title);

        _settings.AddChild(SettingsPage.Build(entry, _log, SetStatus));
    }

    void Back()
    {
        Clear(_settings);
        _settingsScroll.Visible = false;
        _listScroll.Visible = true;
        _accentGroup.Visible = true;
        Refresh();
    }

    void SetStatus(string text)
    {
        _status.Text = text;
    }

    static void Clear(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
