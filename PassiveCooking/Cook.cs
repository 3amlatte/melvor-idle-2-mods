using System;
using System.Reflection;
using GamesByMalcs.Melvor2.Entities.Cooking;

namespace PassiveCooking;

/// <summary>
/// Starts the cooking action's timer without going through ActionComponent.TryStart, so the
/// action never occupies the active-action list and is not stopped when the player starts
/// another skill. The game still ticks the component through CookingEntity.Tick, including
/// during offline progress.
/// </summary>
internal static class Cook
{
    static MethodInfo? s_startCreating;

    public static bool StartCreating(CookingActionComponent action, ActiveCookingRecipe recipe)
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

        for (Type? type = typeof(CookingActionComponent); type != null; type = type.BaseType)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "StartCreating") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsAssignableFrom(typeof(ActiveCookingRecipe))) continue;

                s_startCreating = method;
                return method;
            }
        }
        return null;
    }
}
