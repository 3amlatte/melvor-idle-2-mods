using System.Runtime.CompilerServices;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatStats;
using GamesByMalcs.Melvor2.Modifiers;

namespace NoNegativeGear;

/// <summary>
/// Typed access to members the game keeps private. The loader itself uses the same trick for
/// GameView.Game. Names come from the game's assembly; if a game update renames one, the call
/// throws MissingMethodException / MissingFieldException the first time it runs.
/// </summary>
internal static class Accessors
{
    // Game.Modifiers is private, but the bonus system is the only way to re-apply a changed bonus.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Modifiers")]
    public static extern ModifierSystem ModifiersOf(Game game);

    // CombatStatValue.Value and DamageTypeStatValue.Value are public read-only properties with a
    // private setter. UnsafeAccessor can call the setter directly.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Value")]
    public static extern void SetValue(CombatStatValue value, double newValue);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Value")]
    public static extern void SetValue(DamageTypeStatValue value, int newValue);

    // GlobalModifierValue.Value / ComponentModifierValue.Value are get-only auto-properties:
    // the only way to change them is their compiler-generated backing field.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Value>k__BackingField")]
    public static extern ref double ValueOf(ComponentModifierValue value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Value>k__BackingField")]
    public static extern ref double ValueOf(GlobalModifierValue value);
}
