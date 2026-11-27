using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ModViewer;

internal sealed class ModEntry
{
    public string Name = "";
    public string Assembly = "";
    public string Group = "Other mods";
    public string Path = "";
    public string Dll = "";
    public bool Enabled;
    public bool Is3am;
    public bool HasSettings;
    public string Version = "";
}

internal static class Mods
{
    public static string ModsDir = "";
    public static string DisabledDir = "";

    public static List<ModEntry> Scan()
    {
        Dictionary<string, string> groups = LoadGroupMap();
        var list = new List<ModEntry>();
        ScanDir(ModsDir, true, list, groups);
        ScanDir(DisabledDir, false, list, groups);
        return list
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static void ScanDir(string dir, bool enabled, List<ModEntry> list,
        Dictionary<string, string> groups)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        foreach (string sub in Directory.GetDirectories(dir))
        {
            string[] dlls = Directory.GetFiles(sub, "*.dll");
            if (dlls.Length == 0) continue;

            string folder = Path.GetFileName(sub);
            string dll = dlls.FirstOrDefault(f =>
                            Path.GetFileNameWithoutExtension(f).Equals(folder, StringComparison.OrdinalIgnoreCase))
                        ?? dlls[0];
            var info = AssemblyInfoOf(dll);

            list.Add(new ModEntry
            {
                Name = folder,
                Assembly = info.Name,
                Group = GroupForFolder(sub, folder, info.Name, groups),
                Path = sub,
                Dll = dll,
                Enabled = enabled,
                Is3am = File.Exists(Path.Combine(sub, "3am.mod")),
                HasSettings = File.Exists(Path.Combine(sub, "settings.json")),
                Version = info.Version
            });
        }

        foreach (string dll in Directory.GetFiles(dir, "*.dll"))
        {
            var info = AssemblyInfoOf(dll);
            string name = Path.GetFileNameWithoutExtension(dll);

            list.Add(new ModEntry
            {
                Name = name,
                Assembly = info.Name,
                Group = MapGroup(name, info.Name, groups) ?? "Other mods",
                Path = dll,
                Dll = dll,
                Enabled = enabled,
                HasSettings = File.Exists(Path.Combine(dir, "settings.json")),
                Version = info.Version
            });
        }
    }

    static string GroupForFolder(string path, string folder, string assembly,
        Dictionary<string, string> groups)
    {
        string? mapped = MapGroup(folder, assembly, groups);
        if (mapped != null) return mapped;

        try
        {
            string file = Path.Combine(path, "group.txt");
            if (File.Exists(file))
            {
                string? line = File.ReadLines(file)
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            }
        }
        catch { }

        return File.Exists(Path.Combine(path, "3am.mod")) ? "3am Mods" : "Other mods";
    }

    static string? MapGroup(string name, string assembly, Dictionary<string, string> groups)
    {
        if (groups.TryGetValue(name, out string? group)) return group;
        if (assembly.Length > 0 && groups.TryGetValue(assembly, out group)) return group;
        return null;
    }

    static Dictionary<string, string> LoadGroupMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(ModsDir)) return map;

        string path = Path.Combine(ModsDir, "ModViewer-groups.json");
        if (!File.Exists(path)) return map;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return map;

            foreach (var pair in root)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                if (pair.Value is not JsonArray names) continue;

                foreach (JsonNode? node in names)
                {
                    if (node is JsonValue value &&
                        value.TryGetValue(out string? name) &&
                        !string.IsNullOrWhiteSpace(name))
                    {
                        map[name.Trim()] = pair.Key;
                    }
                }
            }
        }
        catch { }

        return map;
    }

    static (string Name, string Version) AssemblyInfoOf(string dll)
    {
        try
        {
            AssemblyName name = AssemblyName.GetAssemblyName(dll);
            return (name.Name ?? "", name.Version?.ToString(3) ?? "");
        }
        catch { return ("", ""); }
    }

    public static bool IsModViewer(ModEntry entry) =>
        string.Equals(entry.Assembly, "ModViewer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entry.Name, "ModViewer", StringComparison.OrdinalIgnoreCase);

    public static bool SetEnabled(ModEntry entry, bool enabled, out string error)
    {
        error = "";
        if (!enabled && IsModViewer(entry))
        {
            error = "Mod Viewer is part of the loader and cannot be disabled.";
            return false;
        }

        string root = enabled ? ModsDir : DisabledDir;
        if (string.IsNullOrEmpty(root)) { error = "Mod directory is unknown."; return false; }

        Directory.CreateDirectory(root);
        string target = Path.Combine(root, Path.GetFileName(entry.Path));
        if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(entry.Path), StringComparison.OrdinalIgnoreCase))
            return true;
        if (File.Exists(target) || Directory.Exists(target))
            target += "-" + DateTime.Now.ToString("HHmmss");

        try
        {
            if (Directory.Exists(entry.Path)) Directory.Move(entry.Path, target);
            else File.Move(entry.Path, target);
            entry.Path = target;
            entry.Enabled = enabled;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
