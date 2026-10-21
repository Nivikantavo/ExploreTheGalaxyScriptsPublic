using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
#if UNITY_WEBGL && !UNITY_EDITOR
using Playgama;
#endif

[DefaultExecutionOrder(-100)]
public class SaveLoadBootstrap : MonoBehaviour
{
    private const float AutosaveDebounceSeconds = 2f;
    private const float AutosaveMaxIntervalSeconds = 10f;
    private const float StorageInitTimeoutSeconds = 20f;

    private ISaveLoader _saveLoader;
    private ISaveStorage _saveStorage;
    private Inventory _inventory;
    private AllInstruments _allInstruments;
    private AllEquipment _allEquipment;
    private IslandProgressRegistry _islandRegistry;
    private PlayerSpawnPoints _spawnPoints;
    private CreatingResourceObject[] _productions;
    private InstrumentUpgrader[] _instrumentUpgraders;
    private EquipmentUpgrader[] _equipmentUpgraders;
    private OpeningObjectChain[] _openingChains;
    private NpcRescueSite[] _npcRescueSites;
    private ShipSite[] _shipSites;
    private ResourseHolderView[] _resourceHolders;
    private EnemySpawnPoint[] _enemySpawnPoints;
    private TutorialHandler _tutorialHandler;
    private GoalSystem _goalSystem;
    private CelebrationSystem _celebrationSystem;
    private AchievementSystem _achievementSystem;
    private DynamicNavMeshRebuilder _navMeshRebuilder;
    private LifecycleEventBus _lifecycleEventBus;
    private Player _player;
    private IapService _iapService;
    private float _autosaveTimer = -1f;
    private float _timeSinceLastSave = 0f;
    private bool _hasPendingAutosave;
    private bool _pendingIslandCollect;
    private bool _saveApplied;
    private bool _allowPersist;
    private bool _lateApplyInProgress;
    private bool _handlingException;
    private IInitializableSaveLoader _initializableStorage;

    [Inject]
    private void Construct(
        ISaveLoader saveLoader,
        ISaveStorage saveStorage,
        Inventory inventory,
        AllInstruments allInstruments,
        AllEquipment allEquipment,
        IslandProgressRegistry islandRegistry,
        PlayerSpawnPoints spawnPoints,
        Player player,
        DynamicNavMeshRebuilder navMeshRebuilder,
        LifecycleEventBus lifecycleEventBus,
        IapService iapService)
    {
        _saveLoader = saveLoader;
        _saveStorage = saveStorage;
        _inventory = inventory;
        _allInstruments = allInstruments;
        _allEquipment = allEquipment;
        _islandRegistry = islandRegistry;
        _spawnPoints = spawnPoints;
        _player = player;
        _navMeshRebuilder = navMeshRebuilder;
        _lifecycleEventBus = lifecycleEventBus;
        _iapService = iapService;
    }

    private void Awake()
    {
        _productions = FindObjectsByType<CreatingResourceObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _instrumentUpgraders = FindObjectsByType<InstrumentUpgrader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _equipmentUpgraders = FindObjectsByType<EquipmentUpgrader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _openingChains = FindObjectsByType<OpeningObjectChain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _npcRescueSites = FindObjectsByType<NpcRescueSite>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _shipSites = FindObjectsByType<ShipSite>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _resourceHolders = FindObjectsByType<ResourseHolderView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _enemySpawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _tutorialHandler = FindFirstObjectByType<TutorialHandler>(FindObjectsInactive.Include);
        _goalSystem = FindFirstObjectByType<GoalSystem>(FindObjectsInactive.Include);
        _celebrationSystem = FindFirstObjectByType<CelebrationSystem>(FindObjectsInactive.Include);
        _achievementSystem = FindFirstObjectByType<AchievementSystem>(FindObjectsInactive.Include);
        BindLiveScene();

        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialEnded += OnTutorialEnded;

        Application.logMessageReceived += OnLogMessageReceived;

        if (_saveStorage is IInitializableSaveLoader initializableSaveLoader)
        {
            _initializableStorage = initializableSaveLoader;
            _initializableStorage.LateDataArrived += OnLateStorageDataArrived;
        }

        SaveSystemTestLog.Info(
            $"Bootstrap.Awake tutorialHandler={_tutorialHandler != null} islands={_islandRegistry?.Islands.Count ?? -1}");

#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        LoadAndApplyInventory();
        ResolveTutorialAfterSaveLoad();
#endif
    }

