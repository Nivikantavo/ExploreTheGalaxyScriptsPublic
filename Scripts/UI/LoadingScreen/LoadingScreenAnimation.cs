using PrimeTween;
using UnityEngine;
using UnityEngine.Serialization;

public class LoadingScreenAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform _center;
    [SerializeField] private RectTransform _orbiter;
    [SerializeField] private float _orbitDuration = 4f;
    [Tooltip("If <= 0, radius is taken from the initial distance between center and orbiter.")]
    [SerializeField] private float _radius = 120f;
    [SerializeField] private float _startAngleDegrees;
    [SerializeField] private bool _clockwise = true;
    [SerializeField] private bool _faceTravelDirection = true;
    [Tooltip("Constant Z rotation offset for the orbiter. Use -90 for sprites that point up, 0 for sprites that point right.")]
    [FormerlySerializedAs("_facingOffsetDegrees")]
    [SerializeField] private float _orbiterZRotationOffset = -90f;
    [Tooltip("Constant Z rotation offset for the center spin.")]
    [SerializeField] private float _centerZRotationOffset;

    private Tween _orbitTween;
    private float _resolvedRadius;
    private bool _orbiterIsChildOfCenter;

    private void OnEnable()
    {
        StartOrbit();
    }

    private void OnDisable()
    {
        StopOrbit();
    }

    private void StartOrbit()
    {
        if (_center == null || _orbiter == null || _orbitDuration <= 0f)
            return;

        StopOrbit();

        _orbiterIsChildOfCenter = _orbiter.parent == _center;
        _resolvedRadius = _radius > 0f ? _radius : ResolveRadiusFromLayout();
        if (_resolvedRadius <= 0f)
            return;

        float startAngle = _startAngleDegrees * Mathf.Deg2Rad;
        float direction = _clockwise ? -1f : 1f;
        float endAngle = startAngle + direction * Mathf.PI * 2f;

        ApplyOrbit(startAngle);

        _orbitTween = Tween.Custom(
            this,
            startAngle,
            endAngle,
            _orbitDuration,
            onValueChange: (self, angle) => self.ApplyOrbit(angle),
            ease: Ease.Linear,
            cycles: -1,
            cycleMode: CycleMode.Restart,
            useUnscaledTime: true);
    }

    private void StopOrbit()
    {
        if (_orbitTween.isAlive)
            _orbitTween.Stop();
    }

    private float ResolveRadiusFromLayout()
    {
        if (_orbiterIsChildOfCenter)
            return _orbiter.anchoredPosition.magnitude;

        return (_orbiter.anchoredPosition - _center.anchoredPosition).magnitude;
    }

    private void ApplyOrbit(float angleRadians)
    {
        float angleDegrees = angleRadians * Mathf.Rad2Deg;
        Vector2 offset = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * _resolvedRadius;

        // Opposite spin relative to orbit direction.
        float centerZ = -angleDegrees + _centerZRotationOffset;
        _center.localEulerAngles = new Vector3(0f, 0f, centerZ);

        if (_orbiterIsChildOfCenter)
            _orbiter.anchoredPosition = offset;
        else
            _orbiter.anchoredPosition = _center.anchoredPosition + offset;

        float orbiterZ = _orbiterZRotationOffset;
        if (_faceTravelDirection)
            orbiterZ += angleDegrees + (_clockwise ? -90f : 90f);

        // Parent rotation would otherwise double-apply to a child orbiter.
        if (_orbiterIsChildOfCenter)
            orbiterZ -= centerZ;

        _orbiter.localEulerAngles = new Vector3(0f, 0f, orbiterZ);
    }
}
