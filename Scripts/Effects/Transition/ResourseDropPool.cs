using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class ResourseDropPool {
    private const int DefaultCapacity = 16;
    private const int MaxSize = 512;
    private static readonly Dictionary<GameObject, IObjectPool<GameObject>> pools = new();

    public static GameObject SpawnAndPlay(GameObject prefab, Vector3 spawnPosition, Transform pickupTarget) {
        if (prefab == null) {
            return null;
        }

        if (!PersistentEffectPoolRoot.TryTake(pools, prefab, DefaultCapacity, MaxSize, out var pool, out var go))
            return null;

        go.transform.position = spawnPosition;
        go.transform.rotation = Quaternion.identity;

        var anim = go.GetComponent<ResourseDropTween>();
        if (anim == null) {
            anim = go.AddComponent<ResourseDropTween>();
        }

        anim.Play(
            fromPosition: spawnPosition,
            pickupTarget: pickupTarget,
            release: () => pool.Release(go)
        );

        return go;
    }
}
