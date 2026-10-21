using System;
using UnityEngine;

public enum SceneTravelIntent
{
    None,
    EnterDungeon,
    ExitToHost,
    SwitchDungeon
}

public sealed class SceneTravelSession
{
    public bool IsBusy { get; private set; }
    public bool IsInDungeon { get; private set; }
    public string CurrentDungeonSceneName { get; private set; }
    public Vector3 ReturnPosition { get; private set; }
    public Quaternion ReturnRotation { get; private set; }

    public SceneTravelIntent Resolve(bool returnToHost, string sceneName)
    {
        if (IsBusy)
            return SceneTravelIntent.None;

        if (returnToHost)
            return IsInDungeon ? SceneTravelIntent.ExitToHost : SceneTravelIntent.None;

        if (string.IsNullOrWhiteSpace(sceneName))
            return SceneTravelIntent.None;

        if (!IsInDungeon)
            return SceneTravelIntent.EnterDungeon;

        if (string.Equals(CurrentDungeonSceneName, sceneName, StringComparison.Ordinal))
            return SceneTravelIntent.None;

        return SceneTravelIntent.SwitchDungeon;
    }

    public bool TryBegin()
    {
        if (IsBusy)
            return false;

        IsBusy = true;
        return true;
    }

    public void Abort()
    {
        IsBusy = false;
    }

    public void CompleteEnter(string dungeonSceneName, Vector3 returnPosition, Quaternion returnRotation)
    {
        IsInDungeon = true;
        CurrentDungeonSceneName = dungeonSceneName;
        ReturnPosition = returnPosition;
        ReturnRotation = returnRotation;
        IsBusy = false;
    }

    public void CompleteSwitch(string dungeonSceneName)
    {
        IsInDungeon = true;
        CurrentDungeonSceneName = dungeonSceneName;
        IsBusy = false;
    }

    public void CompleteExit()
    {
        IsInDungeon = false;
        CurrentDungeonSceneName = null;
        IsBusy = false;
    }
}

public static class SceneTravelContracts
{
    public const string Gameplay = "Gameplay";
}

public static class SceneTravelTiming
{
    public const float DefaultRunInSeconds = 0.5f;
    public const float DefaultMinLoadingSeconds = 1f;

    public static float RemainingLoadingSeconds(float elapsedUnscaledSeconds, float minLoadingSeconds)
    {
        if (minLoadingSeconds <= 0f)
            return 0f;

        float elapsed = elapsedUnscaledSeconds < 0f ? 0f : elapsedUnscaledSeconds;
        float remaining = minLoadingSeconds - elapsed;
        return remaining > 0f ? remaining : 0f;
    }

    public static Vector3 FlattenedRunInDirection(Vector3 from, Vector3 to, Vector3 fallbackForward)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        if (delta.sqrMagnitude >= 0.0001f)
            return delta.normalized;

        fallbackForward.y = 0f;
        if (fallbackForward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return fallbackForward.normalized;
    }
}
