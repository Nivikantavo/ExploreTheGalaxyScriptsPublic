using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class AchievementSystem : MonoBehaviour
{
    public event Action AchievementUnlocked;

    [SerializeField] private AchievementCatalogSO _catalog;

    private readonly HashSet<string> _unlockedIds = new();

    private Inventory _inventory;
    private IslandProgressRegistry _islandRegistry;
    private GoalOpenerRegistry _goalOpenerRegistry;
    private ISaveLoader _saveLoader;
    private AchievementEvaluationContext _context;
    private bool _trackingEnabled;

    public IReadOnlyCollection<string> UnlockedAchievementIds => _unlockedIds;

    [Inject]
    private void Construct(
        Inventory inventory,
        IslandProgressRegistry islandRegistry,
        GoalOpenerRegistry goalOpenerRegistry,
        ISaveLoader saveLoader)
    {
        _inventory = inventory;
        _islandRegistry = islandRegistry;
        _goalOpenerRegistry = goalOpenerRegistry;
        _saveLoader = saveLoader;
        _context = new AchievementEvaluationContext(_inventory, _islandRegistry, _goalOpenerRegistry);
    }

    private void Awake()
    {
        Subscribe();
        RestoreFromSave();
        EvaluateAchievements(emitEvents: false);
    }

    private void Start()
    {
        EvaluateAchievements(emitEvents: false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void RefreshFromSave()
    {
        RestoreFromSave();
        EvaluateAchievements(emitEvents: false);
        _trackingEnabled = true;
    }

    public string[] CreateUnlockedIdsSaveData()
    {
        if (_unlockedIds.Count == 0)
            return Array.Empty<string>();

        var result = new string[_unlockedIds.Count];
        _unlockedIds.CopyTo(result);
        return result;
    }

    public void CollectListEntries(List<AchievementListEntry> results)
    {
        if (results == null || _catalog == null)
            return;

        foreach (AchievementDefinitionSO achievement in _catalog.Achievements)
        {
            if (achievement == null || string.IsNullOrEmpty(achievement.Id))
                continue;

            bool unlocked = _unlockedIds.Contains(achievement.Id);
            results.Add(achievement.CreateListEntry(unlocked));
        }
    }

    private void Subscribe()
    {
        if (_inventory != null)
        {
            _inventory.InstrumentUpgraded += OnProgressChanged;
            _inventory.LifetimeResourceChanged += OnLifetimeResourceChanged;
            _inventory.EnemiesKilledChanged += OnEnemiesKilledChanged;
        }

        if (_islandRegistry != null)
        {
            foreach (var island in _islandRegistry.Islands)
                island.HexUnlocked += OnIslandHexUnlocked;
        }

        if (_goalOpenerRegistry != null)
            _goalOpenerRegistry.PaymentOpenerChanged += OnPaymentOpenerChanged;
    }

    private void Unsubscribe()
    {
        if (_inventory != null)
        {
            _inventory.InstrumentUpgraded -= OnProgressChanged;
            _inventory.LifetimeResourceChanged -= OnLifetimeResourceChanged;
            _inventory.EnemiesKilledChanged -= OnEnemiesKilledChanged;
        }

        if (_islandRegistry != null)
        {
            foreach (var island in _islandRegistry.Islands)
                island.HexUnlocked -= OnIslandHexUnlocked;
        }

        if (_goalOpenerRegistry != null)
            _goalOpenerRegistry.PaymentOpenerChanged -= OnPaymentOpenerChanged;
    }

    private void RestoreFromSave()
    {
        _unlockedIds.Clear();

        if (!_saveLoader.HasLoadedSave || _saveLoader.LoadedSave?.TriggeredAchievementIds == null)
            return;

        foreach (string id in _saveLoader.LoadedSave.TriggeredAchievementIds)
        {
            if (!string.IsNullOrEmpty(id))
                _unlockedIds.Add(id);
        }
    }

    private void OnProgressChanged(Instrument _) => EvaluateAchievements(emitEvents: _trackingEnabled);

    private void OnLifetimeResourceChanged(ResourceType _, int __) =>
        EvaluateAchievements(emitEvents: _trackingEnabled);

    private void OnEnemiesKilledChanged(int _) => EvaluateAchievements(emitEvents: _trackingEnabled);

    private void OnIslandHexUnlocked(IslandProgressHandler _, HexTile __) =>
        EvaluateAchievements(emitEvents: _trackingEnabled);

    private void OnPaymentOpenerChanged(ResourcePaymentOpener _) =>
        EvaluateAchievements(emitEvents: _trackingEnabled);

    private void EvaluateAchievements(bool emitEvents)
    {
        if (_catalog == null || _context == null)
            return;

        bool unlockedAny = false;
        foreach (AchievementDefinitionSO achievement in _catalog.Achievements)
        {
            if (achievement == null)
                continue;

            string id = achievement.Id;
            if (string.IsNullOrEmpty(id) || _unlockedIds.Contains(id))
                continue;

            if (!achievement.IsSatisfied(_context))
                continue;

            _unlockedIds.Add(id);
            unlockedAny = true;
        }

        if (emitEvents && unlockedAny)
            AchievementUnlocked?.Invoke();
    }
}
