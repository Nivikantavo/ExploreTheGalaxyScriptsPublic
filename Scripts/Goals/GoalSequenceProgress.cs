using System;
using System.Collections.Generic;

public static class GoalSequenceProgress
{
    public static string ResolveWorldId(GoalSequenceConfigSO config)
    {
        if (config == null || string.IsNullOrEmpty(config.WorldId))
            return SaveWorldIds.World1;

        return config.WorldId;
    }

    public static int GetSameWorldGoalCount(GoalSequenceConfigSO start, string worldId)
    {
        if (start == null)
            return 0;

        if (string.IsNullOrEmpty(worldId))
            worldId = ResolveWorldId(start);

        int count = 0;
        var visited = new HashSet<GoalSequenceConfigSO>();
        GoalSequenceConfigSO config = start;

        while (config != null && visited.Add(config))
        {
            if (!string.Equals(ResolveWorldId(config), worldId, StringComparison.Ordinal))
                break;

            if (config.Goals != null)
                count += config.Goals.Count;

            config = config.NextSequence;
        }

        return count;
    }

    public static bool IsOnWorld(GoalSequenceConfigSO sequence, string worldId)
    {
        if (sequence == null)
            return false;

        if (string.IsNullOrEmpty(worldId))
            worldId = SaveWorldIds.World1;

        return string.Equals(ResolveWorldId(sequence), worldId, StringComparison.Ordinal);
    }

    public static bool TryResolve(
        GoalSequenceConfigSO start,
        int completedCount,
        string currentWorldId,
        Func<string, int> getCompletedCountForWorld,
        out GoalDefinitionSO goal,
        out GoalSequenceConfigSO sequence,
        Func<string, GoalSequenceConfigSO> getSequenceForWorld = null)
    {
        goal = null;
        sequence = null;
        if (start == null)
            return false;

        string localWorldId = string.IsNullOrEmpty(currentWorldId)
            ? ResolveWorldId(start)
            : currentWorldId;

        var visited = new HashSet<GoalSequenceConfigSO>();
        GoalSequenceConfigSO config = start;
        int remaining = completedCount < 0 ? 0 : completedCount;

        while (config != null)
        {
            if (!visited.Add(config))
                return false;

            string configWorldId = ResolveWorldId(config);
            if (!string.Equals(configWorldId, localWorldId, StringComparison.Ordinal))
            {
                int foreignCount = getCompletedCountForWorld != null
                    ? Math.Max(0, getCompletedCountForWorld(configWorldId))
                    : 0;
                return TryResolve(
                    config,
                    foreignCount,
                    configWorldId,
                    getCompletedCountForWorld,
                    out goal,
                    out sequence,
                    getSequenceForWorld);
            }

            IReadOnlyList<GoalDefinitionSO> goals = config.Goals;
            int goalCount = goals != null ? goals.Count : 0;
            if (remaining < goalCount)
            {
                goal = goals[remaining];
                sequence = config;
                return goal != null;
            }

            remaining -= goalCount;
            config = ResolveNext(config, configWorldId, getSequenceForWorld);
        }

        return false;
    }

    public static GoalSequenceConfigSO ResolveNext(
        GoalSequenceConfigSO config,
        string configWorldId = null,
        Func<string, GoalSequenceConfigSO> getSequenceForWorld = null)
    {
        if (config == null)
            return null;

        if (config.NextSequence != null)
            return config.NextSequence;

        if (getSequenceForWorld == null)
            return null;

        if (string.IsNullOrEmpty(configWorldId))
            configWorldId = ResolveWorldId(config);

        string nextWorldId = WorldCatalog.GetNextWorldId(configWorldId);
        if (string.IsNullOrEmpty(nextWorldId))
            return null;

        return getSequenceForWorld(nextWorldId);
    }
}