    private void Start()
    {
        SubscribeAutosaveTriggers();
        SaveSystemTestLog.Info("Bootstrap.Start autosave triggers subscribed");

#if UNITY_WEBGL && !UNITY_EDITOR
        StartCoroutine(InitializeAndLoadCoroutine());
#else
        if (TryRedirectToSavedHostWorld())
            return;

        ApplyDeferredSaveState();
        RefreshSystemsAfterSaveApplied();
        SetPersistAllowed(!_saveLoader.IsHostWorldMismatch);
        PlatformGameplayLifecycle.NotifyLevelStarted();
#endif
    }

    private void Update()
    {
        if (!_saveApplied)
            return;

        float dt = Time.unscaledDeltaTime;
        _timeSinceLastSave += dt;

        if (!_hasPendingAutosave)
            return;

        _autosaveTimer -= dt;

        bool debounceElapsed = _autosaveTimer <= 0f;
        bool maxIntervalElapsed = _timeSinceLastSave >= AutosaveMaxIntervalSeconds;
        if (!debounceElapsed && !maxIntervalElapsed)
            return;

        SaveSystemTestLog.Info(
            $"Bootstrap.Update autosave fire debounceElapsed={debounceElapsed} maxIntervalElapsed={maxIntervalElapsed}");
        PersistNow("autosave");
    }

    private void OnDestroy()
    {
        UnsubscribeAutosaveTriggers();

        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialEnded -= OnTutorialEnded;

        Application.logMessageReceived -= OnLogMessageReceived;

        if (_initializableStorage != null)
            _initializableStorage.LateDataArrived -= OnLateStorageDataArrived;

#if UNITY_WEBGL && !UNITY_EDITOR
        UnsubscribePlatformSaveTriggers();
#endif
    }

    private IEnumerator InitializeAndLoadCoroutine()
    {
        SaveSystemTestLog.Info("Bootstrap.InitializeAndLoadCoroutine START");
        yield return WaitForBridgeReady();
        yield return InitializeStorageWithTimeout();

        try
        {
            SaveSystemTestLog.Info("Bootstrap.apply sequence: LoadAndApplyInventory");
            try
            {
                LoadAndApplyInventory();
            }
            catch (System.Exception exception)
            {
                SaveSystemTestLog.Error($"Bootstrap.LoadAndApplyInventory FAILED (continuing): {exception}");
            }

            if (TryRedirectToSavedHostWorld())
                yield break;

            SaveSystemTestLog.Info("Bootstrap.apply sequence: ResolveTutorialAfterSaveLoad");
            try
            {
                ResolveTutorialAfterSaveLoad();
            }
            catch (System.Exception exception)
            {
                SaveSystemTestLog.Error($"Bootstrap.ResolveTutorialAfterSaveLoad FAILED (continuing): {exception}");
            }

            SaveSystemTestLog.Info("Bootstrap.apply sequence: ApplyDeferredSaveState");
            bool worldApplied = false;
            try
            {
                ApplyDeferredSaveState();
                worldApplied = true;
            }
            catch (System.Exception exception)
            {
                SaveSystemTestLog.Error($"Bootstrap.ApplyDeferredSaveState FAILED: {exception}");
            }

            bool hadLoadedSave = _saveLoader.HasLoadedSave;
            UpdatePersistGate(worldApplied, hadLoadedSave);

            SaveSystemTestLog.Info("Bootstrap.apply sequence: RefreshSystemsAfterSaveApplied");
            try
            {
                RefreshSystemsAfterSaveApplied();
            }
            catch (System.Exception exception)
            {
                SaveSystemTestLog.Error($"Bootstrap.RefreshSystemsAfterSaveApplied FAILED (continuing): {exception}");
                _saveApplied = true;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_allowPersist)
            {
                SubscribePlatformSaveTriggers();
                SaveSystemTestLog.Info("Bootstrap platform save triggers subscribed");
            }
#endif
        }
        catch (System.Exception exception)
        {
            SaveSystemTestLog.Error($"Bootstrap.apply FAILED: {exception}");
            ResolveTutorialAfterSaveLoad();
            _saveApplied = true;
            UpdatePersistGate(worldApplied: false, hadLoadedSave: _saveLoader.HasLoadedSave);
            SaveSystemTestLog.Warn(
                $"Bootstrap._saveApplied forced true allowPersist={_allowPersist}");
        }
        finally
        {
            SaveSystemTestLog.Info(
                $"Bootstrap apply finished saveApplied={_saveApplied} allowPersist={_allowPersist} hasLoaded={_saveLoader.HasLoadedSave}");
            PlatformGameplayLifecycle.NotifyLevelStarted();
            SaveSystemTestLog.Info("Bootstrap.InitializeAndLoadCoroutine END");
        }
    }

