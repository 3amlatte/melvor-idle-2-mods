using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GamesByMalcs.Melvor2.Saves;
using Godot;
using MelvorModLoader;

namespace BackupSaver;

/// <summary>
/// Copies the game's save files into the loader support folder after each local save
/// and keeps the five newest dated copies.
/// </summary>
public sealed class BackupSaverMod : IMod
{
    const int Keep = 5;
    const string NativeBackupDir = "save_backups";
    const string OldModBackupDir = "mod_save_backups";

    static readonly object Gate = new();
    static string s_backupRoot = "";

    public void OnLoad(ModContext context)
    {
        s_backupRoot = BackupRoot();
        Directory.CreateDirectory(s_backupRoot);

        Backup(context, "startup");
        SaveManager.LocalSaved += (_, _) => Backup(context, "save");

        context.Log($"Backup Saver: keeping the {Keep} newest backups in {s_backupRoot}");
    }

    static string BackupRoot()
    {
        if (OperatingSystem.IsMacOS())
        {
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
            if (string.IsNullOrWhiteSpace(home))
                home = OS.GetUserDataDir();
            return Path.Combine(home, "Library", "Application Support", "MelvorModLoader", "backups");
        }

        string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = System.Environment.GetEnvironmentVariable("APPDATA") ?? OS.GetUserDataDir();
        return Path.Combine(appData, "MelvorModLoader", "backups");
    }

    static void Backup(ModContext context, string reason)
    {
        lock (Gate)
        {
            try
            {
                Run(context, reason);
            }
            catch (Exception error)
            {
                context.Log($"Backup Saver failed ({reason}): {error.Message}");
            }
        }
    }

    static void Run(ModContext context, string reason)
    {
        string userDir = OS.GetUserDataDir();
        if (!Directory.Exists(userDir))
        {
            if (reason == "startup") context.Log("Backup Saver: the game's save folder does not exist yet.");
            return;
        }

        List<string> saves = Directory.EnumerateFiles(userDir, "*.sav", SearchOption.AllDirectories)
            .Where(path => !IsExcluded(userDir, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (saves.Count == 0)
        {
            if (reason == "startup") context.Log("Backup Saver: there are no save files to back up yet.");
            return;
        }

        List<string> existing = Directory.Exists(s_backupRoot)
            ? Directory.GetDirectories(s_backupRoot).OrderBy(path => path, StringComparer.Ordinal).ToList()
            : new List<string>();

        string? latest = existing.Count > 0 ? existing[^1] : null;
        if (latest != null && SameAs(latest, userDir, saves))
        {
            if (reason == "startup")
                context.Log($"Backup Saver: saves are unchanged since {Path.GetFileName(latest)}.");
            return;
        }

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string destination = Path.Combine(s_backupRoot, stamp);
        for (int suffix = 2; Directory.Exists(destination); suffix++)
            destination = Path.Combine(s_backupRoot, $"{stamp}-{suffix}");

        foreach (string save in saves)
        {
            string relative = Path.GetRelativePath(userDir, save);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(save, target, overwrite: true);
        }

        existing.Add(destination);
        foreach (string old in existing.Take(Math.Max(0, existing.Count - Keep)))
            Directory.Delete(old, recursive: true);

        context.Log($"Backup Saver: {saves.Count} save file(s) copied to {destination} ({reason}).");
    }

    static bool IsExcluded(string userDir, string path)
    {
        string relative = Path.GetRelativePath(userDir, path);
        string[] parts = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Any(part =>
            part.Equals(NativeBackupDir, StringComparison.OrdinalIgnoreCase) ||
            part.Equals(OldModBackupDir, StringComparison.OrdinalIgnoreCase));
    }

    static bool SameAs(string backupDir, string userDir, List<string> saves)
    {
        foreach (string save in saves)
        {
            var copy = new FileInfo(Path.Combine(backupDir, Path.GetRelativePath(userDir, save)));
            var live = new FileInfo(save);
            if (!copy.Exists || copy.Length != live.Length || copy.LastWriteTimeUtc != live.LastWriteTimeUtc)
                return false;
        }

        return true;
    }
}
