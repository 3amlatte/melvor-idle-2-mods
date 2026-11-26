using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2.Components.Trackers;

namespace MasteryEta;

internal static class Accessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Tracker>k__BackingField")]
    public static extern ref TrackerComponent? TrackerOf(TrackerComponentFooterView view);
}
