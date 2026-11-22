namespace PassiveSmelting;

internal static class Tiers
{
    /// <summary>1 bar through 50, 2 bars from 51 to 85, 3 bars from 86 to 99.</summary>
    public static int BarsAt(int level) => level <= 50 ? 1 : level <= 85 ? 2 : 3;

    /// <summary>Percent interval adjustment that turns one cycle into N cycles' worth of output.</summary>
    public static double IntervalPercent(int bars) => 1.0 / bars - 1.0;

    public static string Describe(int bars) =>
        bars == 1 ? "1 bar at a time" : $"{bars} bars at a time";
}
