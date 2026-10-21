using UnityEngine;

/// <summary>
/// Nested gameplay pause via Time.timeScale. Also forwards Playgama level pause/resume.
/// </summary>
public static class GameplayPause
{
    private static int _depth;
    private static float _savedTimeScale = 1f;

    public static bool IsPaused => _depth > 0;

    public static void Push()
    {
        if (_depth == 0)
        {
            _savedTimeScale = GameplayPausePolicy.CaptureSavedTimeScale(Time.timeScale);
            Time.timeScale = GameplayPausePolicy.ResolveTimeScale(1, _savedTimeScale);
        }

        _depth = GameplayPausePolicy.PushDepth(_depth);
        PlatformGameplayLifecycle.NotifyLevelPaused();
    }

    public static void Pop()
    {
        int nextDepth = GameplayPausePolicy.PopDepth(_depth);
        if (nextDepth == _depth)
            return;

        _depth = nextDepth;
        Time.timeScale = GameplayPausePolicy.ResolveTimeScale(_depth, _savedTimeScale);
        PlatformGameplayLifecycle.NotifyLevelResumed();
    }
}
