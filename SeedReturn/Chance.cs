using System;
using System.Collections.Generic;

namespace SeedReturn;

/// <summary>
/// 1% at level 1, 20% at level 99, with a gentle ease-in curve: slower at low levels,
/// faster near the cap. Rounded to whole percent.
/// </summary>
internal static class Chance
{
    public const int MaxLevel = 99;
    public const int MaxPercent = 20;

    public static int Percent(int level)
    {
        if (level <= 1) return 1;
        if (level >= MaxLevel) return MaxPercent;

        double t = (level - 1) / (double)(MaxLevel - 1);
        return (int)Math.Round(1 + (MaxPercent - 1) * Math.Pow(t, 1.25), MidpointRounding.AwayFromZero);
    }

    /// <summary>The next level at which the rounded chance goes up, or 0 if already at the cap.</summary>
    public static int NextLevel(int level)
    {
        int now = Percent(level);
        for (int next = Math.Max(1, level) + 1; next <= MaxLevel; next++)
        {
            if (Percent(next) > now) return next;
        }
        return 0;
    }

    /// <summary>Every percentage band, one per line, for tooltips.</summary>
    public static string Table()
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var (from, to, percent) in Bands())
        {
            lines.Add(from == to
                ? $"Level {from}: {percent}%"
                : $"Levels {from}-{to}: {percent}%");
        }
        return string.Join("\n", lines);
    }

    /// <summary>The level ranges that share each rounded percentage, from 1% to 20%.</summary>
    public static IEnumerable<(int From, int To, int Percent)> Bands()
    {
        int start = 1;
        for (int level = 2; level <= MaxLevel; level++)
        {
            if (Percent(level) == Percent(start)) continue;
            yield return (start, level - 1, Percent(start));
            start = level;
        }
        yield return (start, MaxLevel, Percent(start));
    }
}
