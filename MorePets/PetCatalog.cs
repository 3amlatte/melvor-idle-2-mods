using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;

namespace MorePets;

internal enum MorePetKind
{
    Stance,
    GlobalGold,
    GlobalInterval,
    GlobalXp,
}

/// <summary>One collectible pet and the enemy that drops it.</summary>
internal sealed class MorePetDef : INamedObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ShortName { get; init; }
    public required string Description { get; init; }
    public required string EnemyId { get; init; }
    public required string Location { get; init; }
    public required MorePetKind Kind { get; init; }
    public required int FallbackLevel { get; init; }
    public required string IconFile { get; init; }
    public required string BuffText { get; init; }

    /// <summary>Attack stance id for stance pets, e.g. MelvorBase:Stab.</summary>
    public string? StanceId { get; init; }

    /// <summary>Combat style id for stance pets, e.g. MelvorBase:Melee.</summary>
    public string? StyleId { get; init; }

    public LocalizedString DisplayName { get; private set; } = LocalizedString.Placeholder;

    ILocalizedString INamedObject.DisplayName => DisplayName;

    public Texture2D? Icon { get; set; }

    /// <summary>The modifier values applied while the pet is owned and enabled.</summary>
    public ModifierValues? Modifiers { get; set; }

    public void SetDisplayName()
    {
        DisplayName = new LocalizedString(Name);
    }
}

internal static class PetCatalog
{
    public const string Subcategory = "Dungeon Bosses";

