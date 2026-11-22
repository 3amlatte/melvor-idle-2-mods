using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Modifiers;

namespace PassiveSmelting;

internal static class Accessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Milestones>k__BackingField")]
    public static extern ref MilestonesComponent? MilestonesOf(MilestonesView view);
}
