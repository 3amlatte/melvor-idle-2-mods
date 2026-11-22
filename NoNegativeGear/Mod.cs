using System;
using System.IO;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.GameMenus;
using Godot;
using MelvorModLoader;

namespace NoNegativeGear;

/// <summary>
/// Removes negative stats and bonuses from equipment (armour by default, weapons optionally).
/// Affects every character, every time one is opened; nothing is written to the save.
/// </summary>
public sealed class NoNegativeGearMod : IMod
{
    const string ModVersion = "0.1.0";

    sealed class Settings
    {
        /// <summary>Also strip negatives from weapons. Off by default: the point is armour.</summary>
        public bool IncludeWeapons { get; set; }
    }

    ModContext _ctx = null!;
    Settings _settings = new();
    Game? _applied;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _settings = LoadSettings(ctx);

        // GameView enters the tree before the game computes offline progress: catching it there
        // means already-equipped gear is fixed before the offline calculation reads its stats.
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;

        ctx.Log($"Loaded. Weapons: {(_settings.IncludeWeapons ? "included" : "left alone")}.");
    }

    void OnNodeAdded(Node node)
    {
        if (node is GameView) TryApply();
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second is plenty
        TryApply();
    }

    void TryApply()
    {
        if (GameAccess.IsComputingOffline) return;

        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _applied)) return;

        _applied = game;
        try
        {
            Clamp.Report report = Clamp.Apply(_ctx, game, _settings.IncludeWeapons);
            _ctx.Log($"Removed {report.Stats} negative stat(s) and {report.Bonuses} negative bonus(es) " +
                     $"on {report.Items} item(s) for '{game.CharacterName}' ({report.WeaponsSkipped} weapon(s) skipped).");
            WriteReport(game, report);
        }
        catch (Exception e)
        {
            _ctx.Log($"Could not remove negatives: {e.GetType().Name}: {e.Message}");
        }
    }

    void WriteReport(Game game, Clamp.Report report)
    {
        try
        {
            string file = Path.Combine(_ctx.Directory, "last-run.txt");
            using var writer = new StreamWriter(file);
            writer.WriteLine($"No Negative Gear {ModVersion} - {game.CharacterName} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"{report.Stats} negative stat(s), {report.Bonuses} negative bonus(es), " +
                             $"{report.Items} item(s), {report.WeaponsSkipped} weapon(s) skipped.");
            writer.WriteLine();
            foreach (string line in report.Changes) writer.WriteLine(line);
            if (report.Notes.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine("Left alone (review these):");
                foreach (string note in report.Notes) writer.WriteLine(note);
            }
        }
        catch (Exception e)
        {
            _ctx.Log($"Could not write last-run.txt: {e.Message}");
        }
    }

    static Settings LoadSettings(ModContext ctx)
    {
        string file = Path.Combine(ctx.Directory, "settings.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
        try
        {
            if (File.Exists(file))
            {
                Settings? loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file), options);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception e)
        {
            ctx.Log($"settings.json is not readable, using defaults: {e.Message}");
        }

        var settings = new Settings();
        try { File.WriteAllText(file, JsonSerializer.Serialize(settings, options)); }
        catch (Exception e) { ctx.Log($"Could not create settings.json: {e.Message}"); }
        return settings;
    }
}
