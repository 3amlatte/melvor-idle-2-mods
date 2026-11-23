using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MelvorModLoader;

namespace ModViewer;

/// <summary>
/// Finds a mod's own window. Mods that register with the loader are opened through
/// <see cref="ModWindow.Press"/>; mods with a hand-rolled panel (Astrology, for example) are
/// reached through their loaded mod instance's toggle method.
/// </summary>
internal static class UiAccess
{
    static readonly string[] ToggleNames =
    {
        "TogglePanel", "ToggleWindow", "ToggleMenu", "ToggleUI",
        "OpenWindow", "ShowWindow", "OpenPanel", "ShowPanel", "Toggle"
    };

    public static bool CanOpen(ModEntry entry)
    {
        if (IsSelf(entry)) return false;
        if (entry.HasSettings) return true;
        if (!entry.Enabled) return false;
        if (WindowFor(entry) != null) return true;
        return FindToggle(entry) != null;
    }

    public static bool TryOpenWindow(ModEntry entry, out string message)
    {
        message = "";

        if (entry.HasSettings || IsSelf(entry))
        {
            message = "";
            return false;
        }

        if (!entry.Enabled)
        {
            message = "Enable this mod and restart to open its window.";
            return false;
        }

        ModWindow? window = WindowFor(entry);
        if (window != null)
        {
            window.Press();
            return true;
        }

        object? instance = FindInstance(entry);
        MethodInfo? toggle = instance == null ? null : FindToggleMethod(instance.GetType());
        if (toggle == null)
        {
            message = "This mod has no window to open.";
            return false;
        }

        try
        {
            toggle.Invoke(instance, null);
            return true;
        }
        catch (Exception e)
        {
            message = "Could not open the mod window: " + (e.InnerException?.Message ?? e.Message);
            return false;
        }
    }

    static bool IsSelf(ModEntry entry) =>
        string.Equals(entry.Assembly, typeof(ModViewerMod).Assembly.GetName().Name,
            StringComparison.OrdinalIgnoreCase);

    static ModWindow? WindowFor(ModEntry entry)
    {
        string assembly = entry.Assembly.Length > 0 ? entry.Assembly : entry.Name;
        return Hotkeys.Windows.FirstOrDefault(w =>
            string.Equals(w.Mod, assembly, StringComparison.OrdinalIgnoreCase));
    }

    static object? FindInstance(ModEntry entry)
    {
        string assembly = entry.Assembly.Length > 0 ? entry.Assembly : entry.Name;
        string folder = FolderOf(entry);

        FieldInfo? field = typeof(Loader).GetField("s_frame", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is not IEnumerable handlers) return null;

        foreach (object item in handlers)
        {
            Type tuple = item.GetType();
            if (tuple.GetField("Item1")?.GetValue(item) is not ModContext owner) continue;
            if (!string.Equals(owner.Name, assembly, StringComparison.OrdinalIgnoreCase)) continue;
            if (!SameFolder(owner.Directory, folder)) continue;

            Action? handler = tuple.GetField("Item2")?.GetValue(item) as Action;
            object? target = handler?.Target;
            if (target == null) continue;
            if (FindToggleMethod(target.GetType()) != null) return target;

            object? nested = FindModField(target, assembly);
            if (nested != null) return nested;
        }

        return null;
    }

    static object? FindModField(object target, string assembly)
    {
        foreach (FieldInfo field in target.GetType().GetFields(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            object? value;
            try { value = field.GetValue(target); }
            catch { continue; }

            if (value == null) continue;
            if (!string.Equals(value.GetType().Assembly.GetName().Name, assembly,
                    StringComparison.OrdinalIgnoreCase)) continue;
            if (FindToggleMethod(value.GetType()) != null) return value;
        }

        return null;
    }

    static MethodInfo? FindToggle(ModEntry entry)
    {
        object? instance = FindInstance(entry);
        return instance == null ? null : FindToggleMethod(instance.GetType());
    }

    static MethodInfo? FindToggleMethod(Type type)
    {
        foreach (string name in ToggleNames)
        {
            MethodInfo? method = type.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(void)) return method;
        }

        return null;
    }

    static string FolderOf(ModEntry entry) =>
        Directory.Exists(entry.Path) ? entry.Path : Path.GetDirectoryName(entry.Path) ?? "";

    static bool SameFolder(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