    private static IEnumerator WaitForBridgeReady()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SaveSystemTestLog.Info("Bootstrap.WaitForBridgeReady begin");
        float elapsed = 0f;
        while ((Bridge.instance == null || Bridge.storage == null) && elapsed < StorageInitTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        bool ready = Bridge.instance != null && Bridge.storage != null;
        if (ready)
            SaveSystemTestLog.Info(
                $"Bootstrap.WaitForBridgeReady ready in {elapsed:0.00}s");
        else
            SaveSystemTestLog.Error($"Bootstrap.WaitForBridgeReady TIMEOUT after {StorageInitTimeoutSeconds}s");
#else
        yield break;
#endif
    }

    private IEnumerator InitializeStorageWithTimeout()
    {
        if (_saveStorage is not IInitializableSaveLoader initializableSaveLoader)
        {
            SaveSystemTestLog.Warn($"Bootstrap.InitializeStorage skipped, storage type={_saveStorage?.GetType().Name}");
            yield break;
        }

        if (initializableSaveLoader.IsInitialized)
        {
            SaveSystemTestLog.Info("Bootstrap.InitializeStorage already initialized");
            yield break;
        }

        SaveSystemTestLog.Info("Bootstrap.InitializeStorage begin");
        bool isCompleted = false;
        initializableSaveLoader.Initialize(() => isCompleted = true);

        float elapsed = 0f;
        while (!isCompleted && elapsed < StorageInitTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!isCompleted)
        {
            SaveSystemTestLog.Error(
                $"Bootstrap.InitializeStorage TIMEOUT after {StorageInitTimeoutSeconds}s — persist stays blocked");
            initializableSaveLoader.ForceCompleteInitialization();
        }
        else
        {
            SaveSystemTestLog.Info($"Bootstrap.InitializeStorage completed in {elapsed:0.00}s");
        }
    }

    private void LoadAndApplyInventory()
    {
        bool loaded = _saveLoader.TryLoad();
        SaveSystemTestLog.Info($"Bootstrap.LoadAndApplyInventory TryLoad={loaded}");
        if (!loaded)
            return;

        _saveLoader.ApplyInventory(_inventory, _allInstruments, _allEquipment);
        _player?.RefreshEquipmentHealth();
        SaveSystemTestLog.Info("Bootstrap.LoadAndApplyInventory inventory applied");
    }

