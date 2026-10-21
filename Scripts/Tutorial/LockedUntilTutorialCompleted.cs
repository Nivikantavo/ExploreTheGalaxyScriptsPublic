using UnityEngine;

/// <summary>
/// Disables this HUD GameObject until the tutorial finishes.
/// Drop onto inventory / shop / achievements open buttons (not settings).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class LockedUntilTutorialCompleted : MonoBehaviour
{
    [Tooltip("GameObject to disable during tutorial. Defaults to this object.")]
    [SerializeField] private GameObject _target;

    private void Awake()
    {
        if (_target == null)
            _target = gameObject;

        TutorialHandler tutorialHandler = TutorialHudAccess.FindTutorialHandler();
        if (tutorialHandler != null)
            tutorialHandler.RegisterHudLock(_target);

        // Always register in the static list too — works even if TutorialHandler
        // resolves later / is missing, and survives SetActive(false) on this object.
        TutorialHudAccess.RegisterLockedHud(_target);
    }
}
