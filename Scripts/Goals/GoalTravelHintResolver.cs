using System;
using System.Collections.Generic;

public enum GoalTravelHintKind
{
    None = 0,
    ExitDungeon = 1,
    GoToPlanet = 2,
    EnterDungeon = 3
}

public readonly struct GoalPlayerLocation
{
    public GoalPlayerLocation(bool isInDungeon, string currentDungeonSceneName, string hostWorldId)
    {
        IsInDungeon = isInDungeon;
        CurrentDungeonSceneName = currentDungeonSceneName;
        HostWorldId = string.IsNullOrEmpty(hostWorldId) ? SaveWorldIds.World1 : hostWorldId;
    }

    public bool IsInDungeon { get; }
    public string CurrentDungeonSceneName { get; }
    public string HostWorldId { get; }
}

public readonly struct GoalTravelHint
{
    public static GoalTravelHint None => default;

    public GoalTravelHint(GoalTravelHintKind kind, string planetWorldId, string dungeonSceneName)
    {
        Kind = kind;
        PlanetWorldId = planetWorldId;
        DungeonSceneName = dungeonSceneName;
    }

    public GoalTravelHintKind Kind { get; }
    public string PlanetWorldId { get; }
    public string DungeonSceneName { get; }
}

public static class GoalTravelHintResolver
{
    public static string ResolveGoalScene(
        GoalDefinitionSO goal,
        GoalSequenceConfigSO sequence,
        IReadOnlyList<WorldCatalogEntry> catalog = null)
    {
        if (goal != null && !string.IsNullOrEmpty(goal.RequiredSceneName))
            return goal.RequiredSceneName;

        return WorldCatalog.ResolveSceneName(GoalSequenceProgress.ResolveWorldId(sequence), catalog);
    }

    public static string ResolveGoalWorldId(
        GoalDefinitionSO goal,
        GoalSequenceConfigSO sequence,
        IReadOnlyList<WorldCatalogEntry> catalog = null)
    {
        if (goal != null &&
            !string.IsNullOrEmpty(goal.RequiredSceneName) &&
            WorldCatalog.IsKnownHostScene(goal.RequiredSceneName, catalog))
        {
            return WorldCatalog.ResolveWorldIdFromScene(goal.RequiredSceneName, catalog);
        }

        return GoalSequenceProgress.ResolveWorldId(sequence);
    }

    public static GoalTravelHint Resolve(
        GoalDefinitionSO goal,
        GoalSequenceConfigSO sequence,
        GoalPlayerLocation player,
        IReadOnlyList<WorldCatalogEntry> catalog = null)
    {
        if (goal == null)
            return GoalTravelHint.None;

        string goalScene = ResolveGoalScene(goal, sequence, catalog);
        string goalWorldId = ResolveGoalWorldId(goal, sequence, catalog);
        string hostScene = WorldCatalog.ResolveSceneName(player.HostWorldId, catalog);

        if (player.IsInDungeon)
        {
            if (!string.Equals(goalScene, player.CurrentDungeonSceneName, StringComparison.Ordinal))
                return new GoalTravelHint(
                    GoalTravelHintKind.ExitDungeon,
                    null,
                    player.CurrentDungeonSceneName);

            return GoalTravelHint.None;
        }

        if (WorldCatalog.IsHostMismatch(goalWorldId, player.HostWorldId))
            return new GoalTravelHint(GoalTravelHintKind.GoToPlanet, goalWorldId, null);

        if (!string.IsNullOrEmpty(goalScene) &&
            !string.Equals(goalScene, hostScene, StringComparison.Ordinal))
        {
            return new GoalTravelHint(GoalTravelHintKind.EnterDungeon, goalWorldId, goalScene);
        }

        return GoalTravelHint.None;
    }
}
