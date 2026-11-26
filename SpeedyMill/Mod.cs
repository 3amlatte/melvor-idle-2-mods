using System;
using System.Collections.Generic;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatEffects;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Farming;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;
using MelvorModLoader;

namespace SpeedyMill;

/// <summary>
/// Runs the grain mill's production timer three times as fast by cutting its interval to one
/// third. The multiplier is a normal game modifier, so the mill screen shows the shorter time
/// and the timer keeps the new speed across recipes, reloads and offline progress.
/// </summary>
public sealed class SpeedyMillMod : IMod
{
    sealed class Source(string name) : INamedObject
    {
        public ILocalizedString DisplayName { get; } = new LocalizedString(name);
    }

    static readonly Source SpeedSource = new("Speedy Mill: 200% faster mill");

    // 200% faster production means the interval is one third of normal (3x throughput).
    const double IntervalMultiplier = 1.0 / 3.0;

    ModContext _ctx = null!;
    Game? _game;
    ModifierSystem? _mods;
    ModifierValues? _values;
    FarmingGrainMillComponent? _mill;
    string _loggedRecipeId = "";
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded. Grain mill production runs 200% faster (3x).");
    }

    void OnNodeAdded(Node node)
    {
        // Bind before the load-time offline calculation so a saved mill already uses the
        // shorter interval when it next restarts.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryBind();
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryBind();
        LogInterval();
    }

    void TryBind()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _game)) return;

        Detach();
        _game = game;

        if (!game.Entities.TryGetObjectById("MelvorBase:Farming", out GameEntity? entity) ||
            entity is not FarmingEntity farming)
        {
            _ctx.Log("The Farming entity was not found; the mill speed bonus is off for this character.");
            return;
        }

        _mill = farming.GrainMill;
        _mods = Accessors.ModifiersOf(game);

        try
        {
            _values = BuildValues(game);
            _mods.AddOrUpdate(SpeedSource, _values, IntervalMultiplier);
            _ctx.Log($"Grain mill speed applied: interval x{IntervalMultiplier:0.###} (base {_mill.BaseGrainMillInterval:N0} ms).");
        }
        catch (Exception e)
        {
            _values = null;
            _ctx.Log($"Speedy Mill could not apply the mill modifier: {e.GetType().Name}: {e.Message}");
        }
    }

    void LogInterval()
    {
        if (_mill?.SelectedRecipe is not { } selected) return;
        if (selected.Recipe.Id == _loggedRecipeId) return;

        _loggedRecipeId = selected.Recipe.Id;
        try
        {
            _ctx.Log($"Grain mill interval for {selected.Recipe.DisplayName.Text}: " +
                     $"{selected.Stats.Interval.Value:N0} ms (base {_mill.BaseGrainMillInterval:N0} ms).");
        }
        catch
        {
            // Stat is not ready yet; try again for the next recipe change.
            _loggedRecipeId = "";
        }
    }

    void Detach()
    {
        if (_values != null)
        {
            try { _mods?.Remove(SpeedSource); } catch { /* old game */ }
        }

        _values = null;
        _mods = null;
        _mill = null;
        _game = null;
        _loggedRecipeId = "";
    }

    static ModifierValues BuildValues(Game game)
    {
        // Component-only context: every recipe of the Farming grain mill.
        const string context =
            "{\"Component\": {\"Entity\": \"MelvorBase:Farming\", \"Component\": \"GrainMill\"}}";

        string json =
            "{\"Stat\": {\"StatsType\": \"MelvorBase:GrainMill\", \"Name\": \"PercentInterval\"}, " +
            "\"Value\": 1, \"LocalContext\": " + context + ", " +
            "\"FromContext\": null, \"Condition\": null, \"Scaling\": null}";

        var value = new ComponentModifierValue(game.Data, JsonDocument.Parse(json).RootElement);
        return new ModifierValues(
            new List<GlobalModifierValue>(),
            new List<ComponentModifierValue> { value },
            new List<CombatEffectModifierValue>());
    }
}
