using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared rules + registry for locking meta HUD until the tutorial finishes.
/// Registry keeps GameObject refs so buttons can be SetActive(false) and restored later
/// even when the lock component lives on the disabled object.
/// </summary>
public static class TutorialHudAccess
{
    private static readonly List<GameObject> RegisteredLocks = new();
    private static bool Unlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RegisteredLocks.Clear();
        Unlocked = false;
    }

    public static bool IsMetaHudUnlocked(ISaveLoader saveLoader, TutorialHandler tutorialHandler)
    {
        if (Unlocked)
            return true;

        // Keep meta HUD locked while the in-scene tutorial object is still resolving
        // (including the end cutscene after MarkTutorialCompleted).
        if (tutorialHandler != null && tutorialHandler.isActiveAndEnabled && !tutorialHandler.IsResolved)
            return false;

        if (saveLoader != null && saveLoader.IsTutorialCompleted)
            return true;

        // Scenes without a tutorial (e.g. main menu) keep meta HUD available.
        if (tutorialHandler == null)
            return true;

        return tutorialHandler.IsResolved;
    }

    public static TutorialHandler FindTutorialHandler()
    {
        return Object.FindFirstObjectByType<TutorialHandler>(FindObjectsInactive.Include);
    }

    public static void RegisterLockedHud(GameObject target)
    {
        if (target == null)
            return;

        if (!RegisteredLocks.Contains(target))
            RegisteredLocks.Add(target);

        if (!Unlocked)
            target.SetActive(false);
    }

    public static void NotifyTutorialResolved()
    {
        Unlocked = true;

        for (int i = 0; i < RegisteredLocks.Count; i++)
        {
            GameObject target = RegisteredLocks[i];
            if (target != null)
                target.SetActive(true);
        }
    }
}
