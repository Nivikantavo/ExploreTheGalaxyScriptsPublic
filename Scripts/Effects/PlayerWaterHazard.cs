using System.Collections.Generic;
using UnityEngine;

public class PlayerWaterHazard : MonoBehaviour
{
    public bool IsInWater => _waterOverlapCount > 0;
    public float TimeInWaterSeconds => _timeInWaterSeconds;

    private Player _player;
    private WaterHazardConfig _config;
    private readonly List<GameObject> _orbitObjects = new();

    private int _waterOverlapCount;
    private float _timeInWaterSeconds;
    private float _orbitAngle;
    private bool _warningActive;
    private bool _deathHandled;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void OnDestroy()
    {
        ClearOrbitObjects();
    }

    public void EnterWater(WaterHazardConfig config)
    {
        if (config == null || _player == null || _player.IsDead)
            return;

        _config = config;
        _waterOverlapCount++;
        _deathHandled = false;
        enabled = true;
    }

    public void ExitWater()
    {
        _waterOverlapCount = Mathf.Max(0, _waterOverlapCount - 1);
        if (_waterOverlapCount > 0)
            return;

        ResetState();
        enabled = false;
    }

    private void Update()
    {
        if (_player == null || _player.IsDead || _config == null || _waterOverlapCount <= 0)
            return;

        _timeInWaterSeconds += Time.deltaTime;

        float warningAt = _config.MaxTimeInWaterSeconds - _config.WarningLeadTimeSeconds;
        if (!_warningActive && _timeInWaterSeconds >= warningAt)
            BeginWarning();

        if (_warningActive)
            UpdateOrbitObjects();

        if (!_deathHandled && _timeInWaterSeconds >= _config.MaxTimeInWaterSeconds)
            KillByDrowning();
    }

    private void BeginWarning()
    {
        _warningActive = true;
        _orbitAngle = 0f;
        SpawnOrbitObjects();
    }

    private void SpawnOrbitObjects()
    {
        ClearOrbitObjects();

        if (_config.WarningOrbitPrefab == null || _config.WarningOrbitCount <= 0)
            return;

        for (int i = 0; i < _config.WarningOrbitCount; i++)
        {
            GameObject orbitObject = Instantiate(_config.WarningOrbitPrefab);
            _orbitObjects.Add(orbitObject);
        }

        UpdateOrbitObjects();
    }

    private void UpdateOrbitObjects()
    {
        if (_orbitObjects.Count == 0)
            return;

        _orbitAngle += _config.OrbitSpeedDegreesPerSecond * Time.deltaTime;
        Vector3 center = transform.position + Vector3.up * _config.OrbitHeight;
        float step = 360f / _orbitObjects.Count;

        for (int i = 0; i < _orbitObjects.Count; i++)
        {
            GameObject orbitObject = _orbitObjects[i];
            if (orbitObject == null)
                continue;

            float angleDegrees = _orbitAngle + step * i;
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angleRadians), 0f, Mathf.Sin(angleRadians)) * _config.OrbitRadius;
            orbitObject.transform.position = center + offset;

            Vector3 tangent = new Vector3(-Mathf.Sin(angleRadians), 0f, Mathf.Cos(angleRadians));
            if (tangent.sqrMagnitude > 0.0001f)
                orbitObject.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }

    private void KillByDrowning()
    {
        _deathHandled = true;
        PlayDrownVfx();
        ClearOrbitObjects();
        _warningActive = false;
        _player.DieFromDrowning();
    }

    private void PlayDrownVfx()
    {
        if (_config == null || _config.DrownDeathVfxPrefab == null)
            return;

        LifecycleVfxPool.SpawnAndPlay(
            prefab: _config.DrownDeathVfxPrefab,
            position: transform.position + _config.DrownVfxOffset,
            rotation: Quaternion.identity
        );
    }

    private void ResetState()
    {
        _waterOverlapCount = 0;
        _timeInWaterSeconds = 0f;
        _orbitAngle = 0f;
        _warningActive = false;
        _deathHandled = false;
        ClearOrbitObjects();
    }

    private void ClearOrbitObjects()
    {
        for (int i = 0; i < _orbitObjects.Count; i++)
        {
            if (_orbitObjects[i] != null)
                Destroy(_orbitObjects[i]);
        }

        _orbitObjects.Clear();
    }
}
