using System;
using System.Collections;
using System.Collections.Generic;
using ECM2;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public class SceneTravelService : MonoBehaviour
{
    public static SceneTravelService Instance { get; private set; }
    public event Action TravelCompleted;

    private const string SpawnPointName = "PlayerSpawnPoint";
    private const float CooldownSeconds = 1.5f;
    private const float ArrivalGateSuppressSeconds = 2f;
    private const float ArrivalGateLoadingSuppressSeconds = 120f;

    [SerializeField] private GameObject[] _hostWorldRoots;
    [SerializeField] private LoadingScreen _loadingScreen;
    [SerializeField] private float _runInSeconds = SceneTravelTiming.DefaultRunInSeconds;
    [SerializeField] private float _minLoadingSeconds = SceneTravelTiming.DefaultMinLoadingSeconds;

    private readonly SceneTravelSession _session = new SceneTravelSession();
    private readonly List<GameObject> _hiddenHostRoots = new List<GameObject>();

    private Player _player;
    private CinemachineCamera _cinemachineCamera;
    private IInterstitialAds _interstitialAds;
    private ISaveLoader _saveLoader;
    private DynamicNavMeshRebuilder _navMeshRebuilder;
    private Scene _hostScene;
    private Scene _dungeonScene;
    private Transform _dungeonSpawnPoint;
    private float _cooldownUntil;
    private Coroutine _travelCoroutine;

    public bool IsInDungeon => _session.IsInDungeon;
    public string CurrentDungeonSceneName => _session.CurrentDungeonSceneName;
    public bool IsBusy => _session.IsBusy || Time.unscaledTime < _cooldownUntil;

    [Inject]
    private void Construct(
        Player player,
        CinemachineCamera cinemachineCamera,
        [InjectOptional] IInterstitialAds interstitialAds,
        [InjectOptional] ISaveLoader saveLoader,
        [InjectOptional] DynamicNavMeshRebuilder navMeshRebuilder)
    {
        _player = player;
        _cinemachineCamera = cinemachineCamera;
        _interstitialAds = interstitialAds;
        _saveLoader = saveLoader;
        _navMeshRebuilder = navMeshRebuilder;
    }

    private void Awake()
    {
        Instance = this;
        _hostScene = gameObject.scene;

        if (_loadingScreen == null)
            _loadingScreen = FindFirstObjectByType<LoadingScreen>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryGetDungeonSpawnPoint(out Transform spawnPoint)
    {
        spawnPoint = _dungeonSpawnPoint;
        return _session.IsInDungeon && spawnPoint != null;
    }

    public void Travel(
        SceneTravelDestination destination,
        Vector3 returnPosition,
        Quaternion returnRotation,
        Vector3 runInTarget)
    {
        if (destination == null || _player == null || _player.IsDead)
            return;

        if (Time.unscaledTime < _cooldownUntil)
            return;

        SceneTravelIntent intent = _session.Resolve(destination.ReturnToHost, destination.SceneName);
        if (intent == SceneTravelIntent.None)
            return;

        if (!_session.TryBegin())
            return;

        if (_travelCoroutine != null)
            StopCoroutine(_travelCoroutine);

        _travelCoroutine = StartCoroutine(TravelRoutine(intent, destination, returnPosition, returnRotation, runInTarget));
    }

    private IEnumerator TravelRoutine(
        SceneTravelIntent intent,
        SceneTravelDestination destination,
        Vector3 returnPosition,
        Quaternion returnRotation,
        Vector3 runInTarget)
    {
        yield return RunIntoGate(runInTarget);

        ShowLoading();
        float loadingStarted = Time.unscaledTime;
        SetPlayerPaused(true);

        if (intent == SceneTravelIntent.EnterDungeon)
            yield return EnterDungeon(destination.SceneName, returnPosition, returnRotation);
        else if (intent == SceneTravelIntent.SwitchDungeon)
            yield return SwitchDungeon(destination.SceneName);
        else
            yield return ExitToHost();

        ArmArrivalGates(ArrivalGateLoadingSuppressSeconds);
        yield return WaitRemainingLoading(loadingStarted);
        HideLoading();

        if (_interstitialAds != null)
            yield return _interstitialAds.WaitForInterstitial(InterstitialPlacements.SceneTravel);

        SetPlayerPaused(false);
        RestorePlayerControl();
        ArmArrivalGates(ArrivalGateSuppressSeconds);
        _cooldownUntil = Time.unscaledTime + CooldownSeconds;
        _travelCoroutine = null;
        NotifyTravelCompleted();
    }

    private void NotifyTravelCompleted()
    {
        try
        {
            TravelCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(SceneTravelService)} travel notify failed: {exception.Message}");
        }
    }

    private IEnumerator RunIntoGate(Vector3 runInTarget)
    {
        _player.SetGameplayControlEnabled(false);

        Character character = _player.GetComponent<Character>();
        if (character != null)
        {
            Vector3 direction = SceneTravelTiming.FlattenedRunInDirection(
                _player.transform.position,
                runInTarget,
                _player.transform.forward);
            character.SetMovementDirection(direction);
        }

        if (_runInSeconds > 0f)
            yield return new WaitForSeconds(_runInSeconds);

        if (character != null)
            character.SetMovementDirection(Vector3.zero);
    }

    private IEnumerator WaitRemainingLoading(float loadingStartedUnscaled)
    {
        float remaining = SceneTravelTiming.RemainingLoadingSeconds(
            Time.unscaledTime - loadingStartedUnscaled,
            _minLoadingSeconds);
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);
    }

    private static void ArmArrivalGates(float seconds)
    {
        SceneTravelGate[] gates = FindObjectsByType<SceneTravelGate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (SceneTravelGate gate in gates)
        {
            if (gate != null)
                gate.SuppressFor(seconds);
        }
    }

    private IEnumerator EnterDungeon(string sceneName, Vector3 returnPosition, Quaternion returnRotation)
    {
        if (_navMeshRebuilder != null)
            yield return _navMeshRebuilder.WaitUntilIdle();

        HideHostWorld();

        if (_navMeshRebuilder != null)
            _navMeshRebuilder.PrepareDungeonTransition();

        if (!TryGetLoadedScene(sceneName, out Scene dungeonScene))
        {
            yield return LoadDungeonAdditive(sceneName);
            if (!TryGetLoadedScene(sceneName, out dungeonScene))
            {
                Debug.LogError($"SceneTravelService: failed to load dungeon scene '{sceneName}'.");
                _navMeshRebuilder?.BeginHost();
                ShowHostWorld();
                _navMeshRebuilder?.SnapAgentsToNavMesh();
                _session.Abort();
                yield break;
            }
        }

        PrepareLoadedDungeon(dungeonScene);
        _navMeshRebuilder?.BeginDungeon(dungeonScene);
        TeleportPlayer(_dungeonSpawnPoint != null ? _dungeonSpawnPoint.position : Vector3.zero);
        _session.CompleteEnter(sceneName, returnPosition, returnRotation);
        yield return null;
        ApplyLoadedDungeonProgress();
        if (_navMeshRebuilder != null)
            yield return _navMeshRebuilder.RebuildNow();
    }

    private IEnumerator SwitchDungeon(string sceneName)
    {
        if (_navMeshRebuilder != null)
        {
            yield return _navMeshRebuilder.WaitUntilIdle();
            _navMeshRebuilder.PrepareDungeonTransition();
        }

        yield return UnloadCurrentDungeon();

        yield return LoadDungeonAdditive(sceneName);
        if (!TryGetLoadedScene(sceneName, out Scene dungeonScene))
        {
            Debug.LogError($"SceneTravelService: failed to switch to dungeon scene '{sceneName}'.");
            _navMeshRebuilder?.BeginHost();
            ShowHostWorld();
            _navMeshRebuilder?.SnapAgentsToNavMesh();
            _session.CompleteExit();
            yield break;
        }

        PrepareLoadedDungeon(dungeonScene);
        _navMeshRebuilder?.BeginDungeon(dungeonScene);
        TeleportPlayer(_dungeonSpawnPoint != null ? _dungeonSpawnPoint.position : Vector3.zero);
        _session.CompleteSwitch(sceneName);
        yield return null;
        ApplyLoadedDungeonProgress();
        if (_navMeshRebuilder != null)
            yield return _navMeshRebuilder.RebuildNow();
    }

    private IEnumerator ExitToHost()
    {
        Vector3 returnPosition = _session.ReturnPosition;
        Quaternion returnRotation = _session.ReturnRotation;

        if (_navMeshRebuilder != null)
            yield return _navMeshRebuilder.WaitUntilIdle();

        yield return UnloadCurrentDungeon();
        _navMeshRebuilder?.BeginHost();
        ShowHostWorld();
        _navMeshRebuilder?.SnapAgentsToNavMesh();

        if (_hostScene.IsValid() && _hostScene.isLoaded)
            SceneManager.SetActiveScene(_hostScene);

        TeleportPlayer(returnPosition);
        _player.transform.rotation = returnRotation;
        _dungeonSpawnPoint = null;
        _session.CompleteExit();
    }

    private IEnumerator LoadDungeonAdditive(string sceneName)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (load == null)
            yield break;

        while (!load.isDone)
            yield return null;
    }

    private IEnumerator UnloadCurrentDungeon()
    {
        if (!_dungeonScene.IsValid() || !_dungeonScene.isLoaded)
        {
            _dungeonScene = default;
            yield break;
        }

        AsyncOperation unload = SceneManager.UnloadSceneAsync(_dungeonScene);
        _dungeonScene = default;
        _dungeonSpawnPoint = null;
        if (unload == null)
            yield break;

        while (!unload.isDone)
            yield return null;
    }

    private void ApplyLoadedDungeonProgress()
    {
        if (_saveLoader == null || !_saveLoader.HasLoadedSave)
            return;

        _saveLoader.ApplyIslands();
        _saveLoader.ApplyKilledOneTimeEnemies();
        _saveLoader.ApplyHarvestedOneTimeHolders();
        _saveLoader.DisableCompletedOpeningHexagons();
    }

    private void PrepareLoadedDungeon(Scene dungeonScene)
    {
        _dungeonScene = dungeonScene;
        DisableDungeonCameras(dungeonScene);
        _dungeonSpawnPoint = FindNamedTransform(dungeonScene, SpawnPointName);

        if (_dungeonSpawnPoint == null)
            Debug.LogWarning($"SceneTravelService: '{SpawnPointName}' not found in '{dungeonScene.name}'.");

        WarnIfDungeonSceneContextMissing(dungeonScene);
        SceneManager.SetActiveScene(dungeonScene);
    }

    private void TeleportPlayer(Vector3 position)
    {
        if (_player == null)
            return;

        Vector3 previous = _player.transform.position;
        Character character = _player.GetComponent<Character>();
        if (character != null)
        {
            character.TeleportPosition(position, interpolating: true, updateGround: true);
            character.ClearPhysicsVolumes();
            character.SetVelocity(Vector3.zero);
            Physics.SyncTransforms();

            if (character.IsGrounded())
                character.SetMovementMode(Character.MovementMode.Walking);
            else
                character.SetMovementMode(Character.MovementMode.Falling);
        }
        else
        {
            _player.transform.position = position;
        }

        if (_cinemachineCamera == null || _player.CameraTarget == null)
            return;

        _cinemachineCamera.OnTargetObjectWarped(_player.CameraTarget.transform, position - previous);
    }

    private void SetPlayerPaused(bool paused)
    {
        if (_player == null)
            return;

        Character character = _player.GetComponent<Character>();
        if (character != null)
            character.Pause(paused);
    }

    private void RestorePlayerControl()
    {
        if (_player == null || _player.IsDead)
            return;

        _player.SetGameplayControlEnabled(true);
    }

    private void HideHostWorld()
    {
        _hiddenHostRoots.Clear();
        if (_hostWorldRoots == null)
            return;

        foreach (GameObject root in _hostWorldRoots)
        {
            if (root == null || !root.activeSelf)
                continue;

            root.SetActive(false);
            _hiddenHostRoots.Add(root);
        }
    }

    private void ShowHostWorld()
    {
        foreach (GameObject root in _hiddenHostRoots)
        {
            if (root != null)
                root.SetActive(true);
        }

        _hiddenHostRoots.Clear();
    }

    private void ShowLoading()
    {
        _loadingScreen?.Show();
    }

    private void HideLoading()
    {
        _loadingScreen?.Hide();
    }

    private static void WarnIfDungeonSceneContextMissing(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].GetComponentInChildren<SceneContext>(true) != null)
                return;
        }

        Debug.LogError(
            $"SceneTravelService: scene '{scene.name}' has no SceneContext. " +
            "Injectables in the dungeon (resources, camera fade, spawners) will not receive host bindings. " +
            $"Add a SceneContext with Parent Contract Names = '{SceneTravelContracts.Gameplay}'.");
    }

    private static bool TryGetLoadedScene(string sceneName, out Scene scene)
    {
        scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    private static void DisableDungeonCameras(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
                camera.enabled = false;

            AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
            foreach (AudioListener listener in listeners)
                listener.enabled = false;

            CinemachineCamera[] virtualCameras = root.GetComponentsInChildren<CinemachineCamera>(true);
            foreach (CinemachineCamera virtualCamera in virtualCameras)
                virtualCamera.enabled = false;
        }
    }

    private static Transform FindNamedTransform(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Transform match = FindNamedTransform(root.transform, objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform FindNamedTransform(Transform current, string objectName)
    {
        if (current.name == objectName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform match = FindNamedTransform(current.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }
}
