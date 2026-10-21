using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HexMapPalette", menuName = "Hex Map/Palette")]
public class HexMapPalette : ScriptableObject
{
    [Header("Grid")]
    [SerializeField] private float _hexSize = 1f;
    [SerializeField] private float _environmentHeight = 0.25f;
    [SerializeField] private float _environmentRandomOffset = 0.3f;

    [Header("Ground")]
    [SerializeField] private List<GameObject> _groundPrefabs = new();

    [Header("Environment")]
    [SerializeField] private List<GameObject> _environmentPrefabs = new();

    [Header("Gameplay")]
    [SerializeField] private List<GameObject> _resourceHolderPrefabs = new();
    [SerializeField] private List<GameObject> _rewardedResourceSpotPrefabs = new();
    [SerializeField] private List<GameObject> _instrumentUpgraderPrefabs = new();
    [SerializeField] private List<GameObject> _enemySpawnPointPrefabs = new();
    [SerializeField] private GameObject _openingHexPrefab;

    public float HexSize => _hexSize;
    public float EnvironmentHeight => _environmentHeight;
    public float EnvironmentRandomOffset => _environmentRandomOffset;
    public IReadOnlyList<GameObject> GroundPrefabs => _groundPrefabs;
    public IReadOnlyList<GameObject> EnvironmentPrefabs => _environmentPrefabs;
    public IReadOnlyList<GameObject> ResourceHolderPrefabs => _resourceHolderPrefabs;
    public IReadOnlyList<GameObject> RewardedResourceSpotPrefabs => _rewardedResourceSpotPrefabs;
    public IReadOnlyList<GameObject> InstrumentUpgraderPrefabs => _instrumentUpgraderPrefabs;
    public IReadOnlyList<GameObject> EnemySpawnPointPrefabs => _enemySpawnPointPrefabs;
    public GameObject OpeningHexPrefab => _openingHexPrefab;
}
