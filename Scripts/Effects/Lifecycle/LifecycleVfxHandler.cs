using UnityEngine;
using Zenject;

public class LifecycleVfxHandler : MonoBehaviour
{
    [SerializeField] private GameObject _spawnVfxPrefab;
    [SerializeField] private GameObject _deathVfxPrefab;
    [SerializeField] private Vector3 _positionOffset;

    private LifecycleEventBus _eventBus;

    [Inject]
    private void Construct(LifecycleEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    private void OnEnable()
    {
        _eventBus.OnEvent += HandleLifecycleEvent;
    }

    private void OnDisable()
    {
        _eventBus.OnEvent -= HandleLifecycleEvent;
    }

    private void HandleLifecycleEvent(LifecycleEventData data)
    {
        var prefab = GetPrefab(data.Type);
        if (prefab == null)
            return;

        LifecycleVfxPool.SpawnAndPlay(
            prefab: prefab,
            position: data.Position + _positionOffset,
            rotation: Quaternion.identity
        );
    }

    private GameObject GetPrefab(LifecycleEventType type)
    {
        return type switch
        {
            LifecycleEventType.PlayerSpawned or LifecycleEventType.EnemySpawned => _spawnVfxPrefab,
            LifecycleEventType.PlayerDied or LifecycleEventType.EnemyDied => _deathVfxPrefab,
            _ => null,
        };
    }
}
