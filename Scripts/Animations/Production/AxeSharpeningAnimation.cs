using PrimeTween;
using UnityEngine;

public class AxeSharpeningAnimation : ProductionAnimationBehaviour
{
    [SerializeField] private Transform _wheel;
    [SerializeField] private string _wheelName = "Cylinder.009";
    [SerializeField] private Transform _contactPoint;
    [SerializeField] private Transform _awayPoint;
    [SerializeField] private Transform _sparkPoint;
    [SerializeField] private GameObject _sparkPrefab;
    [SerializeField] private float _sparkScale = 0.4f;
    [SerializeField] private float _spinDuration = 0.28f;
    [SerializeField] private float _strokeDuration = 0.35f;
    [SerializeField] private float _axeScale = 0.55f;
    [SerializeField] private Vector3 _axeEuler = new Vector3(0f, 90f, 70f);

    private Quaternion _wheelRestRotation;
    private Tween _spinTween;
    private Sequence _stroke;
    private GameObject _axeInstance;
    private InstrumentUpgrader _upgrader;

    protected override void OnAnimationAwake()
    {
        _upgrader = GetComponent<InstrumentUpgrader>();
        if (_wheel == null)
            _wheel = ProductionWorkGate.FindDeepChild(transform, _wheelName);

        if (_wheel != null)
            _wheelRestRotation = _wheel.localRotation;
    }

    protected override void OnWorkStarted()
    {
        if (_wheel == null)
        {
            Debug.LogWarning($"{name}: Cylinder.009 was not found for axe sharpening.", this);
            return;
        }

        Vector3 startEuler = _wheel.localEulerAngles;
        _spinTween = Tween.LocalEulerAngles(
            _wheel,
            startEuler,
            startEuler + new Vector3(0f, 0f, 360f),
            _spinDuration,
            Ease.Linear,
            cycles: -1,
            cycleMode: CycleMode.Incremental);

        SpawnAxe();
        PlayStroke();
    }

    protected override void OnWorkStopped()
    {
        if (_spinTween.isAlive)
            _spinTween.Stop();

        if (_stroke.isAlive)
            _stroke.Stop();

        if (_wheel != null)
            _wheel.localRotation = _wheelRestRotation;

        if (_axeInstance != null)
        {
            Destroy(_axeInstance);
            _axeInstance = null;
        }
    }

    private void SpawnAxe()
    {
        if (_axeInstance != null)
            Destroy(_axeInstance);

        GameObject prefab = _upgrader != null ? _upgrader.CurrentUpgradePrefab : null;
        if (prefab == null)
            return;

        _axeInstance = Instantiate(prefab, transform);
        _axeInstance.transform.localScale = Vector3.one * _axeScale;
        _axeInstance.transform.localRotation = Quaternion.Euler(_axeEuler);
        _axeInstance.transform.localPosition = ContactLocalPosition();

        Collider[] colliders = _axeInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void PlayStroke()
    {
        if (!IsPlaying || _axeInstance == null)
            return;

        Vector3 contact = ContactLocalPosition();
        Vector3 away = AwayLocalPosition();
        _axeInstance.transform.localPosition = contact;

        _stroke = Sequence.Create()
            .Chain(Tween.LocalPosition(_axeInstance.transform, contact, away, _strokeDuration, Ease.InOutSine))
            .Chain(Tween.LocalPosition(_axeInstance.transform, away, contact, _strokeDuration, Ease.InOutSine))
            .ChainCallback(this, animation => animation.OnStrokeContact());
    }

    private void OnStrokeContact()
    {
        if (!IsPlaying)
            return;

        Vector3 point = _sparkPoint != null ? _sparkPoint.position : _wheel.position;
        Quaternion rotation = _sparkPoint != null ? _sparkPoint.rotation : transform.rotation;
        ProductionSparkPlayer.Play(_sparkPrefab, point, rotation, _sparkScale);
        PlayStroke();
    }

    private Vector3 ContactLocalPosition()
    {
        if (_contactPoint != null)
            return transform.InverseTransformPoint(_contactPoint.position);

        if (_wheel == null)
            return Vector3.zero;

        return _wheel.localPosition + new Vector3(0.16f, -0.08f, 0.04f);
    }

    private Vector3 AwayLocalPosition()
    {
        if (_awayPoint != null)
            return transform.InverseTransformPoint(_awayPoint.position);

        if (_wheel == null)
            return new Vector3(0.16f, 0.12f, 0.04f);

        return _wheel.localPosition + new Vector3(0.22f, 0.14f, 0.04f);
    }
}
