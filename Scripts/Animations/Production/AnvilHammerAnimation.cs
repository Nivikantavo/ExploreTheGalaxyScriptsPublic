using PrimeTween;
using UnityEngine;

public class AnvilHammerAnimation : ProductionAnimationBehaviour
{
    [SerializeField] private Transform _circlePositive;
    [SerializeField] private Transform _circleNegative;
    [SerializeField] private Transform _hammer;
    [SerializeField] private string _circlePositiveName = "Circle.026";
    [SerializeField] private string _circleNegativeName = "Circle.025";
    [SerializeField] private string _hammerName = "Cube.114";
    [SerializeField] private Transform _sparkPoint;
    [SerializeField] private GameObject _sparkPrefab;
    [SerializeField] private float _sparkScale = 0.45f;
    [SerializeField] private float _gearAngle = 120f;
    [SerializeField] private float _hammerAngle = 90f;
    [SerializeField] private float _windupDuration = 0.55f;
    [SerializeField] private float _strikeDuration = 0.12f;

    private Quaternion _circlePositiveRest;
    private Quaternion _circleNegativeRest;
    private Quaternion _hammerRest;
    private Sequence _strike;

    protected override void OnAnimationAwake()
    {
        if (_circlePositive == null)
            _circlePositive = ProductionWorkGate.FindDeepChild(transform, _circlePositiveName);

        if (_circleNegative == null)
            _circleNegative = ProductionWorkGate.FindDeepChild(transform, _circleNegativeName);

        if (_hammer == null)
            _hammer = ProductionWorkGate.FindDeepChild(transform, _hammerName);

        if (_circlePositive != null)
            _circlePositiveRest = _circlePositive.localRotation;

        if (_circleNegative != null)
            _circleNegativeRest = _circleNegative.localRotation;

        if (_hammer != null)
            _hammerRest = _hammer.localRotation;
    }

    protected override void OnWorkStarted()
    {
        if (_circlePositive == null || _circleNegative == null || _hammer == null)
        {
            Debug.LogWarning($"{name}: anvil parts Circle.026, Circle.025 or Cube.114 were not found.", this);
            return;
        }

        PlayStrike();
    }

    protected override void OnWorkStopped()
    {
        if (_strike.isAlive)
            _strike.Stop();

        ApplyPose(0f);
    }

    private void PlayStrike()
    {
        if (!IsPlaying)
            return;

        _strike = Sequence.Create()
            .Chain(Tween.Custom(
                this,
                0f,
                1f,
                _windupDuration,
                onValueChange: (self, t) => self.ApplyPose(t),
                ease: Ease.InOutSine))
            .Chain(Tween.Custom(
                this,
                1f,
                0f,
                _strikeDuration,
                onValueChange: (self, t) => self.ApplyPose(t),
                ease: Ease.InCubic))
            .ChainCallback(this, animation => animation.OnImpact());
    }

    private void OnImpact()
    {
        if (!IsPlaying)
            return;

        Vector3 point = _sparkPoint != null ? _sparkPoint.position : _hammer.position;
        Quaternion rotation = _sparkPoint != null ? _sparkPoint.rotation : transform.rotation;
        ProductionSparkPlayer.Play(_sparkPrefab, point, rotation, _sparkScale);
        PlayStrike();
    }

    private void ApplyPose(float t)
    {
        if (_circlePositive != null)
            _circlePositive.localRotation = _circlePositiveRest * Quaternion.Euler(0f, 0f, _gearAngle * t);

        if (_circleNegative != null)
            _circleNegative.localRotation = _circleNegativeRest * Quaternion.Euler(0f, 0f, -_gearAngle * t);

        if (_hammer != null)
            _hammer.localRotation = _hammerRest * Quaternion.Euler(0f, 0f, _hammerAngle * t);
    }
}