    private void ApplyDeferredSaveState()
    {
        if (!_saveLoader.HasLoadedSave)
        {
            SaveSystemTestLog.Info("Bootstrap.ApplyDeferredSaveState skipped (no loaded save)");
            return;
        }

        SaveSystemTestLog.Info("Bootstrap.ApplyDeferredSaveState applying islands/chains/productions");
        _saveLoader.ApplyIslands();
        _saveLoader.DisableCompletedOpeningHexagons();
        _saveLoader.ApplyOpeningObjectChains();
        _saveLoader.ApplyHarvestedOneTimeHolders();
        _saveLoader.ApplyKilledOneTimeEnemies();
        _saveLoader.ApplyArrivedNpcWorkers();
        _saveLoader.ApplyShips();

        foreach (var production in _productions)
        {
            if (production == null)
                continue;

            production.Initialize();
            production.ApplySave(_saveLoader);
        }

        foreach (var upgrader in _instrumentUpgraders)
        {
            if (upgrader == null)
                continue;

            upgrader.Initialize();
            upgrader.ApplySave(_saveLoader);
        }

        if (_equipmentUpgraders != null)
        {
            foreach (var upgrader in _equipmentUpgraders)
            {
                if (upgrader == null)
                    continue;

                upgrader.Initialize();
                upgrader.ApplySave(_saveLoader);
            }
        }

        SaveSystemTestLog.Info("Bootstrap.ApplyDeferredSaveState done");
    }

    private void RefreshSystemsAfterSaveApplied()
    {
        _saveApplied = true;
        SaveSystemTestLog.Info("Bootstrap.RefreshSystemsAfterSaveApplied saveApplied=true");

        _goalSystem?.RefreshFromSave();
        _celebrationSystem?.RefreshFromSave();
        _achievementSystem?.RefreshFromSave();
        _navMeshRebuilder?.RequestRebuild();
        _iapService?.StartAfterSaveApplied();

        if (_saveLoader.HasLoadedSave &&
            _saveLoader.LoadedSave.IsTutorialCompleted)
        {
            SaveSystemTestLog.Info("Bootstrap.RefreshSystemsAfterSaveApplied move player to default spawn");
            MovePlayerToDefaultSpawn();
        }

        InstrumentPickup.NotifySaveApplied();
    }

    private void BindLiveScene()
    {
        string hostWorldId = null;
        WorldSaveIdentity[] identities = FindObjectsByType<WorldSaveIdentity>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < identities.Length; i++)
        {
            WorldSaveIdentity identity = identities[i];
            if (identity == null || string.IsNullOrEmpty(identity.WorldId))
                continue;

            hostWorldId = identity.WorldId;
            break;
        }

