using System;
using System.Collections.Generic;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatEffects;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using MelvorModLoader;

namespace TimeSkip;

/// <summary>
/// A window that skips the character forward by a chosen amount of time. It has no hotkey;
/// open it from Mod Viewer (F2) or the loader's Mods menu.
/// The skip goes through the game's own offline progress: it pauses, simulates the ticks,
/// and shows the normal offline summary. A temporary FlatOfflineTime bonus raises the cap
/// just long enough for the requested duration to be accepted, then it is removed.
/// </summary>
public sealed class TimeSkipMod : IMod
{
    sealed class Source(string name) : INamedObject
    {
        public ILocalizedString DisplayName { get; } = new LocalizedString(name);
    }

    static readonly Source CapSource = new("TimeSkip: skip cap");

    ModContext _ctx = null!;
    TimeSkipWindow _window = null!;
    int _frame;

    string _status = "Open a character first.";
    bool _capActive;
    ModifierSystem? _mods;
    Game? _game;
    double _skipHours;

    static ModifierValues? s_values;
    static Game? s_valuesFor;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _window = new TimeSkipWindow(ctx, Skip);

        try
        {
            // No default key: Mod Viewer (F2) and the loader's Mods menu open this window.
            ctx.AddWindow("TimeSkip", Toggle);
        }
        catch (Exception e)
        {
            ctx.Log($"Loader window registration failed ({e.GetType().Name}); TimeSkip will not have a window.");
        }

        ctx.Frame += OnFrame;
        ctx.Log("Loaded. Open TimeSkip from Mod Viewer (F2).");
    }

    void Toggle()
    {
        _window.Toggle();
        if (_window.Visible) UpdateWindow();
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second

        Game? now = GameAccess.CurrentGame;
        if (_game != null && !ReferenceEquals(_game, now))
        {
            // The old character is gone; its modifier values went with it.
            _capActive = false;
            _mods = null;
            _game = null;
        }

        if (_capActive && !GameAccess.IsComputingOffline)
        {
            RemoveCap();
            _status = $"Finished skipping {Duration.Format(_skipHours)}.";
        }

        if (_window.Visible) UpdateWindow();
    }

    void UpdateWindow()
    {
        Game? game = GameAccess.CurrentGame;
        bool loaded = game is { IsSaveLoaded: true };
        bool offline = GameAccess.IsComputingOffline;

        if (!loaded) _status = "Open a character first.";
        else if (offline) _status = "Skipping...";
        else if (!_capActive && _status.StartsWith("Open a character")) _status = "Ready.";

        _window.SetStatus(_status);
        _window.SetSkipEnabled(loaded && !offline && !_capActive);
    }

    void Skip(double hours)
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true })
        {
            _status = "Open a character first.";
            UpdateWindow();
            return;
        }

        if (_capActive || GameAccess.IsComputingOffline)
        {
            _status = "A skip is already running.";
            UpdateWindow();
            return;
        }

        try
        {
            _game = game;
            _mods = Accessors.ModifiersOf(game);

            // One extra hour of headroom over the requested skip.
            _mods.AddOrUpdate(CapSource, Values(game), (hours + 1) * 3_600_000);
            _capActive = true;
            _skipHours = hours;

            double capHours = game.OfflineProgress.Component.MaxOfflineTime / 3_600_000.0;
            _ctx.Log($"Skip {Duration.Format(hours)}: temporary offline cap is {capHours:N0} hours.");

            _status = $"Skipping {Duration.Format(hours)}...";
            _window.SetStatus(_status);
            _window.SetSkipEnabled(false);

            // The game copies MaxOfflineTime into its progress counter synchronously before
            // it starts the background tick loop, so the temporary cap does its job here.
            game.RequestOfflineHours(hours);

            if (!GameAccess.IsComputingOffline)
            {
                RemoveCap();
                _status = "Could not start the skip. Open a character and try again.";
                UpdateWindow();
            }
        }
        catch (Exception e)
        {
            RemoveCap();
            _status = $"Could not skip: {e.Message}";
            UpdateWindow();
        }
    }

    void RemoveCap()
    {
        if (!_capActive) return;
        if (GameAccess.IsComputingOffline) return;   // never touch the game during its offline thread

        try { _mods?.Remove(CapSource); }
        catch { /* the game may already be gone */ }

        _capActive = false;
        _mods = null;
    }

    static ModifierValues Values(Game game)
    {
        if (s_values != null && ReferenceEquals(s_valuesFor, game)) return s_values;

        const string json = """
            {
              "Stat": { "StatsType": "MelvorBase:OfflineProgress", "Name": "FlatOfflineTime" },
              "Value": 1,
              "LocalContext": { "Component": { "Entity": "MelvorBase:OfflineProgress", "Component": "Component" } },
              "FromContext": null,
              "Condition": null,
              "Scaling": null
            }
            """;

        var value = new ComponentModifierValue(game.Data, JsonDocument.Parse(json).RootElement);
        s_values = new ModifierValues(
            new List<GlobalModifierValue>(),
            new List<ComponentModifierValue> { value },
            new List<CombatEffectModifierValue>());
        s_valuesFor = game;
        return s_values;
    }
}
