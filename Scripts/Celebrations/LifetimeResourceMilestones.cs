public static class LifetimeResourceMilestones
{
    public const int DefaultStep = 1000;

    public static int HighestReached(int lifetimeAmount, int step)
    {
        if (step <= 0 || lifetimeAmount < step)
            return 0;

        return (lifetimeAmount / step) * step;
    }

    public static int ConsumeNewlyReached(int lifetimeAmount, int step, ref int acknowledged)
    {
        int reached = HighestReached(lifetimeAmount, step);
        if (reached <= acknowledged)
            return 0;

        acknowledged = reached;
        return reached;
    }

    public static string FormatBody(string localizedBody, int milestone) =>
        $"{milestone} {localizedBody}";
}