        _saveLoader.BindLiveScene(new LiveSceneSaveCache
        {
            Productions = _productions,
            InstrumentUpgraders = _instrumentUpgraders,
            EquipmentUpgraders = _equipmentUpgraders,
            OpeningChains = _openingChains,
            ShipSites = _shipSites,
            NpcRescueSites = _npcRescueSites,
            ResourceHolders = _resourceHolders,
            EnemySpawnPoints = _enemySpawnPoints,
            GoalSystem = _goalSystem,
            CelebrationSystem = _celebrationSystem,
            AchievementSystem = _achievementSystem,
            HostWorldId = hostWorldId
        });
    }

    private void ResolveTutorialAfterSaveLoad()
    {
        if (_tutorialHandler == null)
        {
            SaveSystemTestLog.Warn("Bootstrap.ResolveTutorialAfterSaveLoad TutorialHandler is null");
            if (_saveLoader.HasLoadedSave && _saveLoader.LoadedSave.IsTutorialCompleted)
                TutorialHudAccess.NotifyTutorialResolved();
            return;
        }

        bool skipTutorial = _saveLoader.HasLoadedSave &&
                            _saveLoader.LoadedSave.IsTutorialCompleted;
        SaveSystemTestLog.Info(
            $"Bootstrap.ResolveTutorialAfterSaveLoad BeginAfterSaveLoad skipTutorial={skipTutorial}");
        _tutorialHandler.BeginAfterSaveLoad();

        if (skipTutorial)
            MovePlayerToDefaultSpawn();
    }

    private void MovePlayerToDefaultSpawn()
    {
        if (_player == null || _spawnPoints?.DefaultSpawn == null)
        {
            SaveSystemTestLog.Warn("Bootstrap.MovePlayerToDefaultSpawn missing player/spawn");
            return;
        }

        Vector3 spawnPosition = _spawnPoints.DefaultSpawn.position;
        var character = _player.GetComponent<ECM2.Character>();
        if (character != null)
            character.SetPosition(spawnPosition);
        else
            _player.transform.position = spawnPosition;

        SaveSystemTestLog.Info($"Bootstrap.MovePlayerToDefaultSpawn pos={spawnPosition}");
    }

    private void SubscribeAutosaveTriggers()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged += OnProgressChanged;
            _inventory.InstrumentUpgraded += OnInstrumentUpgraded;
            _inventory.EquipmentChanged += OnEquipmentChanged;
            _inventory.EnemiesKilledChanged += OnEnemiesKilledChanged;
        }

        if (_lifecycleEventBus != null)
            _lifecycleEventBus.OnEvent += OnLifecycleEvent;

        if (_islandRegistry == null)
        {
            SubscribeNpcRescueSites();
            SubscribeShipSites();
            return;
        }

        foreach (var island in _islandRegistry.Islands)
        {
            island.MainPathReached += OnIslandProgressChanged;
            island.HexUnlocked += OnIslandHexUnlocked;
            island.OpeningHexagonChanged += OnOpeningHexagonChanged;
        }

        SubscribeNpcRescueSites();
        SubscribeShipSites();
    }

    private void UnsubscribeAutosaveTriggers()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged -= OnProgressChanged;
            _inventory.InstrumentUpgraded -= OnInstrumentUpgraded;
            _inventory.EquipmentChanged -= OnEquipmentChanged;
            _inventory.EnemiesKilledChanged -= OnEnemiesKilledChanged;
        }

        if (_lifecycleEventBus != null)
            _lifecycleEventBus.OnEvent -= OnLifecycleEvent;

        if (_islandRegistry == null)
        {
            UnsubscribeNpcRescueSites();
            UnsubscribeShipSites();
            return;
        }

        foreach (var island in _islandRegistry.Islands)
        {
            island.MainPathReached -= OnIslandProgressChanged;
            island.HexUnlocked -= OnIslandHexUnlocked;
            island.OpeningHexagonChanged -= OnOpeningHexagonChanged;
        }

        UnsubscribeNpcRescueSites();
        UnsubscribeShipSites();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void SubscribePlatformSaveTriggers()
    {
        if (Bridge.instance == null)
            return;

        if (Bridge.platform != null)
            Bridge.platform.pauseStateChanged += OnPlatformPauseStateChanged;
    }

    private void UnsubscribePlatformSaveTriggers()
    {
        if (Bridge.instance == null)
            return;

        if (Bridge.platform != null)
            Bridge.platform.pauseStateChanged -= OnPlatformPauseStateChanged;
    }

    private void OnPlatformPauseStateChanged(bool isPaused)
    {
        SaveSystemTestLog.Info($"Bootstrap.OnPlatformPauseStateChanged isPaused={isPaused}");
        if (isPaused)
            PersistNow("platformPause");
    }
