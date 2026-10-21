/// <summary>
/// Formats large numbers for UI: 2156 → "2.1k", 1_500_000 → "1.5m".
/// Values below 1000 stay unchanged. Fractional part is truncated to one digit.
/// </summary>
public static class CompactNumberFormatter
{
    private const ulong Thousand = 1_000UL;
    private const ulong Million = 1_000_000UL;
    private const ulong Billion = 1_000_000_000UL;
    private const ulong Trillion = 1_000_000_000_000UL;

    public static string Format(int value) => Format((long)value);

    public static string Format(long value)
    {
        if (value > -(long)Thousand && value < (long)Thousand)
            return value.ToString();

        bool isNegative = value < 0;
        ulong absolute = ToAbsolute(value);

        ResolveScale(absolute, out ulong divisor, out string suffix);

        ulong whole = absolute / divisor;
        ulong tenths = absolute % divisor * 10UL / divisor;

        string number = tenths == 0UL
            ? whole.ToString()
            : string.Concat(whole.ToString(), ".", tenths.ToString());

        return isNegative
            ? string.Concat("-", number, suffix)
            : string.Concat(number, suffix);
    }

    private static ulong ToAbsolute(long value)
    {
        if (value >= 0)
            return (ulong)value;

        if (value == long.MinValue)
            return (ulong)long.MaxValue + 1UL;

        return (ulong)(-value);
    }

    private static void ResolveScale(ulong absolute, out ulong divisor, out string suffix)
    {
        if (absolute >= Trillion)
        {
            divisor = Trillion;
            suffix = "t";
            return;
        }

        if (absolute >= Billion)
        {
            divisor = Billion;
            suffix = "b";
            return;
        }

        if (absolute >= Million)
        {
            divisor = Million;
            suffix = "m";
            return;
        }

        divisor = Thousand;
        suffix = "k";
    }
}
