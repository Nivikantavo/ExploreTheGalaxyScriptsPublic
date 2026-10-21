using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zenject;

/// <summary>
/// Only hex unlocks that overlap an <see cref="EnemySpawnPoint"/> guard radius trigger a rebuild;
/// forced rebuilds (save load / full island open) still run unconditionally.
/// Uses async <see cref="NavMeshSurface.UpdateNavMesh"/> when possible to avoid main-thread freezes.
/// Dungeon travel keeps the host mesh data and swaps the active instance for a mesh baked from the dungeon scene.
/// </summary>
public class DynamicNavMeshRebuilder : MonoBehaviour
{
    private enum NavMeshScope
    {
        Host,
        Dungeon
    }

    private const float DefaultRebuildDelaySeconds = 0.15f;
    private const float DefaultFallbackHexRadius = 5f;
    private const float DefaultRelevancePadding = 0.5f;

    [SerializeField] private float _rebuildDelaySeconds = DefaultRebuildDelaySeconds;
    [SerializeField] private float _relevancePadding = DefaultRelevancePadding;
    [SerializeField] private float _fallbackHexRadius = DefaultFallbackHexRadius;
    [SerializeField] private NavMeshSurface _surface;

    private IslandProgressRegistry _islandRegistry;
    private readonly List<EnemySpawnPoint> _spawnPoints = new();
    private Coroutine _pendingRebuild;
    private bool _subscribed;
    private bool _rebuildInProgress;
    private NavMeshScope _scope = NavMeshScope.Host;
    private Scene _dungeonScene;
    private NavMeshData _dungeonNavMeshData;
    private NavMeshDataInstance _dungeonNavMeshInstance;

    [Inject]
    private void Construct(IslandProgressRegistry islandRegistry)
    {
        _islandRegistry = islandRegistry;
    }

    private void Start()
    {
        EnsureSurface();
        RefreshSpawnPoints();
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (_pendingRebuild != null)
            StopCoroutine(_pendingRebuild);

        DestroyDungeonNavMesh();
    }

    /// <summary>
    /// Waits until a queued or running rebuild finishes. Call before hiding the host world
    /// so an in-flight host bake cannot capture dungeon geometry.
    /// </summary>
    public IEnumerator WaitUntilIdle()
    {
        while (_pendingRebuild != null || _rebuildInProgress)
            yield return null;
    }

    /// <summary>
    /// Drops the host NavMesh from the navigation world without discarding its data,
    /// and blocks further host bakes until <see cref="BeginHost"/>.
    /// </summary>
    public void PrepareDungeonTransition()
    {
        _scope = NavMeshScope.Dungeon;
        _dungeonScene = default;
        EnsureSurface();
        _surface?.RemoveData();
        DestroyDungeonNavMesh();
    }

    public void BeginDungeon(Scene scene)
    {
        _scope = NavMeshScope.Dungeon;
        _dungeonScene = scene;
        EnsureSurface();
        _surface?.RemoveData();
        DestroyDungeonNavMesh();
        RefreshSpawnPoints();
    }

    /// <summary>
    /// Removes the dungeon NavMesh and restores the cached host NavMesh.
    /// </summary>
    public void BeginHost()
    {
        _scope = NavMeshScope.Host;
        _dungeonScene = default;
        DestroyDungeonNavMesh();
        EnsureSurface();
        RefreshSpawnPoints();

        if (_surface != null && _surface.navMeshData != null)
            _surface.AddData();
        else
            RequestRebuild();
    }

    public void SnapAgentsToNavMesh()
    {
        RepositionAgentsOnNavMesh();
    }

    /// <summary>
    /// Rebuilds the active scope immediately. Cancels a debounced rebuild so scene travel
    /// does not wait out the unlock delay.
    /// </summary>
    public IEnumerator RebuildNow()
    {
        if (_pendingRebuild != null)
        {
            StopCoroutine(_pendingRebuild);
            _pendingRebuild = null;
        }

        while (_rebuildInProgress)
            yield return null;

        yield return RebuildRoutine();
    }

    /// <summary>
    /// Queues a debounced NavMesh rebuild. Safe to call many times in a row
    /// (e.g. after loading multiple unlocked hexes from save).
    /// </summary>
    public void RequestRebuild()
    {
        if (!isActiveAndEnabled)
            return;

        if (_pendingRebuild != null)
            StopCoroutine(_pendingRebuild);

        _pendingRebuild = StartCoroutine(RebuildAfterDelay());
    }

