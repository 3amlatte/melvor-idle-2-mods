using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using Godot;

namespace TimeSkip;

/// <summary>
/// Reads the accent colour from Mod Viewer when it is installed. With no Mod Viewer, TimeSkip
/// keeps its own default gold.
/// </summary>
internal static class TimeSkipTheme
{
    public static readonly Color DefaultAccent = new(0.93f, 0.75f, 0.35f);
    public static Color Accent { get; private set; } = DefaultAccent;

    static string _path = "";
    static DateTime _stamp = DateTime.MinValue;

    public static void Initialize(string modsFolder)
    {
        _path = Path.Combine(modsFolder, "ModViewer", "settings.json");
    }

    public static bool RefreshIfChanged()
    {
        DateTime stamp = DateTime.MinValue;
        try
        {
            if (_path.Length > 0 && File.Exists(_path))
                stamp = File.GetLastWriteTimeUtc(_path);
        }
        catch { stamp = DateTime.MinValue; }

        if (stamp == _stamp) return false;
        _stamp = stamp;

        Color next = DefaultAccent;
        if (stamp != DateTime.MinValue)
        {
            try
            {
                JsonNode? root = JsonNode.Parse(File.ReadAllText(_path));
                if (root?["AccentColor"] is JsonValue value && value.TryGetValue(out string? hex))
                    next = Parse(hex);
            }
            catch { }
        }

        if (next == Accent) return false;
        Accent = next;
        return true;
    }

    static Color Parse(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            return DefaultAccent;
        }

        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }
}
