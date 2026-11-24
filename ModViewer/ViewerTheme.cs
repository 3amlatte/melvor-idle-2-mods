using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace ModViewer;

internal sealed record ViewerPalette(
    string Id,
    string Name,
    Color Panel,
    Color Accent,
    Color Text,
    Color Muted);

/// <summary>Theme list and the selected theme, saved in Mod Viewer's settings.json.</summary>
internal static class ViewerTheme
{
    public static readonly IReadOnlyList<ViewerPalette> All = new[]
    {
        new ViewerPalette("AI", "AI",
            Hex("#12141C"), Hex("#EDBF59"), Hex("#FFFFFF"), Hex("#B0B0B0")),
        new ViewerPalette("Dark", "Dark",
            Hex("#0B0D10"), Hex("#8B949E"), Hex("#E6EDF3"), Hex("#8B949E")),
        new ViewerPalette("Light", "Light",
            Hex("#F4F1EA"), Hex("#8A5A00"), Hex("#211F1B"), Hex("#6B665C")),
        new ViewerPalette("Midnight", "Midnight",
            Hex("#0A1020"), Hex("#7AA2FF"), Hex("#DCE6FF"), Hex("#8A9BC4")),
        new ViewerPalette("Forest", "Forest",
            Hex("#0F1A14"), Hex("#7BD88F"), Hex("#E3F0E6"), Hex("#93A89B")),
        new ViewerPalette("Sakura", "Sakura",
            Hex("#20141C"), Hex("#F3A6C8"), Hex("#F7E9F0"), Hex("#B99AA9")),
        new ViewerPalette("Contrast", "Contrast",
            Hex("#000000"), Hex("#FFD400"), Hex("#FFFFFF"), Hex("#C8C8C8"))
    };

    public static ViewerPalette Current { get; private set; } =
        All.FirstOrDefault(t => t.Id == "Dark") ?? All[0];

    static string _path = "";

    public static void Load(string folder, Action<string> log)
    {
        string modsFolder = Directory.GetParent(folder)?.FullName ?? folder;
        _path = Path.Combine(modsFolder, "ModViewer-settings.json");
        string legacy = Path.Combine(folder, "settings.json");
        string readPath = File.Exists(_path) ? _path : (File.Exists(legacy) ? legacy : "");

        try
        {
            if (readPath.Length > 0)
            {
                JsonNode? root = JsonNode.Parse(File.ReadAllText(readPath));
                if (root?["Theme"] is JsonValue value && value.TryGetValue(out string? id))
                    Current = Find(id) ?? All.FirstOrDefault(t => t.Id == "Dark") ?? All[0];
            }
        }
        catch (Exception e)
        {
            log("Mod Viewer settings could not be read: " + e.Message);
        }

        // Persist the resolved theme outside the mod folder, so updating Mod Viewer does not
        // reset it, and keep the colours used by TimeSkip in sync.
        Save(log);
    }

    public static void Select(string id)
    {
        Current = Find(id) ?? All[0];
    }

    public static void Save(Action<string> log)
    {
        if (_path.Length == 0) return;

        try
        {
            JsonObject root;
            try
            {
                root = (File.Exists(_path) ? JsonNode.Parse(File.ReadAllText(_path)) as JsonObject : null)
                       ?? new JsonObject();
            }
            catch { root = new JsonObject(); }

            root["Theme"] = Current.Id;
            root["AccentColor"] = ToHex(Current.Accent);
            root["PanelColor"] = ToHex(Current.Panel);
            root["TextColor"] = ToHex(Current.Text);
            root["MutedColor"] = ToHex(Current.Muted);

            File.WriteAllText(_path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            log("Mod Viewer settings could not be saved: " + e.Message);
        }
    }

    public static int IndexOf(string id)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return 0;
    }