    private IEnumerator RebuildAfterDelay()
    {
        if (_rebuildDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(_rebuildDelaySeconds);
        else
            yield return null;

        _pendingRebuild = null;

        // Coalesce requests that arrive while a rebuild is already running.
        while (_rebuildInProgress)
            yield return null;

        yield return RebuildRoutine();
    }

    private IEnumerator RebuildRoutine()
    {
        EnsureSurface();
        if (_surface == null)
        {
            Debug.LogWarning($"{nameof(DynamicNavMeshRebuilder)}: NavMeshSurface is missing.");
            yield break;
        }

        _rebuildInProgress = true;
        try
        {
            if (_scope == NavMeshScope.Dungeon)
                yield return RebuildDungeonRoutine();
            else
                yield return RebuildHostRoutine();
        }
        finally
        {
            _rebuildInProgress = false;
        }
    }

    private IEnumerator RebuildHostRoutine()
    {
        if (_surface.navMeshData != null)
        {
            AsyncOperation operation = _surface.UpdateNavMesh(_surface.navMeshData);
            if (operation != null)
            {
                while (!operation.isDone)
                    yield return null;
            }
        }
        else
        {
            // First bake has no existing data to update asynchronously.
            _surface.BuildNavMesh();
        }

        if (_scope == NavMeshScope.Host)
            RepositionAgentsOnNavMesh();
    }

    private IEnumerator RebuildDungeonRoutine()
    {
        if (_surface == null || !_dungeonScene.IsValid() || !_dungeonScene.isLoaded)
            yield break;

        Physics.SyncTransforms();
        List<NavMeshBuildSource> sources = CollectDungeonSources(_dungeonScene);
        if (sources.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(DynamicNavMeshRebuilder)}: dungeon '{_dungeonScene.name}' has no NavMesh sources.");
            yield break;
        }

        Bounds bounds = CalculateSourceBounds(sources);
        NavMeshBuildSettings settings = _surface.GetBuildSettings();

        if (_dungeonNavMeshData == null)
        {
            _dungeonNavMeshData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                bounds,
                Vector3.zero,
                Quaternion.identity);
        }
        else
        {
            AsyncOperation operation = NavMeshBuilder.UpdateNavMeshDataAsync(
                _dungeonNavMeshData,
                settings,
                sources,
                bounds);
            if (operation != null)
            {
                while (!operation.isDone)
                    yield return null;
            }
        }

        if (_dungeonNavMeshData == null || _scope != NavMeshScope.Dungeon)
            yield break;

        if (!_dungeonNavMeshInstance.valid)
            _dungeonNavMeshInstance = NavMesh.AddNavMeshData(_dungeonNavMeshData);

