public static class SavePersistPolicy
{
    public static bool CanWriteToStorage(SaveStorageInitState initState)
    {
        return initState == SaveStorageInitState.Empty
               || initState == SaveStorageInitState.Loaded;
    }

    public static bool ShouldCollectLiveWorld(
        bool worldUntrusted,
        int registeredIslandCount,
        bool hasLastKnownGood,
        bool hostWorldMatches = true)
    {
        if (worldUntrusted)
            return false;

        if (!hostWorldMatches)
            return false;

        if (hasLastKnownGood && registeredIslandCount <= 0)
            return false;

        return true;
    }
}
