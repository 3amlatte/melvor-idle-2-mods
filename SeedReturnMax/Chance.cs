using System;
using System.Collections.Generic;

namespace SeedReturnMax;

/// <summary>
/// 60% at level 1, 150% at level 99, with a gentle ease-in curve: slower at low levels,
/// faster near the cap. Rounded to whole percent. Above 100% one batch is guaranteed per
/// full 100%, with the remainder as a chance for one more.
/// </summary>
internal static class Chance
{
    public const int MaxLevel = 99;
    public const int MinPercent = 60;
    public const int MaxPercent = 150;

    public static int Percent(int level)
    {
        if (level <= 1) return MinPercent;
        if (level >= MaxLevel) return MaxPercent;

        double t = (level - 1) / (double)(MaxLevel - 1);
        return (int)Math.Round(MinPercent + (MaxPercent - MinPercent) * Math.Pow(t, 1.25), MidpointRounding.AwayFromZero);
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
        var lines = new List<string>();
        foreach (var (from, to, percent) in Bands())
        {
            lines.Add(from == to
                ? $"Level {from}: {percent}%"
                : $"Levels {from}-{to}: {percent}%");
        }
        return string.Join("\n", lines);
    }

    /// <summary>The level ranges that share each rounded percentage.</summary>
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
