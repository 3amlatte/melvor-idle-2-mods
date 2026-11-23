using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ModViewer;

internal sealed class ModEntry
{
    public string Name = "";
    public string Assembly = "";
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
        var list = new List<ModEntry>();
        ScanDir(ModsDir, true, list);
        ScanDir(DisabledDir, false, list);
        return list
            .OrderByDescending(e => e.Is3am)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static void ScanDir(string dir, bool enabled, List<ModEntry> list)
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

            list.Add(new ModEntry
            {
                Name = Path.GetFileNameWithoutExtension(dll),
                Assembly = info.Name,
                Path = dll,
                Dll = dll,
                Enabled = enabled,
                HasSettings = File.Exists(Path.Combine(dir, "settings.json")),
                Version = info.Version
            });
        }
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

    public static bool SetEnabled(ModEntry entry, bool enabled, out string error)
    {
        error = "";
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
