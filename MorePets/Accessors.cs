using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.PetsCompletion;
using GamesByMalcs.Melvor2.Modifiers;

namespace MorePets;

internal static class Accessors
{
    // Game.Modifiers is private; this is the only way to add and remove pet bonuses.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);

    // PetsCompletionView keeps its bound component in a private auto-property.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<PetsCompletion>k__BackingField")]
    public static extern ref PetsCompletionComponent? PetsCompletionOf(PetsCompletionView view);
}
