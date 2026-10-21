using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class ResoursePickupPopupPool
{
    private const int DefaultCapacity = 8;
    private const int MaxSize = 64;
    private static readonly Dictionary<GameObject, IObjectPool<GameObject>> pools = new();

    public static GameObject SpawnAndPlay(
        GameObject prefab,
        Vector3 spawnPosition,
        Sprite icon,
        string info,
        Camera camera = null)
    {
        if (prefab == null)
            return null;

        if (!PersistentEffectPoolRoot.TryTake(pools, prefab, DefaultCapacity, MaxSize, out var pool, out var go))
            return null;

        go.transform.position = spawnPosition;
        go.transform.rotation = Quaternion.identity;

        if (go.TryGetComponent(out PopapView view))
            view.Setup(icon, info);

        var tween = go.GetComponent<ResoursePickupPopupTween>();
        if (tween == null)
            tween = go.AddComponent<ResoursePickupPopupTween>();

        tween.Play(
            fromPosition: spawnPosition,
            release: () => pool.Release(go),
            camera: camera != null ? camera : Camera.main
        );

        return go;
    }
}
