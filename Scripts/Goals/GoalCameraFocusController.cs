using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class GoalCameraFocusController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private Vector3 _focusOffset = new(0f, 6f, -6f);
    [SerializeField] private float _focusDuration = 2f;

    private readonly Dictionary<string, Transform> _targets = new();
    private Coroutine _focusCoroutine;
    private Transform _runtimeFollowTarget;
    private Transform _runtimeLookAtTarget;
    private Transform _restoreFollow;
    private Transform _restoreLookAt;
    private bool _isFocusing;

    private void Awake()
    {
        RebuildTargets();
    }

    public void FocusTarget(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || _camera == null)
            return;

        RebuildTargets();

        if (!_targets.TryGetValue(targetId, out Transform target) || target == null)
            return;

        FocusTransform(target);
    }

    public void FocusTravelHint(GoalTravelHint hint)
    {
        FocusTransform(GoalTravelFocusLocator.Find(hint));
    }

    public void FocusTransform(Transform target)
    {
        if (target == null || _camera == null)
            return;

        if (_focusCoroutine != null)
            StopCoroutine(_focusCoroutine);

        _focusCoroutine = StartCoroutine(FocusRoutine(target));
    }

    private IEnumerator FocusRoutine(Transform target)
    {
        EnsureRuntimeTargets();

        // Only capture originals once; StopCoroutine on re-click skips restore.
        if (!_isFocusing)
        {
            _restoreFollow = _camera.Follow;
            _restoreLookAt = _camera.LookAt;
            _isFocusing = true;
        }

        Vector3 focusPoint = ResolveFocusWorldPosition(target);
        _runtimeLookAtTarget.position = focusPoint;
        _runtimeFollowTarget.position = focusPoint + _focusOffset;
        _camera.Follow = _runtimeFollowTarget;
        _camera.LookAt = _runtimeLookAtTarget;

        yield return new WaitForSeconds(Mathf.Max(0.1f, _focusDuration));

        _camera.Follow = _restoreFollow;
        _camera.LookAt = _restoreLookAt;
        _isFocusing = false;
        _focusCoroutine = null;
    }

    private void EnsureRuntimeTargets()
    {
        if (_runtimeFollowTarget == null)
        {
            var followObject = new GameObject("GoalCameraFollowTarget");
            _runtimeFollowTarget = followObject.transform;
        }

        if (_runtimeLookAtTarget == null)
        {
            var lookAtObject = new GameObject("GoalCameraLookAtTarget");
            _runtimeLookAtTarget = lookAtObject.transform;
        }
    }

    private static Vector3 ResolveFocusWorldPosition(Transform target)
    {
        if (target.TryGetComponent(out IslandProgressHandler island))
            return island.GetFocusWorldPosition();

        return target.position;
    }

    private void RebuildTargets()
    {
        _targets.Clear();

        GoalFocusTarget[] targets = FindObjectsByType<GoalFocusTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GoalFocusTarget goalFocusTarget in targets)
        {
            if (goalFocusTarget == null || string.IsNullOrWhiteSpace(goalFocusTarget.TargetId))
                continue;

            _targets[goalFocusTarget.TargetId] = goalFocusTarget.transform;
        }
    }
}
