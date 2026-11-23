using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Modifiers;

namespace MelvorAuto;

internal static class Accessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);
}