        RepositionAgentsOnNavMesh();
    }

    private void DestroyDungeonNavMesh()
    {
        if (_dungeonNavMeshInstance.valid)
            _dungeonNavMeshInstance.Remove();

        _dungeonNavMeshInstance = default;

        if (_dungeonNavMeshData == null)
            return;

        Destroy(_dungeonNavMeshData);
        _dungeonNavMeshData = null;
    }

    private List<NavMeshBuildSource> CollectDungeonSources(Scene scene)
    {
        var sources = new List<NavMeshBuildSource>();
        var markups = new List<NavMeshBuildMarkup>();
        List<NavMeshModifier> modifiers = NavMeshModifier.activeModifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            NavMeshModifier modifier = modifiers[i];
            if (modifier == null || modifier.gameObject.scene != scene)
                continue;

            if ((_surface.layerMask.value & (1 << modifier.gameObject.layer)) == 0)
                continue;

            if (!modifier.AffectsAgentType(_surface.agentTypeID))
                continue;

            markups.Add(new NavMeshBuildMarkup
            {
                root = modifier.transform,
                overrideArea = modifier.overrideArea,
                area = modifier.area,
                ignoreFromBuild = modifier.ignoreFromBuild,
                applyToChildren = modifier.applyToChildren,
                overrideGenerateLinks = modifier.overrideGenerateLinks,
                generateLinks = modifier.generateLinks
            });
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || !root.activeInHierarchy)
                continue;

            var rootSources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(
                root.transform,
                _surface.layerMask,
                _surface.useGeometry,
                _surface.defaultArea,
                false,
                markups,
                false,
                rootSources);
            sources.AddRange(rootSources);
        }

        if (_surface.ignoreNavMeshAgent)
        {
            sources.RemoveAll(source =>
                source.component != null && source.component.GetComponent<NavMeshAgent>() != null);
        }

        if (_surface.ignoreNavMeshObstacle)
        {
            sources.RemoveAll(source =>
                source.component != null && source.component.GetComponent<NavMeshObstacle>() != null);
        }

        return sources;
    }

    private static Bounds CalculateSourceBounds(List<NavMeshBuildSource> sources)
    {
        var result = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < sources.Count; i++)
        {
            if (!TryGetSourceBounds(sources[i], out Bounds sourceBounds))
                continue;

            if (!hasBounds)
            {
                result = sourceBounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(sourceBounds);
            }
        }

        if (!hasBounds)
            return new Bounds(Vector3.zero, Vector3.one);

        result.Expand(0.1f);
        return result;
    }

    private static bool TryGetSourceBounds(NavMeshBuildSource source, out Bounds bounds)
    {
        switch (source.shape)
        {
            case NavMeshBuildSourceShape.Mesh:
                var mesh = source.sourceObject as Mesh;
                if (mesh == null)
                    break;

                bounds = TransformBounds(source.transform, mesh.bounds);
                return true;
            case NavMeshBuildSourceShape.Terrain:
                var terrain = source.sourceObject as TerrainData;
                if (terrain == null)
                    break;

                bounds = TransformBounds(source.transform, new Bounds(0.5f * terrain.size, terrain.size));
                return true;
            case NavMeshBuildSourceShape.Box:
            case NavMeshBuildSourceShape.Sphere:
            case NavMeshBuildSourceShape.Capsule:
            case NavMeshBuildSourceShape.ModifierBox:
                bounds = TransformBounds(source.transform, new Bounds(Vector3.zero, source.size));
                return true;
        }

        bounds = default;
        return false;
    }

    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
    {
        Vector3 absAxisX = Abs(matrix.MultiplyVector(Vector3.right));
        Vector3 absAxisY = Abs(matrix.MultiplyVector(Vector3.up));
        Vector3 absAxisZ = Abs(matrix.MultiplyVector(Vector3.forward));
        Vector3 worldPosition = matrix.MultiplyPoint(localBounds.center);
        Vector3 worldSize = absAxisX * localBounds.size.x + absAxisY * localBounds.size.y + absAxisZ * localBounds.size.z;
        return new Bounds(worldPosition, worldSize);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void EnsureSurface()
    {
        if (_surface != null)
            return;

        _surface = GetComponent<NavMeshSurface>();
        if (_surface == null)
            _surface = FindFirstObjectByType<NavMeshSurface>(FindObjectsInactive.Include);

        if (_surface != null)
            return;

        _surface = gameObject.AddComponent<NavMeshSurface>();
        _surface.collectObjects = CollectObjects.All;
        _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        _surface.layerMask = ~0;
    }

    private void RefreshSpawnPoints()
    {
        _spawnPoints.Clear();
        var found = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                _spawnPoints.Add(found[i]);
        }
    }

    private void Subscribe()
    {
        if (_subscribed || _islandRegistry == null)
            return;

        _islandRegistry.IslandRegistered += OnIslandRegistered;
        _islandRegistry.IslandUnregistered += OnIslandUnregistered;

        foreach (var island in _islandRegistry.Islands)
            SubscribeIsland(island);

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _islandRegistry == null)
            return;

        _islandRegistry.IslandRegistered -= OnIslandRegistered;
        _islandRegistry.IslandUnregistered -= OnIslandUnregistered;

        foreach (var island in _islandRegistry.Islands)
            UnsubscribeIsland(island);

        _subscribed = false;
    }

    private void OnIslandRegistered(IslandProgressHandler island)
    {
        SubscribeIsland(island);
    }

    private void OnIslandUnregistered(IslandProgressHandler island)
    {
        UnsubscribeIsland(island);
    }

    private void SubscribeIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.HexUnlocked -= OnHexUnlocked;
        island.HexUnlocked += OnHexUnlocked;
    }

    private void UnsubscribeIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.HexUnlocked -= OnHexUnlocked;
    }

    private void OnHexUnlocked(IslandProgressHandler _, HexTile hex)
    {
        if (hex == null)
        {
            RequestRebuild();
            return;
        }

        // Unlock() scales the tile from zero; wait until the collider is full size.
        void OnAnimationEnded()
        {
            hex.OnUnlockAnimationEnded -= OnAnimationEnded;

            if (IsHexRelevantForAgents(hex))
                RequestRebuild();
        }

        hex.OnUnlockAnimationEnded -= OnAnimationEnded;
        hex.OnUnlockAnimationEnded += OnAnimationEnded;
    }

    private bool IsHexRelevantForAgents(HexTile hex)
    {
        if (_spawnPoints.Count == 0)
            RefreshSpawnPoints();

        if (_spawnPoints.Count == 0)
            return false;

        Vector3 hexCenter = hex.transform.position;
        float hexRadius = GetHexApproximateRadius(hex);
        float padding = Mathf.Max(0f, _relevancePadding);

        for (int i = 0; i < _spawnPoints.Count; i++)
        {
            EnemySpawnPoint spawn = _spawnPoints[i];
            if (spawn == null)
                continue;

            if (!IsSpawnInActiveNavMeshScene(spawn))
                continue;

            float combinedRadius = spawn.GuardRadius + hexRadius + padding;
            if ((spawn.transform.position - hexCenter).sqrMagnitude <= combinedRadius * combinedRadius)
                return true;
        }

        return false;
    }

    private bool IsSpawnInActiveNavMeshScene(EnemySpawnPoint spawn)
    {
        Scene spawnScene = spawn.gameObject.scene;
        if (_scope == NavMeshScope.Dungeon)
            return _dungeonScene.IsValid() && spawnScene == _dungeonScene;

        return spawnScene == gameObject.scene;
    }

    private float GetHexApproximateRadius(HexTile hex)
    {
        Collider collider = hex.GetComponent<Collider>();
        if (collider != null)
        {
            Vector3 extents = collider.bounds.extents;
            return Mathf.Max(extents.x, extents.z);
        }

        Renderer renderer = hex.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Vector3 extents = renderer.bounds.extents;
            return Mathf.Max(extents.x, extents.z);
        }

        return Mathf.Max(0.1f, _fallbackHexRadius);
    }

    private static void RepositionAgentsOnNavMesh()
    {
        var agents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy)
                continue;

            Vector3 position = agent.transform.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }
}

