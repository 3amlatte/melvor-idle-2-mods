using System;
using System.Collections.Generic;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Entities.Ranching;
using Godot;
using MelvorModLoader;

namespace BiggerPastures;

/// <summary>
/// Doubles RanchingEnclosure.AnimalLimit for every enclosure type in the game's data.
/// The limit is read live by the enclosure logic and the Ranching UI, so no game code is
/// patched; nothing is written to the save.
/// </summary>
public sealed class BiggerPasturesMod : IMod
{
    ModContext _ctx = null!;
    Game? _applied;
    int _frame;
    readonly HashSet<RanchingEnclosure> _seen = new();

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
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
            IReadOnlyDataRegistry<RanchingEnclosure> registry = game.Data.GetDataRegistryOutput<RanchingEnclosure>();
            int changed = 0;

            foreach (RanchingEnclosure enclosure in registry.GetAllObjects())
            {
                // Enclosure data objects can outlive one character; never double the same one twice.
                if (!_seen.Add(enclosure)) continue;

                int oldLimit = enclosure.AnimalLimit;
                if (oldLimit <= 0) continue;

                int newLimit = oldLimit * 2;
                Accessors.SetAnimalLimit(enclosure, newLimit);
                _ctx.Log($"{enclosure.DisplayName.Text}: {oldLimit} -> {newLimit} animals");
                changed++;
            }

            _ctx.Log($"Doubled the animal limit for {changed} enclosure type(s).");
        }
        catch (Exception e)
        {
            _ctx.Log($"Could not double the enclosure limits: {e.GetType().Name}: {e.Message}");
        }
    }
}
