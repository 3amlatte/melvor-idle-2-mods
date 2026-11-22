using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatStats;
using GamesByMalcs.Melvor2.Components.Equipment;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Items;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Combat;
using GamesByMalcs.Melvor2.Modifiers;
using MelvorModLoader;

namespace NoNegativeGear;

/// <summary>
/// Removes negative stats and bonuses from equipment by editing the game's own item data.
///
/// Why the Remove/Add dance: the game keeps running totals of combat stats (ActiveCombatStat)
/// and of modifiers (ModifierEntry) that are computed when a value is added. Changing an item's
/// value afterwards leaves those totals stale, so each equipped item is taken out, changed, and
/// put back in. Items that are not equipped are only edited.
/// </summary>
internal static class Clamp
{
    public sealed record Report(int Items, int Stats, int Bonuses, int WeaponsSkipped, List<string> Changes, List<string> Notes);

    public static Report Apply(ModContext ctx, Game game, bool includeWeapons)
    {
        var changes = new List<string>();
        var notes = new List<string>();
        int itemsTouched = 0, statsChanged = 0, bonusesChanged = 0, weaponsSkipped = 0;

        CombatStatsComponent? playerStats = null;
        if (game.Entities.TryGetObjectById("MelvorBase:Combat", out GameEntity? entity) && entity is CombatEntity combat)
            playerStats = combat.PlayerCombatStats;

        ModifierSystem modifiers = Accessors.ModifiersOf(game);

        foreach (Item item in game.Data.GetDataRegistryOutput<Item>().GetAllObjects())
        {
            if (!item.Capabilities.TryGet<EquipmentCapability>(out EquipmentCapability? capability)) continue;

            if (!includeWeapons && item.Capabilities.Contains<WeaponCapability>())
            {
                weaponsSkipped++;
                continue;
            }

            bool badStats = capability.Stats.Stats.Any(v => v.Value < 0)
                         || capability.Stats.PowerRatings.Any(v => v.Value < 0)
                         || capability.Stats.ProtectionRatings.Any(v => v.Value < 0);

            // Kind is the game's own good/bad classification: for a stat where "lower is better"
            // (action interval), a negative value is reported as Positive and stays untouched.
            bool badBonuses = capability.Bonuses is { } b && b.Component.Any(v => v.Kind == ModifierKind.Negative);

            if (!badStats && !badBonuses) continue;

            itemsTouched++;
            string name = item.DisplayName.GetPluralText(1);

            if (badStats)
            {
                bool equipped = playerStats?.Remove(item, capability.Stats) == true;

                foreach (CombatStatValue value in capability.Stats.Stats)
                {
                    if (value.Value >= 0) continue;
                    changes.Add($"{name} | {value.Stat.DisplayName.Text} | {value.Value:0.##} -> 0");
                    Accessors.SetValue(value, 0);
                    statsChanged++;
                }

                foreach (DamageTypeStatValue value in capability.Stats.PowerRatings)
                {
                    if (value.Value >= 0) continue;
                    changes.Add($"{name} | {value.DamageType.DisplayName.Text} power | {value.Value} -> 0");
                    Accessors.SetValue(value, 0);
                    statsChanged++;
                }

                foreach (DamageTypeStatValue value in capability.Stats.ProtectionRatings)
                {
                    if (value.Value >= 0) continue;
                    changes.Add($"{name} | {value.DamageType.ResistanceName.Text} | {value.Value} -> 0");
                    Accessors.SetValue(value, 0);
                    statsChanged++;
                }

                if (equipped) playerStats!.Add(item, capability.Stats);
            }

            if (badBonuses)
            {
                ModifierValues bonuses = capability.Bonuses!;
                bool equipped = modifiers.Remove(item, bonuses);

                foreach (ComponentModifierValue value in bonuses.Component)
                {
                    if (value.Kind != ModifierKind.Negative) continue;
                    changes.Add($"{name} | bonus {value.Stat.Name} | {value.Value:0.##} -> 0");
                    Accessors.ValueOf(value) = 0;
                    bonusesChanged++;
                }

                // Global bonuses are deliberately left alone for now: some global stats are
                // "lower is better", so a sign check alone can remove a benefit. Log them so the
                // list can be reviewed before deciding.
                foreach (GlobalModifierValue value in bonuses.Global)
                {
                    if (value.Value < 0)
                        notes.Add($"left global negative: {name} | {value.StatGroup} | {value.Value:0.##}");
                }

                if (equipped) modifiers.Add(item, bonuses);
            }
        }

        return new Report(itemsTouched, statsChanged, bonusesChanged, weaponsSkipped, changes, notes);
    }
}
