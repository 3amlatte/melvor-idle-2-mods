using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Modifiers;

namespace TimeSkip;

internal static class Accessors
{
    // Game.Modifiers is private. A temporary FlatOfflineTime bonus raises the offline cap just
    // long enough for the requested skip to be accepted, then it is removed.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);
}
