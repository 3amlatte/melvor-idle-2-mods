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
    static readonly string[] FarmerKeys =
    {
        "AutoHarvest", "AutoCompost", "AutoWater", "AutoPlant", "AutoMill"
    };

    static readonly string[] RanchKeys =
    {
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
            column.AddChild(TextLabel("This mod has no settings.json."));
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
                column.AddChild(TextLabel($"{key}: lists/objects are not editable here."));
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
                var label = TextLabel(key);
                label.CustomMinimumSize = new Vector2(220, 0);
                row.AddChild(label);
                var edit = new LineEdit { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                edit.TextChanged += changed => settings[key] = JsonValue.Create(changed);
                row.AddChild(edit);
                column.AddChild(row);
            }
            else if (value.TryGetValue<double>(out double number))
            {
                var row = new HBoxContainer();
                var label = TextLabel(key);
                label.CustomMinimumSize = new Vector2(220, 0);
                row.AddChild(label);
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
                column.AddChild(TextLabel($"{key}: {value.ToJsonString()}"));
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

        var toggles = new List<(string Key, CheckBox Box)>();
        AddToggle(column, toggles, settings, "AutoBuryBones");
        AddToggle(column, toggles, settings, "AutoOpenItems");

        var farmerBody = new VBoxContainer();
        farmerBody.AddThemeConstantOverride("separation", 6);
        AddSectionHeader(column, "Auto Farmer", farmerBody,
            "Show or hide the Farming automation options.");
        foreach (string key in FarmerKeys)
            AddToggle(farmerBody, toggles, settings, key);

        var ranchBody = new VBoxContainer();
        ranchBody.AddThemeConstantOverride("separation", 6);
        AddSectionHeader(column, "Auto Ranching", ranchBody,
            "Show or hide the Ranching options.");
        foreach (string key in RanchKeys)
            AddToggle(ranchBody, toggles, settings, key);

        column.AddChild(new HSeparator());
        column.AddChild(TextLabel("Do not bury"));

        var boneChecks = new List<(string Id, CheckBox Box)>();
        var openChecks = new List<(string Id, CheckBox Box)>();

        string availablePath = Path.Combine(folder, "available.json");
        if (!File.Exists(availablePath))
        {
            column.AddChild(TextLabel("Open a character once so Melvor Auto can write its bone/openable list."));
        }
        else
        {
            JsonObject available = LoadObject(availablePath, log);
            var excludedBones = StringSet(settings, "ExcludedBones");
            var excludedOpen = StringSet(settings, "ExcludedOpenables");

            if (available["Bones"] is JsonArray bones)
            {
                var grid = NewCardGrid();
                column.AddChild(grid);

                foreach (JsonNode? node in bones)
                {
                    if (node is not JsonObject bone) continue;
                    string id = String(bone, "Id");
                    string name = String(bone, "Name");
                    string icon = String(bone, "IconPath");
                    int points = Int(bone, "Points");
                    if (id == "" || points <= 6) continue;

                    CheckBox check = AddItemCard(grid, name, icon, excludedBones.Contains(id),
                        $"Do not bury {name} ({points} points).");
                    boneChecks.Add((id, check));
                }
            }

            column.AddChild(new HSeparator());
            column.AddChild(TextLabel("Do not open"));
            if (available["Openables"] is JsonArray openables)
            {
                var grid = NewCardGrid();
                column.AddChild(grid);

                foreach (JsonNode? node in openables)
                {
                    if (node is not JsonObject openable) continue;
                    string id = String(openable, "Id");
                    string name = String(openable, "Name");
                    string icon = String(openable, "IconPath");
                    if (id == "") continue;

                    CheckBox check = AddItemCard(grid, name, icon, excludedOpen.Contains(id),
                        $"Do not open {name}.");
                    openChecks.Add((id, check));
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
                settings.Remove("AutoFarm");

                File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                status("Saved. Bone/open changes apply live; farming flags apply when a character next opens.");
            }
            catch (Exception e) { status("Could not save: " + e.Message); }
        };
        column.AddChild(save);
    }

    static void AddToggle(VBoxContainer parent, List<(string Key, CheckBox Box)> toggles,
        JsonObject settings, string key)
    {
        var check = new CheckBox
        {
            Text = Label(key),
            ButtonPressed = Bool(settings, key, true),
            TooltipText = Description(key)
        };
        toggles.Add((key, check));
        parent.AddChild(check);
    }

    static void AddSectionHeader(VBoxContainer column, string title, VBoxContainer body,
        string tooltip)
    {
        var header = new Button
        {
            Text = "▼  " + title,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = tooltip
        };
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", ViewerTheme.Current.Accent);
        header.AddThemeColorOverride("font_hover_color", ViewerTheme.Current.Accent.Lightened(0.16f));
        header.Pressed += () =>
        {
            body.Visible = !body.Visible;
            header.Text = (body.Visible ? "▼  " : "▶  ") + title;
        };

        column.AddChild(header);
        column.AddChild(body);
    }

    static bool Bool(JsonObject obj, string key, bool fallback) =>
        obj[key] is JsonValue value && value.TryGetValue(out bool b) ? b : fallback;

    static string Label(string key) => key switch
    {
        "AutoBuryBones" => "Auto bone burial",
        "AutoOpenItems" => "Auto opener",
        "AutoHarvest" => "Auto harvest",
        "AutoCompost" => "Auto compost",
        "AutoWater" => "Auto water",
        "AutoPlant" => "Auto plant",
        "AutoMill" => "Grain mill stocking",
        "AutoRanchMaintenance" => "Repair all enclosures",
        "AutoRanchPets" => "Pet all animals",
        _ => key
    };

    static string Description(string key) => key switch
    {
        "AutoBuryBones" => "Bury every bone that reaches the bank, except the exclusions below.",
        "AutoOpenItems" => "Open chests and other openables as soon as they arrive.",
        "AutoHarvest" => "Harvest ripe crops automatically.",
        "AutoCompost" => "Compost a plot automatically before replanting.",
        "AutoWater" => "Water crops automatically.",
        "AutoPlant" => "Replant harvested plots with the selected crop.",
        "AutoMill" => "Stock the grain mill from the bank and start it if stopped.",
        "AutoRanchMaintenance" => "Repair all enclosures after each hourly maintenance tick.",
        "AutoRanchPets" => "Pet animals as soon as they want attention.",
        _ => ""
    };

    static Label TextLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", ViewerTheme.Current.Text);
        return label;
    }

    static GridContainer NewCardGrid()
    {
        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    static CheckBox AddItemCard(GridContainer grid, string name, string iconPath, bool excluded,
        string tooltip)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(120, 100),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = name
        };
        card.AddThemeStyleboxOverride("panel", CardStyle());

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 2);
        card.AddChild(content);

        var icon = new TextureRect
        {
            Texture = LoadIcon(iconPath),
            CustomMinimumSize = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        content.AddChild(icon);

        var label = new Label
        {
            Text = name,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", ViewerTheme.Current.Text);
        content.AddChild(label);

        var check = new CheckBox
        {
            ButtonPressed = excluded,
            TooltipText = tooltip,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        content.AddChild(check);

        grid.AddChild(card);
        return check;
    }

    static StyleBoxFlat CardStyle()
    {
        ViewerPalette palette = ViewerTheme.Current;
        float luminance = 0.2126f * palette.Panel.R
                          + 0.7152f * palette.Panel.G
                          + 0.0722f * palette.Panel.B;
        Color background = luminance < 0.5f
            ? palette.Panel.Lightened(0.07f)
            : palette.Panel.Darkened(0.05f);
        Color border = palette.Muted;
        border.A = 0.35f;

        var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(5);
        return style;
    }

    static Texture2D? LoadIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try { return ResourceLoader.Load<Texture2D>(path); }
        catch { return null; }
    }

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