    public static readonly IReadOnlyList<MorePetDef> All = new List<MorePetDef>
    {
        new()
        {
            Id = "Eraxi",
            Name = "Lil Eraxi, the Blighted Scar",
            ShortName = "Lil Eraxi",
            Description = "A shard of the Blighted Scar, still humming with ruined magic.",
            EnemyId = "MelvorBase:EraxitheBlightedScar",
            Location = "Blighted Scar",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Defensive",
            StyleId = "MelvorBase:Magic",
            FallbackLevel = 83,
            IconFile = "Eraxi.png",
            BuffText = "+20% max hit and -20% attack interval with Focused Magic attacks.",
        },
        new()
        {
            Id = "ScarGolem",
            Name = "Lil Scar Golem",
            ShortName = "Lil Scar Golem",
            Description = "A walking wound in the land, held together by old magic.",
            EnemyId = "MelvorBase:ScarGolem",
            Location = "Blighted Scar",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Offensive",
            StyleId = "MelvorBase:Magic",
            FallbackLevel = 82,
            IconFile = "ScarGolem.png",
            BuffText = "+20% max hit and -20% attack interval with Empowered Magic attacks.",
        },
        new()
        {
            Id = "KingBlackDragon",
            Name = "Lil King Black Dragon",
            ShortName = "Lil King Black Dragon",
            Description = "The black dragon's king, small enough to perch on your shoulder.",
            EnemyId = "MelvorBase:KingBlackDragon",
            Location = "Black Dragon's Lair",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Stab",
            StyleId = "MelvorBase:Melee",
            FallbackLevel = 90,
            IconFile = "KingBlackDragon.png",
            BuffText = "+20% max hit and -20% attack interval with Stab Melee attacks.",
        },
        new()
        {
            Id = "Mesmeris",
            Name = "Lil Mesmeris, the Slumbering Princess",
            ShortName = "Lil Mesmeris",
            Description = "The princess of the Glowsquid caverns, dreaming of brighter water.",
            EnemyId = "MelvorBase:MesmeristheSlumberingPrincess",
            Location = "Glowsquid Caverns",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Powerful",
            StyleId = "MelvorBase:Ranged",
            FallbackLevel = 75,
            IconFile = "Mesmeris.png",
            BuffText = "+20% max hit and -20% attack interval with Powerful Ranged attacks.",
        },
        new()
        {
            Id = "Qazarak",
            Name = "Lil Qazarak, the Shiftsand Guardian",
            ShortName = "Lil Qazarak",
            Description = "A guardian of the shifting sands, now shifting around your feet.",
            EnemyId = "MelvorBase:QazaraktheShiftsandGuardian",
            Location = "Shiftsand Ruins",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Accurate",
            StyleId = "MelvorBase:Ranged",
            FallbackLevel = 83,
            IconFile = "Qazarak.png",
            BuffText = "+20% max hit and -20% attack interval with Accurate Ranged attacks.",
        },
        new()
        {
            Id = "Kythiss",
            Name = "Lil Kythiss, the Venom Queen",
            ShortName = "Lil Kythiss",
            Description = "A tiny venom queen with an extremely large opinion of herself.",
            EnemyId = "MelvorBase:KythisstheVenomQueen",
            Location = "Venom Pit",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Slash",
            StyleId = "MelvorBase:Melee",
            FallbackLevel = 44,
            IconFile = "Kythiss.png",
            BuffText = "+20% max hit and -20% attack interval with Slash Melee attacks.",
        },
        new()
        {
            Id = "Zuraxa",
            Name = "Lil Zuraxa, the Brood Mother",
            ShortName = "Lil Zuraxa",
            Description = "The brood mother's smallest child. Probably harmless.",
            EnemyId = "MelvorBase:ZuraxatheBroodMother",
            Location = "Murkwood Lair",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:LongRange",
            StyleId = "MelvorBase:Ranged",
            FallbackLevel = 62,
            IconFile = "Zuraxa.png",
            BuffText = "+20% max hit and -20% attack interval with Long-Range Ranged attacks.",
        },
        new()
        {
            Id = "BloodSummoner",
            Name = "Lil Blood Summoner",
            ShortName = "Lil Blood Summoner",
            Description = "It keeps trying to summon things. Mostly snacks.",
            EnemyId = "MelvorBase:BloodSummoner",
            Location = "Blood Sanctum",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Crush",
            StyleId = "MelvorBase:Melee",
            FallbackLevel = 90,
            IconFile = "BloodSummoner.png",
            BuffText = "+20% max hit and -20% attack interval with Crush Melee attacks.",
        },
        new()
        {
            Id = "Magmor",
            Name = "Lil Magmor, the Molten Behemoth",
            ShortName = "Lil Magmor",
            Description = "A molten behemoth with a surprisingly calm temperament.",
            EnemyId = "MelvorBase:MagmortheMoltenBehemoth",
            Location = "Blackstone Gauntlet",
            Kind = MorePetKind.Stance,
            StanceId = "MelvorBase:Block",
            StyleId = "MelvorBase:Melee",
            FallbackLevel = 74,
            IconFile = "Magmor.png",
            BuffText = "+20% max hit and -20% attack interval with Block Melee attacks.",
        },
        new()
        {
            Id = "Morgis",
            Name = "Lil Morgis, the Grave Corruptor",
            ShortName = "Lil Morgis",
            Description = "A grave corrupter who has decided your coin purse is interesting.",
            EnemyId = "MelvorBase:MorgistheGraveCorruptor",
            Location = "Harvest Farm",
            Kind = MorePetKind.GlobalGold,
            FallbackLevel = 26,
            IconFile = "Morgis.png",
            BuffText = "+20% gold from all sources.",
        },
        new()
        {
            Id = "Ghelbak",
            Name = "Lil Ghelbak, the Hob Chief",
            ShortName = "Lil Ghelbak",
            Description = "The hob chief, now chief of a much smaller camp.",
            EnemyId = "MelvorBase:GhelbaktheHobChief",
            Location = "Hobgolbin Camp",
            Kind = MorePetKind.GlobalInterval,
            FallbackLevel = 15,
            IconFile = "Ghelbak.png",
            BuffText = "-20% global action interval.",
        },
        new()
        {
            Id = "Kraken",
            Name = "Lil Kraken, the Drowned Overlord",
            ShortName = "Lil Kraken",
            Description = "The drowned overlord, reduced to a very damp lap pet.",
            EnemyId = "MelvorBase:KrakentheDrownedOverlord",
            Location = "Kraken's Domain",
            Kind = MorePetKind.GlobalXp,
            FallbackLevel = 96,
            IconFile = "Kraken.png",
            BuffText = "+20% global skill XP.",
        },
    };

    public static readonly IReadOnlyDictionary<string, MorePetDef> ById =
        All.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, MorePetDef> ByEnemy =
        All.ToDictionary(p => p.EnemyId, StringComparer.OrdinalIgnoreCase);

    public static int RateForLevel(int level)
    {
        if (level >= 84) return 2500;
        if (level >= 51) return 3500;
        return 5000;
    }
}
