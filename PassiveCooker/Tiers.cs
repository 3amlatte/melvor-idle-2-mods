namespace PassiveCooker;

internal static class Tiers
{
    /// <summary>1 portion through 50, 2 from 51 to 85, 3 from 86 to 99.</summary>
    public static int PortionsAt(int level) => level <= 50 ? 1 : level <= 85 ? 2 : 3;

    /// <summary>Percent interval adjustment that turns one cycle into N cycles' worth of output.</summary>
    public static double IntervalPercent(int portions) => 1.0 / portions - 1.0;

    public static string Describe(int portions) =>
        portions == 1 ? "1 portion at a time" : $"{portions} portions at a time";
}
