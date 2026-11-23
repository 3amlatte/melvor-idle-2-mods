using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MelvorAuto;

public sealed class BoneInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Points { get; set; }
}

public sealed class OpenableInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>
/// Written next to the DLL so the 3am Mods hub (and any external tool) can list the bones and
/// openable items without needing to know the game's data itself.
/// </summary>
public sealed class Available
{
    public List<BoneInfo> Bones { get; set; } = new();
    public List<OpenableInfo> Openables { get; set; } = new();

    static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };

    public void Save(string path)
    {
        try
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, s_options));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // The hub will simply show "open a character first" if this cannot be written.
        }
    }
}
