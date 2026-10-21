using System;
using UnityEngine;

public static class GoalTravelFocusLocator
{
    public static Transform Find(GoalTravelHint hint)
    {
        switch (hint.Kind)
        {
            case GoalTravelHintKind.ExitDungeon:
                return FindReturnGate(hint.DungeonSceneName);
            case GoalTravelHintKind.EnterDungeon:
                return FindEnterGate(hint.DungeonSceneName);
            case GoalTravelHintKind.GoToPlanet:
                return FindPortal();
            default:
                return null;
        }
    }

    private static Transform FindReturnGate(string dungeonSceneName)
    {
        SceneTravelGate[] gates = UnityEngine.Object.FindObjectsByType<SceneTravelGate>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < gates.Length; i++)
        {
            SceneTravelGate gate = gates[i];
            if (gate == null || gate.Destination == null || !gate.Destination.ReturnToHost)
                continue;

            if (!string.IsNullOrEmpty(dungeonSceneName) &&
                !string.Equals(gate.gameObject.scene.name, dungeonSceneName, StringComparison.Ordinal))
                continue;

            return gate.transform;
        }

        return null;
    }

    private static Transform FindEnterGate(string dungeonSceneName)
    {
        if (string.IsNullOrEmpty(dungeonSceneName))
            return null;

        SceneTravelGate[] gates = UnityEngine.Object.FindObjectsByType<SceneTravelGate>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < gates.Length; i++)
        {
            SceneTravelGate gate = gates[i];
            if (gate == null || gate.Destination == null || gate.Destination.ReturnToHost)
                continue;

            if (!string.Equals(gate.Destination.SceneName, dungeonSceneName, StringComparison.Ordinal))
                continue;

            return gate.transform;
        }

        return null;
    }

    private static Transform FindPortal()
    {
        Portal portal = UnityEngine.Object.FindFirstObjectByType<Portal>(FindObjectsInactive.Exclude);
        return portal != null ? portal.transform : null;
    }
}