    /// <summary>Button, checkbox and input styling for the selected palette.</summary>
    public static Theme BuildControlTheme()
    {
        ViewerPalette palette = Current;
        bool dark = Luminance(palette.Panel) < 0.5f;
        bool neutralGrey = string.Equals(palette.Id, "Dark", StringComparison.OrdinalIgnoreCase);

        Color control = neutralGrey
            ? Hex("#2A2E35")
            : dark ? palette.Panel.Lightened(0.10f) : palette.Panel.Darkened(0.07f);
        Color hover = neutralGrey
            ? Hex("#3A3F48")
            : dark ? control.Lightened(0.06f) : control.Darkened(0.05f);
        Color pressed = neutralGrey
            ? Hex("#4A505B")
            : control.Lerp(palette.Accent, 0.30f);
        Color disabled = control;
        disabled.A = 0.55f;

        Color border = neutralGrey ? Hex("#434850") : palette.Muted;
        if (!neutralGrey) border.A = 0.35f;
        Color hoverBorder = neutralGrey ? Hex("#6A707A") : palette.Accent;
        if (!neutralGrey) hoverBorder.A = 0.65f;
        Color focusBorder = neutralGrey ? Hex("#8A909A") : palette.Accent;
        Color pressedText = Contrast(pressed);

        StyleBoxFlat normal = Box(control, border);
        StyleBoxFlat hoverBox = Box(hover, hoverBorder);
        StyleBoxFlat pressedBox = Box(pressed, hoverBorder);
        StyleBoxFlat disabledBox = Box(disabled, border);
        StyleBoxFlat focusBox = Box(new Color(0, 0, 0, 0), focusBorder);

        var theme = new Theme();
        theme.SetColor("font_color", "Label", palette.Text);

        StyleControls(theme, "Button", normal, hoverBox, pressedBox, disabledBox, focusBox,
            palette, pressedText);
        StyleControls(theme, "CheckBox", normal, hoverBox, pressedBox, disabledBox, focusBox,
            palette, pressedText);
        StyleControls(theme, "OptionButton", normal, hoverBox, pressedBox, disabledBox, focusBox,
            palette, pressedText);

        theme.SetColor("font_color", "LineEdit", palette.Text);
        theme.SetColor("font_placeholder_color", "LineEdit", palette.Muted);
        theme.SetColor("caret_color", "LineEdit", palette.Accent);
        Color selection = palette.Accent;
        selection.A = 0.35f;
        theme.SetColor("selection_color", "LineEdit", selection);
        theme.SetStylebox("normal", "LineEdit", Box(control, border));
        theme.SetStylebox("focus", "LineEdit", Box(control, focusBorder));
        theme.SetStylebox("read_only", "LineEdit", Box(disabled, border));

        return theme;
    }

    static void StyleControls(Theme theme, string type,
        StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox disabled, StyleBox focus,
        ViewerPalette palette, Color pressedText)
    {
        theme.SetStylebox("normal", type, normal);
        theme.SetStylebox("hover", type, hover);
        theme.SetStylebox("pressed", type, pressed);
        theme.SetStylebox("hover_pressed", type, pressed);
        theme.SetStylebox("disabled", type, disabled);
        theme.SetStylebox("focus", type, focus);

        theme.SetColor("font_color", type, palette.Text);
        theme.SetColor("font_hover_color", type, palette.Text);
        theme.SetColor("font_pressed_color", type, pressedText);
        theme.SetColor("font_disabled_color", type, palette.Muted);
        theme.SetColor("font_focus_color", type, palette.Text);
        theme.SetColor("icon_normal_color", type, palette.Text);
        theme.SetColor("icon_hover_color", type, palette.Text);
        theme.SetColor("icon_pressed_color", type, pressedText);
        theme.SetColor("icon_disabled_color", type, palette.Muted);
    }

    static StyleBoxFlat Box(Color background, Color border)
    {
        var box = new StyleBoxFlat { BgColor = background, BorderColor = border };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(5);
        box.SetContentMarginAll(6);
        return box;
    }

    static float Luminance(Color color) =>
        0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;

    static Color Contrast(Color background) =>
        Luminance(background) < 0.55f ? new Color(1, 1, 1) : new Color(0, 0, 0);

    static ViewerPalette? Find(string id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    static string ToHex(Color color) =>
        $"#{Byte(color.R):X2}{Byte(color.G):X2}{Byte(color.B):X2}";

    static int Byte(float value) => Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    static Color Hex(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            return new Color(0.07f, 0.08f, 0.11f);
        }

        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }
}
