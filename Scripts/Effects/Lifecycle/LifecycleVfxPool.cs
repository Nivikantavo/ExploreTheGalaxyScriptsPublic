using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class LifecycleVfxPool
{
    private const int DefaultCapacity = 8;
    private const int MaxSize = 64;
    private static readonly Dictionary<GameObject, IObjectPool<GameObject>> pools = new();

    public static GameObject SpawnAndPlay(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (!PersistentEffectPoolRoot.TryTake(pools, prefab, DefaultCapacity, MaxSize, out var pool, out var go))
            return null;

        go.transform.SetPositionAndRotation(position, rotation);

        if (!go.TryGetComponent(out PooledVfxPlayer player))
            player = go.AddComponent<PooledVfxPlayer>();

        player.Play(() => pool.Release(go));
        return go;
    }
}
