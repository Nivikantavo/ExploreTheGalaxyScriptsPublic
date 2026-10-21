using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using Playgama;
using Playgama.Modules.Platform;
#endif

/// <summary>
/// Sends Playgama Bridge level lifecycle messages only on platforms that relay them
/// (crazy_games, poki, yandex). Nested pauses use a depth counter so celebration + ads
/// do not emit mismatched resume events.
/// </summary>
public static class PlatformGameplayLifecycle
{
    private static readonly HashSet<string> SupportedPlatformIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "crazy_games",
        "poki",
        "yandex",
    };

    private static bool _levelStarted;
    private static int _pauseDepth;

    public static bool IsLevelStarted => _levelStarted;

    public static void NotifyLevelStarted()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_levelStarted || !CanSend())
            return;

        _levelStarted = true;
        _pauseDepth = 0;
        Send(PlatformMessage.LevelStarted);
#endif
    }

    public static void NotifyLevelPaused()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_levelStarted || !CanSend())
            return;

        _pauseDepth++;
        if (_pauseDepth == 1)
            Send(PlatformMessage.LevelPaused);
#endif
    }

    public static void NotifyLevelResumed()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_levelStarted || _pauseDepth <= 0 || !CanSend())
            return;

        _pauseDepth--;
        if (_pauseDepth == 0)
            Send(PlatformMessage.LevelResumed);
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static bool CanSend()
    {
        if (Bridge.instance == null || Bridge.platform == null)
            return false;

        string platformId = Bridge.platform.id;
        return !string.IsNullOrEmpty(platformId) && SupportedPlatformIds.Contains(platformId);
    }

    private static void Send(PlatformMessage message)
    {
        try
        {
            Bridge.platform.SendMessage(message);
        }
        catch (Exception exception)
        {
            Debug.LogError($"{nameof(PlatformGameplayLifecycle)}: {message} failed: {exception}");
        }
    }
#endif
}
