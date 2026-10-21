using UnityEngine;

public class HexMapRoot : MonoBehaviour
{
    [SerializeField] private float _hexSize = 1f;
    [SerializeField] private Transform _ground;
    [SerializeField] private Transform _environment;
    [SerializeField] private Transform _resources;
    [SerializeField] private Transform _gameplay;

    public float HexSize => _hexSize;

    public Transform Ground => GetOrCreate(ref _ground, "Ground");
    public Transform Environment => GetOrCreate(ref _environment, "Environment");
    public Transform Resources => GetOrCreate(ref _resources, "Resources");
    public Transform Gameplay => GetOrCreate(ref _gameplay, "Gameplay");

    public Transform GetContainer(HexMapPaintMode mode)
    {
        return mode switch
        {
            HexMapPaintMode.Ground => Ground,
            HexMapPaintMode.Environment => Environment,
            HexMapPaintMode.ResourceHolder => Resources,
            HexMapPaintMode.RewardedResourceSpot => Gameplay,
            HexMapPaintMode.InstrumentUpgrader => Gameplay,
            HexMapPaintMode.OpeningHex => Gameplay,
            HexMapPaintMode.EnemySpawnPoint => Gameplay,
            HexMapPaintMode.OneTimeEnemySpawnPoint => Gameplay,
            _ => transform
        };
    }

    private Transform GetOrCreate(ref Transform container, string name)
    {
        if (container == null)
        {
            var existing = transform.Find(name);
            if (existing != null)
                container = existing;
            else
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                container = go.transform;
            }
        }

        return container;
    }

#if UNITY_EDITOR
    public void EnsureContainers()
    {
        _ = Ground;
        _ = Environment;
        _ = Resources;
        _ = Gameplay;
    }
#endif
}
