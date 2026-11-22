using System;
using System.Collections.Generic;
using System.Linq;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Core;
using GamesByMalcs.Melvor2.Data.Items;
using GamesByMalcs.Melvor2.Data.Recipes;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Farming;
using Godot;
using MelvorModLoader;

namespace SeedReturn;

/// <summary>
/// Every successful harvest rolls for seeds of the crop that was planted. The chance scales
/// from 1% at Farming 1 to 20% at Farming 99 (see <see cref="Chance"/>). The number of seeds
/// returned depends on the crop category: allotment 3, herb 2, tree/flower/orchard 1,
/// mill farm 2, compost 0.
/// </summary>
public sealed class SeedReturnMod : IMod
{
    ModContext _ctx = null!;
    MilestonesSection _section = null!;
    Game? _game;
    FarmingEntity? _farming;
    FarmingPassiveComponent? _passive;
    EventHandler<ValueChangedEventArgs<IRecipeCategory?>>? _categoryChanged;
    int _frame;
    readonly HashSet<string> _unknownCategories = new();

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _section = new MilestonesSection(ctx);
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        // Catch the game view before offline progress is computed, so seeds are returned
        // for harvests that happen during the offline calculation too.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryAttach();

        // The Milestones window is created once and rebound per skill; track it so the
        // Seed Return section can be shown when Farming is the skill on display.
        if (node is MilestonesView) _section.Track(node);
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryAttach();
        if (_farming != null) _section.Update(_farming);
    }

    void TryAttach()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _game)) return;

        Detach();
        _game = game;

        if (!game.Entities.TryGetObjectById("MelvorBase:Farming", out GameEntity? entity) || entity is not FarmingEntity farming)
        {
            _ctx.Log("The Farming entity was not found; seed returns are off for this character.");
            return;
        }

        _farming = farming;
        _passive = farming.Passive;
        _passive.RecipeHarvested += OnRecipeHarvested;

        // Choosing a real milestone category brings the list back from the Seed Return table.
        _categoryChanged = (_, _) => _section.ShowList();
        farming.Milestones.SelectedCategoryChanged += _categoryChanged;

        _ctx.Log("Farming categories: " + string.Join(", ",
            _passive.ActiveCategories.Keys.Select(c => $"{c.Id} ({c.DisplayName.Text})")));
        _ctx.Log($"Ready. Farming level {_passive.Experience.Level} -> {Chance.Percent(_passive.Experience.Level)}% seed return chance.");
    }

    void Detach()
    {
        if (_passive != null)
        {
            try { _passive.RecipeHarvested -= OnRecipeHarvested; }
            catch { /* the old game may already be disposed */ }
            _passive = null;
        }
        if (_farming != null && _categoryChanged != null)
        {
            try { _farming.Milestones.SelectedCategoryChanged -= _categoryChanged; }
            catch { /* the old game may already be disposed */ }
        }
        _categoryChanged = null;
        _farming = null;
        _game = null;
    }

    void OnRecipeHarvested(object? sender, FarmingRecipeHarvestedEventArgs e)
    {
        if (_passive == null) return;

        FarmingRecipe recipe = e.Recipe;
        int percent = Chance.Percent(_passive.Experience.Level);
        if (Random.Shared.Next(100) >= percent) return;

        int count = SeedCountFor(recipe.Category);
        if (count <= 0) return;

        // The seed is the first ingredient of a farming recipe.
        Item? seed = recipe.Ingredients.Items.FirstOrDefault()?.Item;
        if (seed == null)
        {
            _ctx.Log($"No seed ingredient found for {recipe.DisplayName.Text}; nothing returned.");
            return;
        }

        int added = _passive.Inventory.AddQuantity(seed, count);
        if (added > 0)
        {
            _ctx.Log($"+{added} {seed.DisplayName.GetPluralText(added)} from {recipe.DisplayName.Text} " +
                     $"({recipe.Category.DisplayName.Text}, {percent}% chance).");
        }
    }

    int SeedCountFor(FarmingCategory category)
    {
        string key = (category.Id + " " + category.DisplayName.Text).ToLowerInvariant();

        if (key.Contains("compost")) return 0;
        if (key.Contains("allotment")) return 3;
        if (key.Contains("herb")) return 2;
        if (key.Contains("mill") || key.Contains("grain")) return 2;
        if (key.Contains("tree") || key.Contains("orchard") || key.Contains("flower")) return 1;

        if (_unknownCategories.Add(category.Id))
            _ctx.Log($"Unknown farming category '{category.Id}' ({category.DisplayName.Text}); no seeds returned for it yet.");
        return 0;
    }
}
