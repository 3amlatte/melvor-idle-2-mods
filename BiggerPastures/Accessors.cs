using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2.Entities.Ranching;

namespace BiggerPastures;

internal static class Accessors
{
    // RanchingEnclosure.AnimalLimit has a private setter; the value lives in the game's data
    // and is read live by the enclosure logic and its UI.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_AnimalLimit")]
    public static extern void SetAnimalLimit(RanchingEnclosure enclosure, int value);
}
