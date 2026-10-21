public static class SaveExceptionPolicy
{
    private static readonly string[] SavePathMarkers =
    {
        nameof(SaveLoadBootstrap),
        nameof(SaveLoadSystem),
        nameof(SaveDataMerger),
        nameof(IslandProgressHandler),
        nameof(SaveIntegrityGuard)
    };

    public static bool ShouldMarkWorldUntrusted(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
            return false;

        for (int i = 0; i < SavePathMarkers.Length; i++)
        {
            if (stackTrace.IndexOf(SavePathMarkers[i], System.StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }
}
