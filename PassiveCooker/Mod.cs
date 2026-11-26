using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatEffects;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Core;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Recipes;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Cooking;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;
using MelvorModLoader;

namespace PassiveCooker;

/// <summary>
/// Runs the cooking recipe selected on the Cooking page as a passive action: its timer keeps
/// ticking while the character trains another skill and during offline progress. Only recipe
/// bases and basic food are cooked; selecting any other cooking category stops the passive
/// action. Like Passive Smelting, the interval shortens with level: 2x from 51 and 3x from 86.
/// </summary>
public sealed class PassiveCookerMod : IMod
{
    sealed class Source(string name) : INamedObject
    {
        public ILocalizedString DisplayName { get; } = new LocalizedString(name);
    }

    static readonly Source SpeedSource = new("Passive Cooker: cooking speed");

    ModContext _ctx = null!;
    PassiveCookerSection _section = null!;
    Game? _game;
    CookingEntity? _cooking;
    CookingActionComponent? _action;
    ModifierSystem? _mods;
    EventHandler<ValueChangedEventArgs<IRecipeCategory?>>? _categoryChanged;
    string _waitingRecipeId = "";
    int _frame;

    ModifierValues? _speedValues;
    string _speedRecipeId = "";
    int _speedPortions;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _section = new PassiveCookerSection(ctx);
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        // Catch the game view before the load-time offline calculation starts, so a passive
        // cooking action saved with the character is running for that calculation too.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryBind();
        if (node is MilestonesView) _section.Track(node);
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryBind();
        Update();
        if (_cooking != null)
            _section.Update(_cooking, _action?.SelectedRecipe, Tiers.PortionsAt(_cooking.Experience.Level));
    }

    void TryBind()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _game)) return;

        Detach();
        _game = game;

        if (!game.Entities.TryGetObjectById("MelvorBase:Cooking", out GameEntity? entity) ||
            entity is not CookingEntity cooking)
        {
            _ctx.Log("The Cooking entity was not found; passive cooking is off for this character.");
            return;
        }

        _cooking = cooking;
        _action = cooking.Action;
        _mods = Accessors.ModifiersOf(game);

        _categoryChanged = (_, _) => _section.ShowList();
        cooking.Milestones.SelectedCategoryChanged += _categoryChanged;

        _ctx.Log("Cooking categories: " + string.Join(", ",
            cooking.Action.ActiveCategories.Keys.Select(c => $"{c.Id} ({c.DisplayName.Text})")));
        Update();
    }

    void Update()
    {
        if (_cooking == null || _action == null) return;

        int level = _cooking.Experience.Level;
        int portions = Tiers.PortionsAt(level);
        ActiveCookingRecipe? selected = _action.SelectedRecipe;
        bool cookable = selected != null && IsBasicCooking(selected.Recipe);

        if (!cookable)
        {
            RemoveSpeed();
            if (!_action.IsStarted && _action.CreatingRecipe != null)
            {
                string name = _action.CreatingRecipe.Recipe.DisplayName.Text;
                _action.TryStop(force: true);
                _ctx.Log($"Passive cooking stopped: {name} is not a recipe base or basic food.");
            }
            return;
        }

        ApplySpeed(selected!, portions);

        if (_action.IsStarted) return;   // the player is actively cooking: the game owns the action

        if (_action.CreatingRecipe == null)
        {
            if (!CanStartPassive(selected!)) return;
            if (Cook.StartCreating(_action, selected!))
            {
                _waitingRecipeId = "";
                _ctx.Log($"Passive cooking started: {selected!.Recipe.DisplayName.Text} " +
                         $"({selected.Recipe.Category.DisplayName.Text}), {Tiers.Describe(portions)}.");
            }
        }
        else if (!ReferenceEquals(_action.CreatingRecipe, selected))
        {
            _action.TryStop(force: true);
            if (!CanStartPassive(selected!)) return;
            if (Cook.StartCreating(_action, selected!))
            {
                _waitingRecipeId = "";
                _ctx.Log($"Passive cooking switched to {selected!.Recipe.DisplayName.Text}, {Tiers.Describe(portions)}.");
            }
        }
    }

    bool CanStartPassive(ActiveCookingRecipe selected)
    {
        if (!selected.Recipe.Requirements.AreMet)
        {
            if (_waitingRecipeId != selected.Recipe.Id)
            {
                _waitingRecipeId = selected.Recipe.Id;
                _ctx.Log($"Passive cooking waiting: {selected.Recipe.DisplayName.Text} requirements are not met.");
            }
            return false;
        }

        if (!_action!.CanCreateRecipe(selected))
        {
            if (_waitingRecipeId != selected.Recipe.Id)
            {
                _waitingRecipeId = selected.Recipe.Id;
                _ctx.Log($"Passive cooking waiting: not enough materials or bank space for {selected.Recipe.DisplayName.Text}.");
            }
            return false;
        }

        return true;
    }

    void ApplySpeed(ActiveCookingRecipe selected, int portions)
    {
        if (portions <= 1)
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
            _speedPortions = 0;
        }

        double multiplier = Tiers.IntervalPercent(portions);
        _mods!.AddOrUpdate(SpeedSource, _speedValues, multiplier);

        if (_speedPortions != portions)
        {
            _speedPortions = portions;
            _ctx.Log($"Passive cooking speed: {Tiers.Describe(portions)} for {selected.Recipe.DisplayName.Text} " +
                     $"(interval {selected.Stats.Interval.Value:0} ms, base {selected.Recipe.BaseInterval} ms).");
        }
    }

    void RemoveSpeed()
    {
        if (_speedValues == null) return;
        try { _mods?.Remove(SpeedSource); } catch { /* old game */ }
        _speedValues = null;
        _speedRecipeId = "";
        _speedPortions = 0;
    }

    void Detach()
    {
        RemoveSpeed();
        if (_cooking != null && _categoryChanged != null)
        {
            try { _cooking.Milestones.SelectedCategoryChanged -= _categoryChanged; }
            catch { /* old game */ }
        }
        _categoryChanged = null;
        _cooking = null;
        _action = null;
        _mods = null;
        _game = null;
        _waitingRecipeId = "";
    }

    internal static bool IsBasicCooking(CookingRecipe recipe)
    {
        string key = (recipe.Category.Id + " " + recipe.Category.DisplayName.Text).ToLowerInvariant();
        return key.Contains("basicrecipes") || key.Contains("basic food") ||
               key.Contains("recipebases") || key.Contains("recipe bases");
    }

    static ModifierValues BuildValues(Game game, CookingRecipe recipe)
    {
        string context =
            $"{{\"Component\": {{\"Entity\": \"MelvorBase:Cooking\", \"Component\": \"Action\"}}, " +
            $"\"Category\": \"{recipe.Category.Id}\", \"Recipe\": \"{recipe.Id}\"}}";

        string json =
            "{\"Stat\": {\"StatsType\": \"MelvorBase:CookingAction\", \"Name\": \"PercentInterval\"}, " +
            "\"Value\": 1, \"LocalContext\": " + context + ", " +
            "\"FromContext\": null, \"Condition\": null, \"Scaling\": null}";

        var value = new ComponentModifierValue(game.Data, JsonDocument.Parse(json).RootElement);
        return new ModifierValues(
            new List<GlobalModifierValue>(),
            new List<ComponentModifierValue> { value },
            new List<CombatEffectModifierValue>());
    }
}
