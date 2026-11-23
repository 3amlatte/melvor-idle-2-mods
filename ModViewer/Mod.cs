using System;
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
            // Start Steam's launch URL after this process exits.
            string cmd = "timeout /t 2 /nobreak >nul & start \"\" \"steam://rungameid/3218350\"";
            OS.CreateProcess("cmd.exe", new[] { "/c", cmd }, false);
            _ctx.Log("Restarting Melvor Idle 2...");
            _ctx.Tree.Quit();
        }
        catch (Exception e)
        {
            _ctx.Log("Restart failed: " + e.Message);
        }
    }
}
