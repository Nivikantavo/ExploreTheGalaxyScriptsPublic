public static class IslandPercentCelebrationRules
{
    public static bool TryConsumeIncreasedPercent(
        int currentPercent,
        ref int acknowledgedPercent,
        out int percentToShow)
    {
        if (currentPercent <= acknowledgedPercent)
        {
            percentToShow = 0;
            return false;
        }

        acknowledgedPercent = currentPercent;
        percentToShow = currentPercent;
        return true;
    }

    public static string FormatBody(string localizedBody, int percent) =>
        $"{localizedBody} {percent}%";
}
