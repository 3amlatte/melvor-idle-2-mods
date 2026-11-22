using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.ArtisanAction;
using GamesByMalcs.Melvor2.Components.CombatEffects;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Core;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Recipes;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Smithing;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;
using MelvorModLoader;

namespace PassiveSmelting;

/// <summary>
/// Runs the smelting recipe selected on the Smithing page as a passive action: its timer keeps
/// ticking while the character trains another skill and during offline progress. Above level 50
/// the interval is shortened so it produces 2 bars per normal cycle, and above 85, 3 bars.
/// </summary>
public sealed class PassiveSmeltingMod : IMod
{
    sealed class Source(string name) : INamedObject
    {
        public ILocalizedString DisplayName { get; } = new LocalizedString(name);
    }

    static readonly Source SpeedSource = new("Passive Smelting: furnace speed");

    ModContext _ctx = null!;
    PassiveSmeltingSection _section = null!;
    Game? _game;
    SmithingEntity? _smithing;
    ArtisanActionComponent? _action;
    ModifierSystem? _mods;
    EventHandler<ValueChangedEventArgs<IRecipeCategory?>>? _categoryChanged;
    int _frame;

    ModifierValues? _speedValues;
    string _speedRecipeId = "";
    int _speedBars;
    string _waitingRecipeId = "";

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _section = new PassiveSmeltingSection(ctx);
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        // Catch the game view before the load-time offline calculation starts, so a passive
        // smelting action saved with the character is running for that calculation too.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryBind();
        if (node is MilestonesView) _section.Track(node);
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryBind();
        Update();
        if (_smithing != null)
            _section.Update(_smithing, _action?.SelectedRecipe, Tiers.BarsAt(_smithing.Experience.Level));
    }

    void TryBind()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _game)) return;

        Detach();
        _game = game;

        if (!game.Entities.TryGetObjectById("MelvorBase:Smithing", out GameEntity? entity) || entity is not SmithingEntity smithing)
        {
            _ctx.Log("The Smithing entity was not found; passive smelting is off for this character.");
            return;
        }

        _smithing = smithing;
        _action = smithing.Action;
        _mods = Accessors.ModifiersOf(game);

        _categoryChanged = (_, _) => _section.ShowList();
        smithing.Milestones.SelectedCategoryChanged += _categoryChanged;

        _ctx.Log("Smithing categories: " + string.Join(", ",
            smithing.Action.ActiveCategories.Keys.Select(c => $"{c.Id} ({c.DisplayName.Text})")));
        Update();
    }

    void Update()
    {
        if (_smithing == null || _action == null || _mods == null) return;

        int level = _smithing.Experience.Level;
        int bars = Tiers.BarsAt(level);
        ActiveArtisanRecipe? selected = _action.SelectedRecipe;
        bool smelting = selected != null && IsSmelting(selected.Recipe);

        if (!smelting)
        {
            RemoveSpeed();
            if (!_action.IsStarted && _action.CreatingRecipe != null)
            {
                string name = _action.CreatingRecipe.Recipe.DisplayName.Text;
                _action.TryStop(force: true);
                _ctx.Log($"Passive smelting stopped: {name} is not in a smelting category.");
            }
            return;
        }

        ApplySpeed(selected!, bars);

        if (_action.IsStarted) return;   // the player is actively smithing: the game owns the action

        if (_action.CreatingRecipe == null)
        {
            if (!CanStartPassive(selected!)) return;
            if (Artisan.StartCreating(_action, selected!))
            {
                _waitingRecipeId = "";
                _ctx.Log($"Passive smelting started: {selected!.Recipe.DisplayName.Text} " +
                         $"({selected.Recipe.Category.DisplayName.Text}), {Tiers.Describe(bars)}.");
            }
        }
        else if (!ReferenceEquals(_action.CreatingRecipe, selected))
        {
            _action.TryStop(force: true);
            if (!CanStartPassive(selected!)) return;
            if (Artisan.StartCreating(_action, selected!))
            {
                _waitingRecipeId = "";
                _ctx.Log($"Passive smelting switched to {selected!.Recipe.DisplayName.Text}, {Tiers.Describe(bars)}.");
            }
        }
    }

    bool CanStartPassive(ActiveArtisanRecipe selected)
    {
        if (!selected.Recipe.Requirements.AreMet)
        {
            if (_waitingRecipeId != selected.Recipe.Id)
            {
                _waitingRecipeId = selected.Recipe.Id;
                _ctx.Log($"Passive smelting waiting: {selected.Recipe.DisplayName.Text} requirements are not met.");
            }
            return false;
        }

        if (!_action!.CanCreateRecipe(selected))
        {
            if (_waitingRecipeId != selected.Recipe.Id)
            {
                _waitingRecipeId = selected.Recipe.Id;
                _ctx.Log($"Passive smelting waiting: not enough materials or bank space for {selected.Recipe.DisplayName.Text}.");
            }
            return false;
        }

        return true;
    }

    void ApplySpeed(ActiveArtisanRecipe selected, int bars)
    {
        if (bars <= 1)
        {
            RemoveSpeed();
            return;
        }

        string recipeId = selected.Recipe.Id;
        if (_speedValues == null || _speedRecipeId != recipeId)
        {
            RemoveSpeed();
            _speedValues = BuildValues(_game!, selected.Recipe);
            _speedRecipeId = recipeId;
            _speedBars = 0;
        }

        double multiplier = Tiers.IntervalPercent(bars);
        _mods!.AddOrUpdate(SpeedSource, _speedValues, multiplier);

        if (_speedBars != bars)
        {
            _speedBars = bars;
            _ctx.Log($"Passive smelting speed: {Tiers.Describe(bars)} for {selected.Recipe.DisplayName.Text} " +
                     $"(interval {selected.Stats.Interval.Value:0} ms, base {selected.Recipe.BaseInterval} ms).");
        }
    }

    void RemoveSpeed()
    {
        if (_speedValues == null) return;
        try { _mods?.Remove(SpeedSource); } catch { /* old game */ }
        _speedValues = null;
        _speedRecipeId = "";
        _speedBars = 0;
    }

    void Detach()
    {
        RemoveSpeed();
        if (_smithing != null && _categoryChanged != null)
        {
            try { _smithing.Milestones.SelectedCategoryChanged -= _categoryChanged; }
            catch { /* old game */ }
        }
        _categoryChanged = null;
        _smithing = null;
        _action = null;
        _mods = null;
        _game = null;
    }

    internal static bool IsSmelting(ArtisanRecipe recipe)
    {
        string key = (recipe.Category.Id + " " + recipe.Category.DisplayName.Text).ToLowerInvariant();
        return key.Contains("bar") || key.Contains("smelt");
    }

    static ModifierValues BuildValues(Game game, ArtisanRecipe recipe)
    {
        string context =
            $"{{\"Component\": {{\"Entity\": \"MelvorBase:Smithing\", \"Component\": \"Action\"}}, " +
            $"\"Category\": \"{recipe.Category.Id}\", \"Recipe\": \"{recipe.Id}\"" +
            (recipe.Subcategory != null ? $", \"Subcategory\": \"{recipe.Subcategory.Id}\"" : "") +
            "}";

        string json =
            "{\"Stat\": {\"StatsType\": \"MelvorBase:ArtisanAction\", \"Name\": \"PercentInterval\"}, " +
            "\"Value\": 1, \"LocalContext\": " + context + ", " +
            "\"FromContext\": null, \"Condition\": null, \"Scaling\": null}";

        var value = new ComponentModifierValue(game.Data, JsonDocument.Parse(json).RootElement);
        return new ModifierValues(
            new List<GlobalModifierValue>(),
            new List<ComponentModifierValue> { value },
            new List<CombatEffectModifierValue>());
    }
}