#endif

    private void OnProgressChanged(ResourceCell _) => ScheduleAutosave("inventory", collectIslands: false);

    private void OnInstrumentUpgraded(Instrument _) => ScheduleAutosave("instrument", collectIslands: false);

    private void OnEquipmentChanged(Equipment _) => ScheduleAutosave("equipment", collectIslands: false);

    private void OnEnemiesKilledChanged(int _) => ScheduleAutosave("enemiesKilled", collectIslands: false);

    private void OnLifecycleEvent(LifecycleEventData data)
    {
        if (data.Type != LifecycleEventType.EnemyDied || _inventory == null)
            return;

        _inventory.RegisterEnemyKill();
    }

    private void OnIslandProgressChanged(IslandProgressHandler island) =>
        ScheduleAutosave($"mainPath island={island?.IslandIndex}", collectIslands: true);

    private void OnIslandHexUnlocked(IslandProgressHandler island, HexTile hex) =>
        ScheduleAutosave($"hexUnlock island={island?.IslandIndex} q={hex?.Q} r={hex?.R}", collectIslands: true);

    private void OnOpeningHexagonChanged(IslandProgressHandler island, ResourcePaymentOpener _) =>
        ScheduleAutosave($"openingPay island={island?.IslandIndex}", collectIslands: true);

    private void SubscribeNpcRescueSites()
    {
        if (_npcRescueSites == null)
            return;

        for (int i = 0; i < _npcRescueSites.Length; i++)
        {
            if (_npcRescueSites[i] == null)
                continue;

            _npcRescueSites[i].Arrived += OnNpcWorkerArrived;
        }
    }

    private void UnsubscribeNpcRescueSites()
    {
        if (_npcRescueSites == null)
            return;

        for (int i = 0; i < _npcRescueSites.Length; i++)
        {
            if (_npcRescueSites[i] == null)
                continue;

            _npcRescueSites[i].Arrived -= OnNpcWorkerArrived;
        }
    }

    private void OnNpcWorkerArrived() => ScheduleAutosave("npcWorkerArrived", collectIslands: true);

    private void SubscribeShipSites()
    {
        if (_shipSites == null)
            return;

        for (int i = 0; i < _shipSites.Length; i++)
        {
            if (_shipSites[i] == null)
                continue;

            _shipSites[i].Changed += OnShipChanged;
            if (_shipSites[i].TryGetComponent(out ShipTransport transport))
                transport.VoyageCompleted += OnShipChanged;
        }
    }

    private void UnsubscribeShipSites()
    {
        if (_shipSites == null)
            return;

        for (int i = 0; i < _shipSites.Length; i++)
        {
            if (_shipSites[i] == null)
                continue;

            _shipSites[i].Changed -= OnShipChanged;
            if (_shipSites[i].TryGetComponent(out ShipTransport transport))
                transport.VoyageCompleted -= OnShipChanged;
        }
    }

    private void OnShipChanged() => ScheduleAutosave("shipChanged", collectIslands: true);

    private void ScheduleAutosave(string reason, bool collectIslands)
    {
        if (!_saveApplied)
        {
            SaveSystemTestLog.Warn($"Bootstrap.ScheduleAutosave ignored ({reason}) saveApplied=false");
            return;
        }

        bool wasPending = _hasPendingAutosave;
        _hasPendingAutosave = true;
        if (collectIslands)
            _pendingIslandCollect = true;
        _autosaveTimer = AutosaveDebounceSeconds;
        if (!wasPending)
            SaveSystemTestLog.Info($"Bootstrap.ScheduleAutosave reason={reason}");
    }

    private void PersistNow(string reason = "unspecified")
    {
        if (!_saveApplied)
        {
            SaveSystemTestLog.Warn($"Bootstrap.PersistNow ignored ({reason}) saveApplied=false");
            return;
        }

        if (!_allowPersist && !_saveLoader.IsWorldUntrusted)
        {
            SaveSystemTestLog.Warn(
                $"Bootstrap.PersistNow BLOCKED ({reason}) allowPersist=false — " +
                "refusing to overwrite cloud save with an unapplied world state");
            return;
        }

        SaveSystemTestLog.Info($"Bootstrap.PersistNow reason={reason}");
        bool collectIslands = reason != "autosave" || _pendingIslandCollect;
        _autosaveTimer = -1f;
        _hasPendingAutosave = false;
        _pendingIslandCollect = false;
        _timeSinceLastSave = 0f;
        _saveLoader.Save(collectIslands);
    }

    private void SetPersistAllowed(bool allowed)
    {
        _allowPersist = allowed;
        if (_saveLoader != null)
            _saveLoader.PersistAllowed = allowed;

        SaveSystemTestLog.Info(
            $"Bootstrap.SetPersistAllowed {allowed} init={_saveStorage?.InitState}");
    }

    private void UpdatePersistGate(bool worldApplied, bool hadLoadedSave)
    {
        bool storageReady = SavePersistPolicy.CanWriteToStorage(_saveStorage.InitState);
        bool allow = storageReady && (!hadLoadedSave || worldApplied) && !_saveLoader.IsHostWorldMismatch;
        SetPersistAllowed(allow);
        SaveSystemTestLog.Info(
            $"Bootstrap.persistGate allowPersist={_allowPersist} storageReady={storageReady} " +
            $"hadLoadedSave={hadLoadedSave} worldApplied={worldApplied} init={_saveStorage.InitState}");
    }

    private void OnLateStorageDataArrived()
    {
        if (this == null || !isActiveAndEnabled || _lateApplyInProgress)
            return;

        SaveSystemTestLog.Info("Bootstrap.OnLateStorageDataArrived");
        StartCoroutine(ApplyLateStorageCoroutine());
    }

    private IEnumerator ApplyLateStorageCoroutine()
    {
        _lateApplyInProgress = true;
        yield return null;

        try
        {
            LoadAndApplyInventory();
            if (TryRedirectToSavedHostWorld())
                yield break;

            ResolveTutorialAfterSaveLoad();

            bool worldApplied = false;
            try
            {
                ApplyDeferredSaveState();
                worldApplied = true;
            }
            catch (System.Exception exception)
            {
                SaveSystemTestLog.Error($"Bootstrap.late ApplyDeferredSaveState FAILED: {exception}");
            }

            try
            {
                RefreshSystemsAfterSaveApplied();
            }
            catch (System.Exception exception)
            {
                SaveSystemTestLog.Error($"Bootstrap.late RefreshSystemsAfterSaveApplied FAILED: {exception}");
                _saveApplied = true;
            }

            UpdatePersistGate(worldApplied, _saveLoader.HasLoadedSave);

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_allowPersist)
                SubscribePlatformSaveTriggers();
#endif
        }
        finally
        {
            _lateApplyInProgress = false;
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception || _handlingException)
            return;

        if (_saveLoader != null && _saveLoader.IsWorldUntrusted)
            return;

        if (!SaveExceptionPolicy.ShouldMarkWorldUntrusted(stackTrace))
            return;

        _handlingException = true;
        try
        {
            SaveSystemTestLog.Error(
                $"Bootstrap captured save-path exception, marking world untrusted: {condition}\n{stackTrace}");
            _saveLoader?.MarkWorldUntrusted();
        }
        finally
        {
            _handlingException = false;
        }
    }

    private void OnApplicationQuit()
    {
        SaveSystemTestLog.Info("Bootstrap.OnApplicationQuit");
        if (_saveLoader != null && _saveLoader.IsWorldUntrusted)
            _saveLoader.FlushLastKnownGood();
        else
            PersistNow("quit");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        SaveSystemTestLog.Info($"Bootstrap.OnApplicationPause pause={pauseStatus}");
        if (pauseStatus)
            PersistNow("appPause");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SaveSystemTestLog.Info($"Bootstrap.OnApplicationFocus hasFocus={hasFocus}");
        if (!hasFocus)
            PersistNow("appFocusLost");
    }

    private void OnTutorialEnded()
    {
        SaveSystemTestLog.Info("Bootstrap.OnTutorialEnded");
        _saveLoader.MarkTutorialCompleted();
        PersistNow("tutorialEnded");
    }

    private bool TryRedirectToSavedHostWorld()
    {
        if (_saveLoader == null || !_saveLoader.IsHostWorldMismatch)
            return false;

        string sceneName = _saveLoader.ResolveRedirectSceneName();
        SaveSystemTestLog.Warn(
            $"Bootstrap.redirect host mismatch saved={_saveLoader.LoadedSave?.CurrentWorldId} " +
            $"host={_saveLoader.GetHostWorldId()} scene={sceneName}");
        SetPersistAllowed(false);

        if (string.IsNullOrEmpty(sceneName) || sceneName == gameObject.scene.name)
            return false;

        SceneManager.LoadScene(sceneName);
        return true;
    }
}
