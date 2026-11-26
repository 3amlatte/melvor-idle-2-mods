using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.PetsCompletion;
using GamesByMalcs.Melvor2.Entities.CompletionLog;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;

namespace MorePets;

internal static class Accessors
{
    // Game.Modifiers is private; this is the only way to add and remove pet bonuses.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);

    // PetsCompletionView keeps its bound component in a private auto-property.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<PetsCompletion>k__BackingField")]
    public static extern ref PetsCompletionComponent? PetsCompletionOf(PetsCompletionView view);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<CurrentCategoryLabel>k__BackingField")]
    public static extern ref Label? CurrentCategoryLabelOf(CompletionLogActiveView view);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<SkillNameLabel>k__BackingField")]
    public static extern ref Label? SkillNameLabelOf(PetsCompletionBreakdownView view);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<PetCountLabel>k__BackingField")]
    public static extern ref Int32OutOfLabel? PetCountLabelOf(PetsCompletionBreakdownView view);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<PetProgress>k__BackingField")]
    public static extern ref TextureProgressBar? PetProgressOf(PetsCompletionBreakdownView view);
}
