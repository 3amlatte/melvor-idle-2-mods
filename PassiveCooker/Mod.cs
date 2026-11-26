using System;
using System.Linq;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Cooking;
using Godot;
using MelvorModLoader;

namespace PassiveCooker;

/// <summary>
/// Runs the cooking recipe selected on the Cooking page as a passive action: its timer keeps
/// ticking while the character trains another skill and during offline progress. Only recipe
/// bases and basic food are cooked; selecting any other cooking category stops the passive
/// action.
/// </summary>
public sealed class PassiveCookerMod : IMod
{
    ModContext _ctx = null!;
    Game? _game;
    CookingEntity? _cooking;
    CookingActionComponent? _action;
    string _waitingRecipeId = "";
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        // Catch the game view before the load-time offline calculation starts, so a passive
        // cooking action saved with the character is running for that calculation too.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryBind();
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryBind();
        Update();
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

        _ctx.Log("Cooking categories: " + string.Join(", ",
            cooking.Action.ActiveCategories.Keys.Select(c => $"{c.Id} ({c.DisplayName.Text})")));
        Update();
    }

    void Update()
    {
        if (_cooking == null || _action == null) return;

        ActiveCookingRecipe? selected = _action.SelectedRecipe;
        bool cookable = selected != null && IsBasicCooking(selected.Recipe);

        if (!cookable)
        {
            if (!_action.IsStarted && _action.CreatingRecipe != null)
            {
                string name = _action.CreatingRecipe.Recipe.DisplayName.Text;
                _action.TryStop(force: true);
                _ctx.Log($"Passive cooking stopped: {name} is not a recipe base or basic food.");
            }
            return;
        }

        if (_action.IsStarted) return;   // the player is actively cooking: the game owns the action

        if (_action.CreatingRecipe == null)
        {
            if (!CanStartPassive(selected!)) return;
            if (Cook.StartCreating(_action, selected!))
            {
                _waitingRecipeId = "";
                _ctx.Log($"Passive cooking started: {selected!.Recipe.DisplayName.Text} " +
                         $"({selected.Recipe.Category.DisplayName.Text}).");
            }
        }
        else if (!ReferenceEquals(_action.CreatingRecipe, selected))
        {
            _action.TryStop(force: true);
            if (!CanStartPassive(selected!)) return;
            if (Cook.StartCreating(_action, selected!))
            {
                _waitingRecipeId = "";
                _ctx.Log($"Passive cooking switched to {selected!.Recipe.DisplayName.Text} " +
                         $"({selected.Recipe.Category.DisplayName.Text}).");
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

    void Detach()
    {
        _cooking = null;
        _action = null;
        _game = null;
        _waitingRecipeId = "";
    }

    internal static bool IsBasicCooking(CookingRecipe recipe)
    {
        string key = (recipe.Category.Id + " " + recipe.Category.DisplayName.Text).ToLowerInvariant();
        return key.Contains("basicrecipes") || key.Contains("basic food") ||
               key.Contains("recipebases") || key.Contains("recipe bases");
    }
}
