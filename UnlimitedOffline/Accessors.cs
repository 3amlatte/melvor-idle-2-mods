using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Modifiers;

namespace UnlimitedOffline;

internal static class Accessors
{
    // Game.Modifiers is private. The modifier system is the game's own way of extending
    // offline time (shop upgrades use the same stat), so no game code is patched.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);
}
