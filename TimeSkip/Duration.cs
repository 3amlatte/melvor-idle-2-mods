namespace TimeSkip;

internal static class Duration
{
    public static string Format(double hours)
    {
        if (hours >= 48 && hours % 24 == 0) return $"{hours / 24:0} days";
        if (hours == 1) return "1 hour";
        return $"{hours:0} hours";
    }
}
