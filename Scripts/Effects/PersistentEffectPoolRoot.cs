using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class PersistentEffectPoolRoot
{
    private static Transform _root;

    public static Transform Root
    {
        get
        {
            if (_root != null)
                return _root;

            var go = new GameObject(nameof(PersistentEffectPoolRoot));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
            return _root;
        }
    }

    public static bool TryTake(
        Dictionary<GameObject, IObjectPool<GameObject>> pools,
        GameObject prefab,
        int defaultCapacity,
        int maxSize,
        out IObjectPool<GameObject> pool,
        out GameObject instance)
    {
        pool = GetOrCreate(pools, prefab, defaultCapacity, maxSize);
        instance = pool.Get();
        if (instance != null)
            return true;

        pools.Remove(prefab);
        pool.Clear();

        pool = GetOrCreate(pools, prefab, defaultCapacity, maxSize);
        instance = pool.Get();
        return instance != null;
    }

    private static IObjectPool<GameObject> GetOrCreate(
        Dictionary<GameObject, IObjectPool<GameObject>> pools,
        GameObject prefab,
        int defaultCapacity,
        int maxSize)
    {
        if (pools.TryGetValue(prefab, out var pool))
            return pool;

        pool = new ObjectPool<GameObject>(
            createFunc: () => Object.Instantiate(prefab, Root),
            actionOnGet: Activate,
            actionOnRelease: Deactivate,
            actionOnDestroy: DestroyInstance,
            collectionCheck: false,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        pools[prefab] = pool;
        return pool;
    }

    private static void Activate(GameObject go)
    {
        if (go != null)
            go.SetActive(true);
    }

    private static void Deactivate(GameObject go)
    {
        if (go != null)
            go.SetActive(false);
    }

    private static void DestroyInstance(GameObject go)
    {
        if (go != null)
            Object.Destroy(go);
    }
}
