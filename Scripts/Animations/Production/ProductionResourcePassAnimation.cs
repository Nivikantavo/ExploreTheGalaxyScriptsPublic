using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

public class ProductionResourcePassAnimation : ProductionAnimationBehaviour
{
    [SerializeField] private bool _spin;
    [SerializeField] private Transform _spinner;
    [SerializeField] private string _spinnerName = "Circle.003";
    [SerializeField] private float _spinDuration = 0.12f;
    [SerializeField] private bool _pulseScale;
    [SerializeField] private float _pulseMultiplier = 1.08f;
    [SerializeField] private float _pulseDuration = 0.4f;
    [SerializeField] private GameObject _inputPrefab;
    [SerializeField] private GameObject _outputPrefab;
    [SerializeField] private float _visualScale = 0.35f;
    [SerializeField] private Transform _entry;
    [SerializeField] private Transform _exit;
    [SerializeField] private float _passDuration = 1.1f;

    private Quaternion _spinnerRestRotation;
    private Tween _spinTween;
    private Tween _pulseTween;
    private Tween _passTween;
    private Transform[] _scaleTargets = System.Array.Empty<Transform>();
    private Vector3[] _restScales = System.Array.Empty<Vector3>();
    private GameObject _visual;
    private Transform _hiddenHost;

    protected override void OnAnimationAwake()
    {
        if (_spin && _spinner == null)
            _spinner = ProductionWorkGate.FindDeepChild(transform, _spinnerName);

        if (_spinner != null)
            _spinnerRestRotation = _spinner.localRotation;

        if (_pulseScale)
            CacheScaleTargets();
    }

    protected override void OnWorkStarted()
    {
        StartSpin();
        StartPulse();
        BeginPass();
    }

    protected override void OnWorkStopped()
    {
        if (_spinTween.isAlive)
            _spinTween.Stop();

        if (_pulseTween.isAlive)
            _pulseTween.Stop();

        if (_passTween.isAlive)
            _passTween.Stop();

        if (_spinner != null)
            _spinner.localRotation = _spinnerRestRotation;

        RestoreScales();
        DestroyVisual();
    }

    private void StartSpin()
    {
        if (!_spin || _spinner == null)
            return;

        Vector3 startEuler = _spinner.localEulerAngles;
        _spinTween = Tween.LocalEulerAngles(
            _spinner,
            startEuler,
            startEuler + new Vector3(0f, 0f, 360f),
            _spinDuration,
            Ease.Linear,
            cycles: -1,
            cycleMode: CycleMode.Incremental);
    }

    private void StartPulse()
    {
        if (!_pulseScale || _scaleTargets.Length == 0)
            return;

        _pulseTween = Tween.Custom(
            this,
            0f,
            1f,
            _pulseDuration,
            onValueChange: (self, t) => self.ApplyPulse(t),
            ease: Ease.InOutSine,
            cycles: -1,
            cycleMode: CycleMode.Yoyo);
    }

    private void BeginPass()
    {
        if (!IsPlaying || _inputPrefab == null || _outputPrefab == null)
            return;

        Vector3 entry = EntryPosition();
        Vector3 gate = GatePosition();
        _visual = SpawnVisual(_inputPrefab, entry);
        if (_visual == null)
            return;

        float halfDuration = Mathf.Max(0.05f, _passDuration * 0.5f);
        _passTween = Tween.Position(_visual.transform, entry, gate, halfDuration, Ease.Linear)
            .OnComplete(this, animation => animation.OnReachedGate());
    }

    private void OnReachedGate()
    {
        if (!IsPlaying)
            return;

        Vector3 gate = GatePosition();
        Vector3 exit = ExitPosition();
        DestroyVisual();
        _visual = SpawnVisual(_outputPrefab, gate);
        if (_visual == null)
            return;

        float halfDuration = Mathf.Max(0.05f, _passDuration * 0.5f);
        _passTween = Tween.Position(_visual.transform, gate, exit, halfDuration, Ease.Linear)
            .OnComplete(this, animation => animation.OnReachedExit());
    }

    private void OnReachedExit()
    {
        if (!IsPlaying)
            return;

        DestroyVisual();
        BeginPass();
    }

    private GameObject SpawnVisual(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, HiddenHost);
        StripGameplay(instance);

        Vector3 entry = EntryPosition();
        Vector3 exit = ExitPosition();
        Vector3 travel = exit - entry;
        Quaternion rotation = travel.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(travel.normalized, Vector3.up)
            : transform.rotation;

        instance.transform.SetParent(transform, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one * _visualScale;
        instance.SetActive(true);
        return instance;
    }

    private void DestroyVisual()
    {
        if (_visual == null)
            return;

        Destroy(_visual);
        _visual = null;
    }

    private Vector3 EntryPosition()
    {
        if (_entry != null)
            return _entry.position;

        return GatePosition() - transform.forward * 0.35f;
    }

    private Vector3 ExitPosition()
    {
        if (_exit != null)
            return _exit.position;

        return GatePosition() + transform.forward * 0.35f;
    }

    private Vector3 GatePosition()
    {
        if (_spinner != null)
            return _spinner.position;

        if (_entry != null && _exit != null)
            return Vector3.Lerp(_entry.position, _exit.position, 0.5f);

        return transform.position + Vector3.up * 0.4f;
    }

    private Transform HiddenHost
    {
        get
        {
            if (_hiddenHost == null)
            {
                var hostObject = new GameObject("HiddenVisualHost");
                hostObject.SetActive(false);
                _hiddenHost = hostObject.transform;
                _hiddenHost.SetParent(transform, false);
            }

            return _hiddenHost;
        }
    }

    private void CacheScaleTargets()
    {
        var targets = new List<Transform>();
        var seen = new HashSet<Transform>();
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Transform directChild = GetDirectChild(renderers[i].transform);
            if (directChild == null || directChild == transform)
                continue;

            if (directChild.GetComponent<RectTransform>() != null || directChild.GetComponent<Canvas>() != null)
                continue;

            if (seen.Add(directChild))
                targets.Add(directChild);
        }

        _scaleTargets = targets.ToArray();
        _restScales = new Vector3[_scaleTargets.Length];
        for (int i = 0; i < _scaleTargets.Length; i++)
            _restScales[i] = _scaleTargets[i].localScale;
    }

    private Transform GetDirectChild(Transform target)
    {
        while (target.parent != null && target.parent != transform)
            target = target.parent;

        return target.parent == transform ? target : null;
    }

    private void ApplyPulse(float t)
    {
        float multiplier = Mathf.Lerp(1f, _pulseMultiplier, t);
        for (int i = 0; i < _scaleTargets.Length; i++)
        {
            if (_scaleTargets[i] != null)
                _scaleTargets[i].localScale = _restScales[i] * multiplier;
        }
    }

    private void RestoreScales()
    {
        for (int i = 0; i < _scaleTargets.Length; i++)
        {
            if (_scaleTargets[i] != null)
                _scaleTargets[i].localScale = _restScales[i];
        }
    }

    private static void StripGameplay(GameObject instance)
    {
        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                DestroyImmediate(behaviours[i]);
        }

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                DestroyImmediate(colliders[i]);
        }

        Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
                canvases[i].gameObject.SetActive(false);
        }

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            if (particles[i].GetComponent<MeshRenderer>() == null)
                particles[i].gameObject.SetActive(false);
            else
                DestroyImmediate(particles[i]);
        }

        Light[] lights = instance.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = false;
        }
    }
}
