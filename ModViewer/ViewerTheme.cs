using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace ModViewer;

/// <summary>The Mod Viewer's accent colour, saved in its own settings.json.</summary>
internal static class ViewerTheme
{
    public static readonly Color DefaultAccent = new(0.93f, 0.75f, 0.35f);
    public static Color Accent { get; private set; } = DefaultAccent;

    static string _path = "";

    public static void Load(string folder, Action<string> log)
    {
        _path = Path.Combine(folder, "settings.json");

        try
        {
            if (!File.Exists(_path)) return;

            JsonNode? root = JsonNode.Parse(File.ReadAllText(_path));
            if (root?["AccentColor"] is JsonValue value && value.TryGetValue(out string? hex))
                Accent = Parse(hex);
        }
        catch (Exception e)
        {
            log("Mod Viewer settings could not be read: " + e.Message);
        }
    }

    public static void SetAccent(Color color)
    {
        Accent = color;
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

            root["AccentColor"] = ToHex(Accent);
            File.WriteAllText(_path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            log("Mod Viewer settings could not be saved: " + e.Message);
        }
    }

    static string ToHex(Color color) =>
        $"#{Byte(color.R):X2}{Byte(color.G):X2}{Byte(color.B):X2}";

    static int Byte(float value) => Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

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
