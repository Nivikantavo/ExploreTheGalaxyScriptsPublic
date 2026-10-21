using UnityEngine;

[CreateAssetMenu(fileName = "WaterHazardConfig", menuName = "Scriptable Objects/WaterHazardConfig")]
public class WaterHazardConfig : ScriptableObject
{
    public float MaxTimeInWaterSeconds => _maxTimeInWaterSeconds;
    public float WarningLeadTimeSeconds => Mathf.Min(_warningLeadTimeSeconds, _maxTimeInWaterSeconds);
    public GameObject WarningOrbitPrefab => _warningOrbitPrefab;
    public int WarningOrbitCount => _warningOrbitCount;
    public float OrbitRadius => _orbitRadius;
    public float OrbitHeight => _orbitHeight;
    public float OrbitSpeedDegreesPerSecond => _orbitSpeedDegreesPerSecond;
    public GameObject DrownDeathVfxPrefab => _drownDeathVfxPrefab;
    public Vector3 DrownVfxOffset => _drownVfxOffset;

    [Header("Drowning")]
    [SerializeField, Min(0.1f)] private float _maxTimeInWaterSeconds = 10f;
    [SerializeField, Min(0f)] private float _warningLeadTimeSeconds = 3f;

    [Header("Warning Orbit (sharks)")]
    [SerializeField] private GameObject _warningOrbitPrefab;
    [SerializeField, Min(1)] private int _warningOrbitCount = 3;
    [SerializeField, Min(0.1f)] private float _orbitRadius = 2.5f;
    [SerializeField] private float _orbitHeight;
    [SerializeField] private float _orbitSpeedDegreesPerSecond = 120f;

    [Header("Death VFX")]
    [SerializeField] private GameObject _drownDeathVfxPrefab;
    [SerializeField] private Vector3 _drownVfxOffset;
}
