public sealed class InterstitialWaitSession
{
    public const float DefaultSkipIfNeverOpenedSeconds = 3f;
    public const float DefaultHardTimeoutSeconds = 90f;

    public bool IsComplete { get; private set; }
    public bool DidOpen { get; private set; }

    public void NotifyOpened()
    {
        DidOpen = true;
    }

    public void NotifyFinished()
    {
        IsComplete = true;
    }

    public bool ShouldFinish(
        float elapsedUnscaledSeconds,
        float skipIfNeverOpenedSeconds = DefaultSkipIfNeverOpenedSeconds,
        float hardTimeoutSeconds = DefaultHardTimeoutSeconds)
    {
        if (IsComplete)
            return true;

        if (elapsedUnscaledSeconds >= hardTimeoutSeconds)
            return true;

        if (!DidOpen && elapsedUnscaledSeconds >= skipIfNeverOpenedSeconds)
            return true;

        return false;
    }
}
