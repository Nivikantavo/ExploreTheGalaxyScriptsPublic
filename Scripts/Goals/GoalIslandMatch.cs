using System;

public static class GoalIslandMatch
{
    public static bool Matches(int requiredIslandIndex, string requiredSceneName, int islandIndex, string sceneName)
    {
        if (islandIndex != requiredIslandIndex)
            return false;

        if (string.IsNullOrEmpty(requiredSceneName))
            return true;

        return string.Equals(requiredSceneName, sceneName, StringComparison.Ordinal);
    }
}
