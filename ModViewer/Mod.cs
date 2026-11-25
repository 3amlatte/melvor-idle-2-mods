using System;
using System.Diagnostics;
using System.IO;
using Godot;
using MelvorModLoader;

namespace ModViewer;

/// <summary>
/// F2 opens a list of every installed mod (mods and mods-disabled). The Enabled checkbox moves
/// a mod folder/file out of or back into the loader's path; a restart applies it.
/// Our own mods are grouped by a "3am.mod" marker file in their folder.
/// </summary>
public sealed class ModViewerMod : IMod
{
    ModContext _ctx = null!;
    ViewerWindow _window = null!;
    bool _fallbackF2;
    bool _wasF2;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;

        Mods.ModsDir = Directory.GetParent(ctx.Directory)?.FullName ?? "";
        Mods.DisabledDir = Path.Combine(Directory.GetParent(Mods.ModsDir)?.FullName ?? "", "mods-disabled");

        _window = new ViewerWindow(ctx, Restart, ctx.Log);

        try
        {
            ctx.AddWindow("Mod Viewer", Toggle, Key.F2);
        }
        catch (Exception e)
        {
            _fallbackF2 = true;
            ctx.Log($"Loader window registration failed ({e.GetType().Name}); polling F2 directly.");
        }

        ctx.Frame += OnFrame;
        ctx.Log("Loaded. Press F2 to open Mod Viewer.");
    }

    void OnFrame()
    {
        if (_fallbackF2)
        {
            bool down = Input.IsKeyPressed(Key.F2);
            if (down && !_wasF2) Toggle();
            _wasF2 = down;
        }

        _frame++;
    }

    void Toggle()
    {
        _window.Toggle();
    }

    void Restart()
    {
        try
        {
            // The old two-second timeout relaunched Steam before Melvor had finished closing.
            // Hand the wait to a detached PowerShell helper that watches this exact process
            // and starts Steam only after it has exited.
            int pid = Process.GetCurrentProcess().Id;
            string script = Path.Combine(Path.GetTempPath(), "3am-modviewer-restart.ps1");
            File.WriteAllText(script,
                "param([int]$GamePid)\n" +
                "try { Wait-Process -Id $GamePid -Timeout 300 -ErrorAction SilentlyContinue } catch { }\n" +
                "Start-Sleep -Seconds 4\n" +
                "Start-Process 'steam://rungameid/3218350'\n");

            OS.CreateProcess("powershell.exe", new[]
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-WindowStyle", "Hidden",
                "-File", script,
                "-GamePid", pid.ToString(),
            }, false);

            _ctx.Log($"Restarting Melvor Idle 2 after process {pid} exits...");
            _ctx.Tree.Quit();
        }
        catch (Exception e)
        {
            _ctx.Log("Restart failed: " + e.Message);
        }
    }
}
