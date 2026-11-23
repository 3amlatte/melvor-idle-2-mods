using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace ModViewer;

internal static class SettingsPage
{
    static readonly string[] MelvorAutoBools =
    {
        "AutoBuryBones", "AutoOpenItems", "AutoFarm", "AutoHarvest",
        "AutoCompost", "AutoWater", "AutoPlant", "AutoMill",
        "AutoRanchMaintenance", "AutoRanchPets"
    };

    public static Control Build(ModEntry entry, Action<string> log, Action<string> status)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);

        string folder = Directory.Exists(entry.Path)
            ? entry.Path
            : Path.GetDirectoryName(entry.Path) ?? entry.Path;
        string path = Path.Combine(folder, "settings.json");

        if (!File.Exists(path))
        {
            column.AddChild(new Label { Text = "This mod has no settings.json." });
            return column;
        }

        if (entry.Name.Equals("MelvorAuto", StringComparison.OrdinalIgnoreCase))
            BuildMelvorAuto(folder, path, column, log, status);
        else
            BuildGeneric(path, column, log, status);

        return column;
    }

    static JsonObject LoadObject(string path, Action<string> log)
    {
        try { return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject(); }
        catch (Exception e)
        {
            log($"Could not read {Path.GetFileName(path)}: {e.Message}");
            return new JsonObject();
        }
    }

    static void BuildGeneric(string path, VBoxContainer column, Action<string> log, Action<string> status)
    {
        JsonObject settings = LoadObject(path, log);

        foreach (var pair in settings.ToList())
        {
            string key = pair.Key;
            if (pair.Value is not JsonValue value)
            {
                column.AddChild(new Label { Text = $"{key}: lists/objects are not editable here." });
                continue;
            }

            if (value.TryGetValue<bool>(out bool b))
            {
                var check = new CheckBox { Text = key, ButtonPressed = b };
                check.Toggled += pressed => settings[key] = JsonValue.Create(pressed);
                column.AddChild(check);
            }
            else if (value.TryGetValue<string>(out string? text))
            {
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = key, CustomMinimumSize = new Vector2(220, 0) });
                var edit = new LineEdit { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                edit.TextChanged += changed => settings[key] = JsonValue.Create(changed);
                row.AddChild(edit);
                column.AddChild(row);
            }
            else if (value.TryGetValue<double>(out double number))
            {
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = key, CustomMinimumSize = new Vector2(220, 0) });
                var edit = new LineEdit
                {
                    Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                edit.TextChanged += changed =>
                {
                    if (double.TryParse(changed, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                        settings[key] = JsonValue.Create(parsed);
                };
                row.AddChild(edit);
                column.AddChild(row);
            }
            else
            {
                column.AddChild(new Label { Text = $"{key}: {value.ToJsonString()}" });
            }
        }

        var save = new Button { Text = "Save", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        save.Pressed += () =>
        {
            try
            {
                File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                status("Saved. Mods that read settings at startup need a restart.");
            }
            catch (Exception e) { status("Could not save: " + e.Message); }
        };
        column.AddChild(save);
    }

    static void BuildMelvorAuto(string folder, string path, VBoxContainer column, Action<string> log, Action<string> status)
    {
        JsonObject settings = LoadObject(path, log);

        var title = new Label { Text = "Melvor Auto" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.93f, 0.75f, 0.35f));
        column.AddChild(title);

        var toggles = new List<(string Key, CheckBox Box)>();
        foreach (string key in MelvorAutoBools)
        {
            var check = new CheckBox
            {
                Text = Label(key),
                ButtonPressed = Bool(settings, key, true)
            };
            toggles.Add((key, check));
            column.AddChild(check);
        }

        column.AddChild(new HSeparator());
        column.AddChild(new Label { Text = "Bones bigger than Big Bone (checked = do not auto-bury)" });

        var boneChecks = new List<(string Id, CheckBox Box)>();
        var openChecks = new List<(string Id, CheckBox Box)>();

        string availablePath = Path.Combine(folder, "available.json");
        if (!File.Exists(availablePath))
        {
            column.AddChild(new Label
            {
                Text = "Open a character once so Melvor Auto can write its bone/openable list."
            });
        }
        else
        {
            JsonObject available = LoadObject(availablePath, log);
            var excludedBones = StringSet(settings, "ExcludedBones");
            var excludedOpen = StringSet(settings, "ExcludedOpenables");

            if (available["Bones"] is JsonArray bones)
            {
                foreach (JsonNode? node in bones)
                {
                    if (node is not JsonObject bone) continue;
                    string id = String(bone, "Id");
                    string name = String(bone, "Name");
                    int points = Int(bone, "Points");
                    if (id == "" || points <= 6) continue;

                    var check = new CheckBox { Text = $"{name} ({points} points)", ButtonPressed = excludedBones.Contains(id) };
                    boneChecks.Add((id, check));
                    column.AddChild(check);
                }
            }

            column.AddChild(new HSeparator());
            column.AddChild(new Label { Text = "Openable items (checked = do not auto-open)" });
            if (available["Openables"] is JsonArray openables)
            {
                foreach (JsonNode? node in openables)
                {
                    if (node is not JsonObject openable) continue;
                    string id = String(openable, "Id");
                    string name = String(openable, "Name");
                    if (id == "") continue;

                    var check = new CheckBox { Text = name, ButtonPressed = excludedOpen.Contains(id) };
                    openChecks.Add((id, check));
                    column.AddChild(check);
                }
            }
        }

        var save = new Button { Text = "Save", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        save.Pressed += () =>
        {
            try
            {
                foreach (var (key, box) in toggles)
                    settings[key] = JsonValue.Create(box.ButtonPressed);

                settings["ExcludedBones"] = ArrayOf(boneChecks.Where(x => x.Box.ButtonPressed).Select(x => x.Id));
                settings["ExcludedOpenables"] = ArrayOf(openChecks.Where(x => x.Box.ButtonPressed).Select(x => x.Id));
                settings["BoneDefaultsApplied"] = JsonValue.Create(true);

                File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                status("Saved. Bone/open changes apply live; farming flags apply when a character next opens.");
            }
            catch (Exception e) { status("Could not save: " + e.Message); }
        };
        column.AddChild(save);
    }

    static bool Bool(JsonObject obj, string key, bool fallback) =>
        obj[key] is JsonValue value && value.TryGetValue(out bool b) ? b : fallback;

    static string Label(string key) => key switch
    {
        "AutoBuryBones" => "Auto bone burial",
        "AutoOpenItems" => "Auto opener",
        "AutoFarm" => "Auto farmer (master)",
        "AutoHarvest" => "Auto harvest",
        "AutoCompost" => "Auto compost",
        "AutoWater" => "Auto water",
        "AutoPlant" => "Auto plant",
        "AutoMill" => "Grain mill stocking (every 2 hours)",
        "AutoRanchMaintenance" => "Auto Ranching: repair all enclosures",
        "AutoRanchPets" => "Auto Ranching: pet all animals",
        _ => key
    };

    static string String(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue(out string? text) ? text : "";

    static int Int(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue(out int number) ? number : 0;

    static HashSet<string> StringSet(JsonObject obj, string key)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (obj[key] is JsonArray array)
            foreach (JsonNode? node in array)
                if (node is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrEmpty(text))
                    set.Add(text);
        return set;
    }

    static JsonArray ArrayOf(IEnumerable<string> ids)
    {
        var array = new JsonArray();
        foreach (string id in ids) array.Add(JsonValue.Create(id));
        return array;
    }
}
