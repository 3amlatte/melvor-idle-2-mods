using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2.Components.Milestones;

namespace SeedReturnStandard;

internal static class Accessors
{
    // MilestonesView keeps the component it is showing in a private auto-property. Comparing
    // that with the Farming entity's component tells us whether this is the Farming window,
    // without depending on the translated title text.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Milestones>k__BackingField")]
    public static extern ref MilestonesComponent? MilestonesOf(MilestonesView view);
}
