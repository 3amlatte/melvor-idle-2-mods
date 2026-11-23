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
    readonly Label _hint;
    readonly Label _themeLabel;
    readonly HBoxContainer _themeGroup;
    readonly OptionButton _themePicker;
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
        ViewerPalette palette = ViewerTheme.Current;

        _layer = new CanvasLayer { Layer = 120, Visible = false };

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(center);

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(820, 620) };
        _panelStyle = new StyleBoxFlat
        {
            BgColor = palette.Panel,
            BorderColor = palette.Accent
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
        _title.AddThemeColorOverride("font_color", palette.Accent);
        header.AddChild(_title);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _themeGroup = new HBoxContainer();
        _themeGroup.AddThemeConstantOverride("separation", 6);
        _themeLabel = new Label { Text = "Theme" };
        _themeLabel.AddThemeFontSizeOverride("font_size", 12);
        _themeLabel.AddThemeColorOverride("font_color", palette.Muted);
        _themeGroup.AddChild(_themeLabel);

        _themePicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(140, 0),
            TooltipText = "Choose a theme"
        };
        foreach (ViewerPalette theme in ViewerTheme.All)
            _themePicker.AddItem(theme.Name);
        _themePicker.Selected = ViewerTheme.IndexOf(ViewerTheme.Current.Id);
        _themePicker.ItemSelected += OnThemeSelected;
        _themeGroup.AddChild(_themePicker);
        header.AddChild(_themeGroup);

        var close = new Button { Text = "Close" };
        close.Pressed += Hide;
        header.AddChild(close);
        column.AddChild(header);

        _hint = new Label
        {
            Text = "Every folder in mods and mods-disabled. Click a mod's name for its settings or window. Turn a mod off to move it out of the loader's path; restart to apply.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hint.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(_hint);

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
        footer.AddChild(_status);

        var modsFolder = new Button { Text = "Mods Folder", TooltipText = Mods.ModsDir };
        modsFolder.Pressed += OpenModsFolder;
        footer.AddChild(modsFolder);

        var restartButton = new Button { Text = "Restart Game" };
        restartButton.Pressed += () => _restart();
        footer.AddChild(restartButton);
        column.AddChild(footer);

        ApplyPalette(false);
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
        _themeGroup.Visible = true;
        ApplyPalette(false);
        Refresh();
        _layer.Visible = true;
    }

    public void Hide()
    {
        _layer.Visible = false;
    }

    void OnThemeSelected(long index)
    {
        if (index < 0 || index >= ViewerTheme.All.Count) return;

        ViewerTheme.Select(ViewerTheme.All[(int)index].Id);
        ViewerTheme.Save(_log);
        ApplyPalette(true);
    }

    void ApplyPalette(bool refresh)
    {
        ViewerPalette palette = ViewerTheme.Current;

        _panelStyle.BgColor = palette.Panel;
        _panelStyle.BorderColor = palette.Accent;
        _panel.AddThemeStyleboxOverride("panel", _panelStyle);

        _title.AddThemeColorOverride("font_color", palette.Accent);
        _hint.AddThemeColorOverride("font_color", palette.Muted);
        _themeLabel.AddThemeColorOverride("font_color", palette.Muted);
        _status.AddThemeColorOverride("font_color", palette.Muted);

        if (refresh) Refresh();
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
            if (!any) _list.AddChild(TextLabel("No 3am mods found."));
        }

        AddHeader("Other mods");
        bool anyOther = false;
        foreach (ModEntry entry in entries)
        {
            if (entry.Is3am) continue;
            _list.AddChild(Row(entry));
            anyOther = true;
        }
        if (!anyOther) _list.AddChild(TextLabel("No other mods found."));
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
        button.AddThemeColorOverride("font_color", ViewerTheme.Current.Accent);
        button.AddThemeColorOverride("font_hover_color", ViewerTheme.Current.Accent.Lightened(0.16f));
        button.Pressed += toggle;
        _list.AddChild(button);
    }

    void AddHeader(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", ViewerTheme.Current.Accent);
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
        sub.AddThemeColorOverride("font_color", ViewerTheme.Current.Muted);
        info.AddChild(sub);
        row.AddChild(info);

        var enabled = new CheckBox { Text = "Enabled", ButtonPressed = entry.Enabled };
        ViewerTheme.StyleCheckBox(enabled);
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
            label.AddThemeColorOverride("font_color", ViewerTheme.Current.Text);
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
        button.AddThemeColorOverride("font_color", ViewerTheme.Current.Text);
        button.AddThemeColorOverride("font_hover_color", ViewerTheme.Current.Accent);
        button.AddThemeColorOverride("font_pressed_color", ViewerTheme.Current.Accent);
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
        _themeGroup.Visible = false;

        var back = new Button { Text = "Back to mods", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        back.Pressed += Back;
        _settings.AddChild(back);

        var title = new Label
        {
            Text = entry.Name.Equals("MelvorAuto", StringComparison.OrdinalIgnoreCase)
                ? "Melvor Auto"
                : entry.Name
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", ViewerTheme.Current.Accent);
        _settings.AddChild(title);

        _settings.AddChild(SettingsPage.Build(entry, _log, SetStatus));
    }

    void Back()
    {
        Clear(_settings);
        _settingsScroll.Visible = false;
        _listScroll.Visible = true;
        _themeGroup.Visible = true;
        Refresh();
    }

    void OpenModsFolder()
    {
        try
        {
            if (Mods.ModsDir.Length == 0)
            {
                SetStatus("The mods folder path is unknown.");
                return;
            }

            OS.CreateProcess("explorer.exe", new[] { Mods.ModsDir }, false);
        }
        catch (Exception e)
        {
            SetStatus("Could not open the mods folder: " + e.Message);
        }
    }

    void SetStatus(string text)
    {
        _status.Text = text;
    }

    static Label TextLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", ViewerTheme.Current.Text);
        return label;
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
