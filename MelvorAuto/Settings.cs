using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MelvorAuto;

public sealed class Settings
{
    public bool AutoBuryBones { get; set; } = true;
    /// <summary>True once the default "skip bones better than Big Bone" list has been filled in.</summary>
    public bool BoneDefaultsApplied { get; set; }
    public List<string> ExcludedBones { get; set; } = new();
    public bool AutoOpenItems { get; set; } = true;
    public List<string> ExcludedOpenables { get; set; } = new();
    public bool AutoHarvest { get; set; } = true;
    public bool AutoCompost { get; set; } = true;
    public bool AutoWater { get; set; } = true;
    public bool AutoPlant { get; set; } = true;
    public bool AutoMill { get; set; } = true;
    public bool AutoRanchMaintenance { get; set; } = true;
    public bool AutoRanchPets { get; set; } = true;

    /// <summary>Character name -> Unix time of the last mill stocking.</summary>
    public Dictionary<string, long> MillStockedAtUnix { get; set; } = new();

    static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static Settings Load(string path, Action<string> log)
    {
        try
        {
            if (File.Exists(path))
            {
                Settings? settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), s_options);
                if (settings != null) return settings;
            }
        }
        catch (Exception e)
        {
            log($"settings.json could not be read, using defaults: {e.Message}");
        }

        var fresh = new Settings();
        fresh.Save(path, log);
        return fresh;
    }

    public void Save(string path, Action<string> log)
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(this, s_options)); }
        catch (Exception e) { log($"settings.json could not be saved: {e.Message}"); }
    }
}
