using UnityEngine;

public class HitActivatedSpawnPoint : MonoBehaviour
{
    [SerializeField, Tooltip("Одноразовая точка спавна. Если пусто — ищется в детях, включая неактивные.")]
    private EnemySpawnPoint _spawnPoint;

    private IHitAnimated _hitAnimated;
    private bool _activated;

    private void OnValidate()
    {
        if (_spawnPoint == null)
            _spawnPoint = GetComponentInChildren<EnemySpawnPoint>(true);
    }

    private void Awake()
    {
        _hitAnimated = GetComponentInParent<IHitAnimated>();
        if (_spawnPoint == null)
            _spawnPoint = GetComponentInChildren<EnemySpawnPoint>(true);

        if (_spawnPoint != null && !_spawnPoint.IsCleared)
            _spawnPoint.RequireActivation();
    }

    private void OnEnable()
    {
        if (_activated || _hitAnimated == null)
            return;

        _hitAnimated.OnHit += OnHit;
    }

    private void OnDisable()
    {
        if (_hitAnimated == null)
            return;

        _hitAnimated.OnHit -= OnHit;
    }

    private void OnHit()
    {
        if (_activated)
            return;

        _activated = true;

        if (_hitAnimated != null)
            _hitAnimated.OnHit -= OnHit;

        if (_spawnPoint == null || _spawnPoint.IsCleared)
            return;

        _spawnPoint.Activate();
    }
}
