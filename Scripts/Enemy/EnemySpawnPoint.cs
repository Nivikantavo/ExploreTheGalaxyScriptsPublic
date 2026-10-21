using System;
using System.Collections;
using UnityEngine;
using Zenject;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private EnemyInitConfig _enemyConfig;
    [SerializeField] private float _respawnTime = 5f;
    [SerializeField] private float _enemyGuardRadius;
    [SerializeField] private float _spawnTriggerOffset = 5f;
    [SerializeField, Tooltip("Враг появляется один раз. После убийства точка больше не спавнит.")]
    private bool _isOneTime;
    [SerializeField, Tooltip("Стабильный id для сохранения. Если пусто — генерируется для инстанса в сцене.")]
    private string _saveId;

    private Enemy _enemy;
    private WaitForSeconds _respawnInterval;
    private LifecycleEventBus _lifecycleEventBus;
    private Player _player;
    private bool _hasSpawned;
    private bool _cleared;
    private bool _requiresActivation;
    private bool _isActivated;
    private bool _respawnPending;

    public event Action Cleared;

    public float GuardRadius => _enemyGuardRadius;
    public bool IsOneTime => _isOneTime;
    public bool IsCleared => _cleared;
    public bool IsActivated => _isActivated;

    [Inject]
    private void Construct(LifecycleEventBus lifecycleEventBus, Player player)
    {
        _lifecycleEventBus = lifecycleEventBus;
        _player = player;
    }

    private void OnValidate()
    {
        _respawnTime = Mathf.Max(0f, _respawnTime);
        _enemyGuardRadius = Mathf.Max(0f, _enemyGuardRadius);
        _spawnTriggerOffset = Mathf.Max(0f, _spawnTriggerOffset);
        AssignSaveIdIfEmpty();
    }

    private void Start()
    {
        _respawnInterval = new(_respawnTime);
    }

    private void OnEnable()
    {
        if (!_respawnPending || _cleared || _isOneTime)
            return;

        StartCoroutine(RespawnCoroutone());
    }

    private void Update()
    {
        TrySpawnIfReady();
    }

    public void RequireActivation()
    {
        _requiresActivation = true;
    }

    public void Activate()
    {
        if (_cleared)
            return;

        _isActivated = true;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!enabled)
            enabled = true;

        TrySpawnIfReady();
    }

    private void OnDestroy()
    {
        UnsubscribeEnemyDied();
    }

    public string GetSaveId()
    {
        if (!string.IsNullOrEmpty(_saveId))
            return _saveId;

        HexMapPlacedObject placed = GetComponent<HexMapPlacedObject>()
            ?? GetComponentInParent<HexMapPlacedObject>(true);
        IslandProgressHandler island = GetComponentInParent<IslandProgressHandler>(true);
        if (placed != null && island != null)
            return $"{island.IslandIndex}:{placed.Q}:{placed.R}:Enemy";

        return gameObject.name;
    }

    public bool AssignSaveIdIfEmpty()
    {
#if UNITY_EDITOR
        if (!_isOneTime || !string.IsNullOrEmpty(_saveId) || Application.isPlaying)
            return false;

        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            return false;

        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
            return false;

        _saveId = Guid.NewGuid().ToString("N");
        return true;
#else
        return false;
#endif
    }

    public void ApplyClearedFromSave()
    {
        _cleared = true;
        _hasSpawned = true;
        _respawnPending = false;
        StopAllCoroutines();

        if (_enemy == null)
            return;

        UnsubscribeEnemyDied();
        Destroy(_enemy.gameObject);
        _enemy = null;
    }

    private void TrySpawnIfReady()
    {
        if (_cleared || _hasSpawned || _player == null || !_player.gameObject.activeInHierarchy)
            return;

        if (_requiresActivation && !_isActivated)
            return;

        if (IsPlayerInSpawnTrigger())
            SpawnInitialEnemy();
    }

    private bool IsPlayerInSpawnTrigger()
    {
        float triggerRadius = _enemyGuardRadius + _spawnTriggerOffset;
        float radiusSqr = triggerRadius * triggerRadius;
        return (transform.position - _player.transform.position).sqrMagnitude <= radiusSqr;
    }

    private void SpawnInitialEnemy()
    {
        if (_enemyConfig == null || _enemyConfig.EnemyPrefab == null)
            return;

        _hasSpawned = true;
        _enemy = SpawnEnemy();
        _enemy.EnemyDied += OnEnemyDied;
        PublishEnemySpawned();
    }

    private Enemy SpawnEnemy()
    {
        // Keep the instance under the spawn point so hiding the host island also freezes it.
        var enemy = Instantiate(_enemyConfig.EnemyPrefab, transform.position, Quaternion.identity, transform);
        enemy.Initialize(_enemyConfig.MaxHealth, _enemyConfig.InstrumentConfig, transform, _enemyGuardRadius, _enemyConfig.RecourseConfig, _enemyConfig.ResourceCount);
        return enemy;
    }

    private void OnEnemyDied()
    {
        if (_isOneTime)
        {
            _cleared = true;
            Cleared?.Invoke();
        }

        _lifecycleEventBus.Publish(new LifecycleEventData(LifecycleEventType.EnemyDied, _enemy.transform.position));

        if (_isOneTime)
            return;

        _respawnPending = true;
        StartCoroutine(RespawnCoroutone());
    }

    private IEnumerator RespawnCoroutone()
    {
        yield return _respawnInterval;
        _respawnPending = false;
        if (_cleared || _enemy == null)
            yield break;

        _enemy.gameObject.SetActive(true);
        _enemy.Reset();
        _enemy.PlaceAt(transform.position, Quaternion.identity);
        PublishEnemySpawned();
    }

    private void PublishEnemySpawned()
    {
        _lifecycleEventBus.Publish(new LifecycleEventData(LifecycleEventType.EnemySpawned, _enemy.transform.position));
    }

    private void UnsubscribeEnemyDied()
    {
        if (_enemy == null)
            return;

        _enemy.EnemyDied -= OnEnemyDied;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _enemyGuardRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _enemyGuardRadius + _spawnTriggerOffset);
    }
}
