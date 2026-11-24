using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using Godot;

namespace TimeSkip;

/// <summary>
/// Reads the Mod Viewer theme when it is installed. With no Mod Viewer, TimeSkip keeps the
/// default AI palette (dark panel, gold accent).
/// </summary>
internal static class TimeSkipTheme
{
    public static Color Accent { get; private set; } = Hex("#EDBF59");
    public static Color Panel { get; private set; } = Hex("#12141C");
    public static Color Text { get; private set; } = Hex("#FFFFFF");
    public static Color Muted { get; private set; } = Hex("#B0B0B0");

    static string _path = "";
    static string _legacyPath = "";
    static DateTime _stamp = DateTime.MinValue;

    public static void Initialize(string modsFolder)
    {
        _path = Path.Combine(modsFolder, "ModViewer-settings.json");
        _legacyPath = Path.Combine(modsFolder, "ModViewer", "settings.json");
    }

    public static bool RefreshIfChanged()
    {
        string source = File.Exists(_path)
            ? _path
            : File.Exists(_legacyPath) ? _legacyPath : "";

        DateTime stamp = DateTime.MinValue;
        try
        {
            if (source.Length > 0)
                stamp = File.GetLastWriteTimeUtc(source);
        }
        catch { stamp = DateTime.MinValue; }

        if (stamp == _stamp) return false;

        Color accent = Hex("#EDBF59");
        Color panel = Hex("#12141C");
        Color text = Hex("#FFFFFF");
        Color muted = Hex("#B0B0B0");

        if (stamp != DateTime.MinValue)
        {
            try
            {
                JsonNode? root = JsonNode.Parse(File.ReadAllText(source));
                accent = Read(root, "AccentColor", accent);
                panel = Read(root, "PanelColor", panel);
                text = Read(root, "TextColor", text);
                muted = Read(root, "MutedColor", muted);
            }
            catch
            {
                // Mod Viewer may be halfway through writing the file; try again next tick.
                return false;
            }
        }

        _stamp = stamp;
        if (accent == Accent && panel == Panel && text == Text && muted == Muted)
            return false;

        Accent = accent;
        Panel = panel;
        Text = text;
        Muted = muted;
        return true;
    }

    static Color Read(JsonNode? root, string key, Color fallback) =>
        root?[key] is JsonValue value && value.TryGetValue(out string? hex)
            ? Parse(hex, fallback)
            : fallback;

    static Color Parse(string hex, Color fallback)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            return fallback;
        }

        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }

    static Color Hex(string hex) => Parse(hex, new Color(0.07f, 0.08f, 0.11f));
}
