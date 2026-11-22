using System;
using System.Reflection;
using GamesByMalcs.Melvor2.Components.ArtisanAction;

namespace PassiveSmelting;

/// <summary>
/// Starts the Smithing action's timer without going through ActionComponent.TryStart, so the
/// action never occupies the active-action list and is not stopped when the player starts
/// another skill. The game still ticks the component through SmithingEntity.Tick, including
/// during offline progress.
/// </summary>
internal static class Artisan
{
    static MethodInfo? s_startCreating;

    public static bool StartCreating(ArtisanActionComponent action, ActiveArtisanRecipe recipe)
    {
        MethodInfo? method = StartCreatingMethod();
        if (method == null) return false;

        try { method.Invoke(action, new object[] { recipe }); }
        catch { return false; }

        return action.CreatingRecipe != null;
    }

    static MethodInfo? StartCreatingMethod()
    {
        if (s_startCreating != null) return s_startCreating;

        for (Type? type = typeof(ArtisanActionComponent); type != null; type = type.BaseType)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "StartCreating") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ActiveArtisanRecipe))
                {
                    s_startCreating = method;
                    return method;
                }
            }
        }
        return null;
    }
}
