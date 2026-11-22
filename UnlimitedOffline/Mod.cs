using System;
using System.Collections.Generic;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatEffects;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;
using MelvorModLoader;

namespace UnlimitedOffline;

/// <summary>
/// Removes the 24-hour offline progress cap by adding a very large FlatOfflineTime bonus to the
/// game's Offline Progress component. The cap is recomputed from that stat every time offline
/// progress runs, so the bonus is active for load, sleep/wake and the debug menu alike.
/// </summary>
public sealed class UnlimitedOfflineMod : IMod
{
    // 100 years in milliseconds. Doubles handle this exactly enough, and it is far beyond any
    // real absence while keeping the progress bar and summary sensible.
    const double UnlimitedMs = 100.0 * 365.25 * 24 * 60 * 60 * 1000;

    sealed class Source(string name) : INamedObject
    {
        public ILocalizedString DisplayName { get; } = new LocalizedString(name);
    }

    static readonly Source BonusSource = new("Unlimited Offline");

    ModContext _ctx = null!;
    Game? _applied;
    int _frame;

    static ModifierValues? s_values;
    static Game? s_valuesFor;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        // The game view is added before the load-time offline calculation starts, so the
        // bonus is in place before the cap is read.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryApply();
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryApply();
    }

    void TryApply()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _applied)) return;

        _applied = game;
        try
        {
            ModifierSystem modifiers = Accessors.ModifiersOf(game);
            modifiers.AddOrUpdate(BonusSource, Values(game), UnlimitedMs);

            double hours = game.OfflineProgress.Component.MaxOfflineTime / 3_600_000.0;
            _ctx.Log($"Offline cap removed for '{game.CharacterName}': {hours:N0} hours available.");
        }
        catch (Exception e)
        {
            _ctx.Log($"Could not raise the offline cap: {e.GetType().Name}: {e.Message}");
        }
    }

    static ModifierValues Values(Game game)
    {
        if (s_values != null && ReferenceEquals(s_valuesFor, game)) return s_values;

        // "Value": 1 millisecond, multiplied by the duration below.
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
