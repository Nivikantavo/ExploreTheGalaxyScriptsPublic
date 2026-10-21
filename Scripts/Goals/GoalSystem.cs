using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class GoalSystem : MonoBehaviour
{
    public event Action<GoalDefinitionSO> CurrentGoalChanged;
    public event Action<GoalDefinitionSO> GoalCompleted;

    [SerializeField] private GoalSequenceConfigSO _sequenceConfig;

    public GoalDefinitionSO CurrentGoal
    {
        get
        {
            TryResolveCurrent(out GoalDefinitionSO goal, out _);
            return goal;
        }
    }

    public int CompletedGoalsCount { get; private set; }

    private Inventory _inventory;
    private IslandProgressRegistry _islandRegistry;
    private GoalOpenerRegistry _goalOpenerRegistry;
    private NpcRescueRegistry _npcRescueRegistry;
    private ISaveLoader _saveLoader;
    private GoalEvaluationContext _context;
    private SceneTravelService _travelService;

    private string LocalWorldId => GoalSequenceProgress.ResolveWorldId(_sequenceConfig);

    [Inject]
    private void Construct(
        Inventory inventory,
        IslandProgressRegistry islandRegistry,
        GoalOpenerRegistry goalOpenerRegistry,
        NpcRescueRegistry npcRescueRegistry,
        ISaveLoader saveLoader)
    {
        _inventory = inventory;
        _islandRegistry = islandRegistry;
        _goalOpenerRegistry = goalOpenerRegistry;
        _npcRescueRegistry = npcRescueRegistry;
        _saveLoader = saveLoader;
        _context = new GoalEvaluationContext(_inventory, _islandRegistry, _goalOpenerRegistry, _npcRescueRegistry);
    }

    private void Awake()
    {
        Subscribe();
        RestoreProgressFromSave();
        TryAdvanceGoals(emitCompletionEvents: false);
        SafeNotifyCurrentGoalChanged();
    }

    private void Start()
    {
        // Scene revert can change Awake order vs Zenject inject; re-bind progress listeners.
        Unsubscribe();
        Subscribe();
        SubscribeTravel();
        TryAdvanceGoals(emitCompletionEvents: false);
        SafeNotifyCurrentGoalChanged();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UnsubscribeTravel();
    }

    public string GetCurrentGoalText()
    {
        if (CurrentGoal == null)
            return "All goals completed";

        try
        {
            return CurrentGoal.BuildDescription(_context);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(GoalSystem)}.{nameof(GetCurrentGoalText)} failed: {exception.Message}");
            return string.Empty;
        }
    }

    public Sprite GetCurrentGoalIcon()
    {
        return CurrentGoal != null ? CurrentGoal.Icon : null;
    }

    public string GetCurrentGoalTargetId()
    {
        return CurrentGoal != null ? CurrentGoal.TargetId : string.Empty;
    }

    public GoalTravelHint GetCurrentTravelHint()
    {
        if (!TryResolveCurrent(out GoalDefinitionSO goal, out GoalSequenceConfigSO sequence))
            return GoalTravelHint.None;

        return GoalTravelHintResolver.Resolve(goal, sequence, CapturePlayerLocation());
    }

    private void Subscribe()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged += OnProgressChanged;
            _inventory.InstrumentUpgraded += OnInstrumentUpgraded;
            _inventory.EquipmentChanged += OnEquipmentChanged;
        }

        if (_islandRegistry != null)
        {
            _islandRegistry.IslandRegistered += OnIslandRegistered;
            _islandRegistry.IslandUnregistered += OnIslandUnregistered;

            foreach (var island in _islandRegistry.Islands)
                SubscribeIsland(island);
        }

        if (_goalOpenerRegistry != null)
            _goalOpenerRegistry.PaymentOpenerChanged += OnPaymentOpenerChanged;

        if (_npcRescueRegistry != null)
            _npcRescueRegistry.Changed += OnNpcRescueChanged;
    }

    private void Unsubscribe()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged -= OnProgressChanged;
            _inventory.InstrumentUpgraded -= OnInstrumentUpgraded;
            _inventory.EquipmentChanged -= OnEquipmentChanged;
        }

        if (_islandRegistry != null)
        {
            _islandRegistry.IslandRegistered -= OnIslandRegistered;
            _islandRegistry.IslandUnregistered -= OnIslandUnregistered;

            foreach (var island in _islandRegistry.Islands)
                UnsubscribeIsland(island);
        }

        if (_goalOpenerRegistry != null)
            _goalOpenerRegistry.PaymentOpenerChanged -= OnPaymentOpenerChanged;

        if (_npcRescueRegistry != null)
            _npcRescueRegistry.Changed -= OnNpcRescueChanged;
    }

    private void SubscribeTravel()
    {
        if (_travelService != null)
            return;

        _travelService = SceneTravelService.Instance;
        if (_travelService != null)
            _travelService.TravelCompleted += OnTravelCompleted;
    }

    private void UnsubscribeTravel()
    {
        if (_travelService == null)
            return;

        _travelService.TravelCompleted -= OnTravelCompleted;
        _travelService = null;
    }

    private void OnTravelCompleted()
    {
        SafeNotifyCurrentGoalChanged();
    }

    public void RefreshFromSave()
    {
        RestoreProgressFromSave();
        TryAdvanceGoals(emitCompletionEvents: false);
        SafeNotifyCurrentGoalChanged();
    }

    private void SafeNotifyCurrentGoalChanged()
    {
        try
        {
            CurrentGoalChanged?.Invoke(CurrentGoal);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(GoalSystem)} UI notify failed: {exception.Message}");
        }
    }

    private void RestoreProgressFromSave()
    {
        if (_saveLoader == null || !_saveLoader.HasLoadedSave || _saveLoader.LoadedSave == null)
        {
            CompletedGoalsCount = 0;
            return;
        }

        int savedCount = SaveWorldAccess.GetCompletedGoalsCount(_saveLoader.LoadedSave, LocalWorldId);
        int maxCount = GoalSequenceProgress.GetSameWorldGoalCount(_sequenceConfig, LocalWorldId);
        CompletedGoalsCount = Mathf.Clamp(savedCount, 0, maxCount);
    }

    private void OnProgressChanged(ResourceCell _)
    {
        TryAdvanceGoals(emitCompletionEvents: true);
        SafeNotifyCurrentGoalChanged();
    }

    private void OnInstrumentUpgraded(Instrument _)
    {
        TryAdvanceGoals(emitCompletionEvents: true);
        SafeNotifyCurrentGoalChanged();
    }

    private void OnEquipmentChanged(Equipment _)
    {
        TryAdvanceGoals(emitCompletionEvents: true);
        SafeNotifyCurrentGoalChanged();
    }

    private void OnNpcRescueChanged()
    {
        TryAdvanceGoals(emitCompletionEvents: true);
        SafeNotifyCurrentGoalChanged();
    }

    private void OnIslandRegistered(IslandProgressHandler island)
    {
        SubscribeIsland(island);
        OnIslandProgressChanged();
    }

    private void OnIslandUnregistered(IslandProgressHandler island)
    {
        UnsubscribeIsland(island);
    }

    private void SubscribeIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.MainPathReached += OnIslandMainPathReached;
        island.HexUnlocked += OnIslandHexUnlocked;
    }

    private void UnsubscribeIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.MainPathReached -= OnIslandMainPathReached;
        island.HexUnlocked -= OnIslandHexUnlocked;
    }

    private void OnIslandMainPathReached(IslandProgressHandler _)
    {
        OnIslandProgressChanged();
    }

    private void OnIslandHexUnlocked(IslandProgressHandler _, HexTile __)
    {
        OnIslandProgressChanged();
    }

    private void OnPaymentOpenerChanged(ResourcePaymentOpener _)
    {
        OnIslandProgressChanged();
    }

    private void OnIslandProgressChanged()
    {
        TryAdvanceGoals(emitCompletionEvents: true);
        SafeNotifyCurrentGoalChanged();
    }

    private void TryAdvanceGoals(bool emitCompletionEvents)
    {
        if (_sequenceConfig == null)
            return;

        if (_context == null)
            _context = new GoalEvaluationContext(_inventory, _islandRegistry, _goalOpenerRegistry, _npcRescueRegistry);

        while (TryGetAdvanceableGoal(out GoalDefinitionSO currentGoal) && currentGoal.IsCompleted(_context))
        {
            CompletedGoalsCount++;

            if (emitCompletionEvents)
                GoalCompleted?.Invoke(currentGoal);
        }
    }

    private bool TryGetAdvanceableGoal(out GoalDefinitionSO goal)
    {
        if (!TryResolveCurrent(out goal, out GoalSequenceConfigSO sequence))
            return false;

        return GoalSequenceProgress.IsOnWorld(sequence, LocalWorldId);
    }

    private bool TryResolveCurrent(out GoalDefinitionSO goal, out GoalSequenceConfigSO sequence)
    {
        return GoalSequenceProgress.TryResolve(
            _sequenceConfig,
            CompletedGoalsCount,
            LocalWorldId,
            GetCompletedCountForWorld,
            out goal,
            out sequence,
            GetSequenceForWorld);
    }

    private int GetCompletedCountForWorld(string worldId)
    {
        if (string.Equals(worldId, LocalWorldId, StringComparison.Ordinal))
            return CompletedGoalsCount;

        if (_saveLoader == null || _saveLoader.LoadedSave == null)
            return 0;

        return SaveWorldAccess.GetCompletedGoalsCount(_saveLoader.LoadedSave, worldId);
    }

    private GoalSequenceConfigSO GetSequenceForWorld(string worldId)
    {
        if (string.IsNullOrEmpty(worldId))
            return null;

        var visited = new HashSet<GoalSequenceConfigSO>();
        GoalSequenceConfigSO config = _sequenceConfig;
        while (config != null && visited.Add(config))
        {
            if (string.Equals(GoalSequenceProgress.ResolveWorldId(config), worldId, StringComparison.Ordinal))
                return config;

            config = config.NextSequence;
        }

        WorldCatalogSO catalog = FindLoadedWorldCatalog();
        GoalSequenceConfigSO fromCatalog = WorldCatalog.FindGoalSequence(worldId, catalog != null ? catalog.Worlds : null);
        if (fromCatalog != null)
            return fromCatalog;

        GoalSequenceConfigSO[] loaded = Resources.FindObjectsOfTypeAll<GoalSequenceConfigSO>();
        for (int i = 0; i < loaded.Length; i++)
        {
            GoalSequenceConfigSO candidate = loaded[i];
            if (candidate == null)
                continue;

            if (string.Equals(GoalSequenceProgress.ResolveWorldId(candidate), worldId, StringComparison.Ordinal))
                return candidate;
        }

#if UNITY_EDITOR
        if (string.Equals(worldId, SaveWorldIds.World2, StringComparison.Ordinal))
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GoalSequenceConfigSO>(
                "Assets/Configs/Goal/GoalSequenceConfigWorld2.asset");
        }
#endif

        return null;
    }

    private static WorldCatalogSO FindLoadedWorldCatalog()
    {
        PlanetTravelPanelView panel = UnityEngine.Object.FindFirstObjectByType<PlanetTravelPanelView>(
            FindObjectsInactive.Include);
        if (panel != null && panel.WorldCatalog != null)
            return panel.WorldCatalog;

        WorldCatalogSO[] catalogs = Resources.FindObjectsOfTypeAll<WorldCatalogSO>();
        for (int i = 0; i < catalogs.Length; i++)
        {
            WorldCatalogSO catalog = catalogs[i];
            if (catalog != null && catalog.Worlds != null && catalog.Worlds.Length > 0)
                return catalog;
        }

        return null;
    }

    private GoalPlayerLocation CapturePlayerLocation()
    {
        SceneTravelService travel = _travelService != null ? _travelService : SceneTravelService.Instance;
        string hostWorldId = _saveLoader != null
            ? _saveLoader.GetHostWorldId()
            : LocalWorldId;

        return new GoalPlayerLocation(
            travel != null && travel.IsInDungeon,
            travel != null ? travel.CurrentDungeonSceneName : null,
            hostWorldId);
    }
}
