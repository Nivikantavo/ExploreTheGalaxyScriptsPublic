using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class CelebrationSystem : MonoBehaviour
{
    public event Action<PendingCelebration> CelebrationTriggered;

    [SerializeField] private CelebrationCatalogSO _catalog;

    private readonly HashSet<string> _triggeredIds = new();
    private readonly Queue<PendingCelebration> _pending = new();
    private readonly Dictionary<ResourceType, int> _acknowledgedLifetimeMilestones = new();
    private readonly Dictionary<int, int> _acknowledgedIslandPercents = new();
    private readonly HashSet<int> _acknowledgedKillThresholds = new();
    private readonly HashSet<string> _acknowledgedOpeners = new();

    private Inventory _inventory;
    private IslandProgressRegistry _islandRegistry;
    private GoalOpenerRegistry _goalOpenerRegistry;
    private ISaveLoader _saveLoader;
    private AllInstruments _allInstruments;
    private AllEquipment _allEquipment;
    private AllResoursesConfig _allResources;
    private IInterstitialCooldown _interstitialCooldown;
    private bool _popupsEnabled;

    public bool HasPending => _pending.Count > 0;

    public IReadOnlyCollection<string> TriggeredCelebrationIds => _triggeredIds;

    [Inject]
    private void Construct(
        Inventory inventory,
        IslandProgressRegistry islandRegistry,
        GoalOpenerRegistry goalOpenerRegistry,
        ISaveLoader saveLoader,
        AllInstruments allInstruments,
        AllEquipment allEquipment,
        AllResoursesConfig allResources,
        IInterstitialCooldown interstitialCooldown)
    {
        _inventory = inventory;
        _islandRegistry = islandRegistry;
        _goalOpenerRegistry = goalOpenerRegistry;
        _saveLoader = saveLoader;
        _allInstruments = allInstruments;
        _allEquipment = allEquipment;
        _allResources = allResources;
        _interstitialCooldown = interstitialCooldown;
    }

    private void Awake()
    {
        Subscribe();
        RestoreTriggeredFromSave();
        SeedAcknowledgedProgress();
    }

    private void Start()
    {
        if (_popupsEnabled)
            return;

        SeedAcknowledgedProgress();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public bool TryDequeue(out PendingCelebration celebration)
    {
        if (_pending.Count == 0)
        {
            celebration = default;
            return false;
        }

        celebration = _pending.Dequeue();
        return true;
    }

    public string[] CreateTriggeredIdsSaveData()
    {
        if (_triggeredIds.Count == 0)
            return Array.Empty<string>();

        var result = new string[_triggeredIds.Count];
        _triggeredIds.CopyTo(result);
        return result;
    }

    public void RefreshFromSave()
    {
        RestoreTriggeredFromSave();
        _pending.Clear();
        SeedAcknowledgedProgress();
        _popupsEnabled = true;
    }

    private void Subscribe()
    {
        if (_inventory != null)
        {
            _inventory.InstrumentUpgraded += OnInstrumentUpgraded;
            _inventory.EquipmentChanged += OnEquipmentChanged;
            _inventory.LifetimeResourceChanged += OnLifetimeResourceChanged;
            _inventory.EnemiesKilledChanged += OnEnemiesKilledChanged;
        }

        if (_islandRegistry != null)
        {
            _islandRegistry.IslandRegistered += OnIslandRegistered;
            _islandRegistry.IslandUnregistered += OnIslandUnregistered;
            foreach (IslandProgressHandler island in _islandRegistry.Islands)
                SubscribeIsland(island);
        }

        if (_goalOpenerRegistry != null)
        {
            _goalOpenerRegistry.OpenerRegistered += OnOpenerRegistered;
            _goalOpenerRegistry.PaymentOpenerChanged += OnPaymentOpenerChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_inventory != null)
        {
            _inventory.InstrumentUpgraded -= OnInstrumentUpgraded;
            _inventory.EquipmentChanged -= OnEquipmentChanged;
            _inventory.LifetimeResourceChanged -= OnLifetimeResourceChanged;
            _inventory.EnemiesKilledChanged -= OnEnemiesKilledChanged;
        }

        if (_islandRegistry != null)
        {
            _islandRegistry.IslandRegistered -= OnIslandRegistered;
            _islandRegistry.IslandUnregistered -= OnIslandUnregistered;
            foreach (IslandProgressHandler island in _islandRegistry.Islands)
                UnsubscribeIsland(island);
        }

        if (_goalOpenerRegistry != null)
        {
            _goalOpenerRegistry.OpenerRegistered -= OnOpenerRegistered;
            _goalOpenerRegistry.PaymentOpenerChanged -= OnPaymentOpenerChanged;
        }
    }

    private void OnIslandRegistered(IslandProgressHandler island)
    {
        SubscribeIsland(island);
        if (island == null || _acknowledgedIslandPercents.ContainsKey(island.IslandIndex))
            return;

        _acknowledgedIslandPercents[island.IslandIndex] = island.GetOpenPercent();
    }

    private void OnIslandUnregistered(IslandProgressHandler island) => UnsubscribeIsland(island);

    private void SubscribeIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.HexUnlocked -= OnIslandHexUnlocked;
        island.HexUnlocked += OnIslandHexUnlocked;
    }

    private void UnsubscribeIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.HexUnlocked -= OnIslandHexUnlocked;
    }

    private void RestoreTriggeredFromSave()
    {
        _triggeredIds.Clear();

        if (!_saveLoader.HasLoadedSave || _saveLoader.LoadedSave?.TriggeredCelebrationIds == null)
            return;

        foreach (string id in _saveLoader.LoadedSave.TriggeredCelebrationIds)
        {
            if (!string.IsNullOrEmpty(id))
                _triggeredIds.Add(id);
        }
    }

    private void SeedAcknowledgedProgress()
    {
        _acknowledgedLifetimeMilestones.Clear();
        _acknowledgedIslandPercents.Clear();
        _acknowledgedKillThresholds.Clear();
        _acknowledgedOpeners.Clear();

        LifetimeResourceCelebrationSO lifetime = FindLifetimeCelebration();
        if (_inventory != null)
        {
            foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
            {
                int amount = _inventory.GetLifetimeAmount(resourceType);
                int milestone = lifetime != null
                    ? lifetime.HighestReached(amount)
                    : LifetimeResourceMilestones.HighestReached(amount, LifetimeResourceMilestones.DefaultStep);
                if (milestone > 0)
                    _acknowledgedLifetimeMilestones[resourceType] = milestone;
            }

            foreach (EnemyKillsCelebrationSO enemy in Enumerate<EnemyKillsCelebrationSO>())
            {
                if (enemy.KillThreshold > 0 && _inventory.EnemiesKilled >= enemy.KillThreshold)
                    _acknowledgedKillThresholds.Add(enemy.KillThreshold);
            }
        }

        if (_islandRegistry != null)
        {
            foreach (IslandProgressHandler island in _islandRegistry.Islands)
            {
                if (island == null)
                    continue;

                _acknowledgedIslandPercents[island.IslandIndex] = island.GetOpenPercent();
            }
        }

        if (_goalOpenerRegistry != null)
        {
            foreach (OpenerOpenedCelebrationSO opener in Enumerate<OpenerOpenedCelebrationSO>())
            {
                if (!string.IsNullOrEmpty(opener.GoalOpenerId) && _goalOpenerRegistry.IsFilled(opener.GoalOpenerId))
                    _acknowledgedOpeners.Add(opener.GoalOpenerId);
            }
        }
    }

    private void OnInstrumentUpgraded(Instrument instrument)
    {
        if (!_popupsEnabled || instrument == null)
            return;

        InstrumentUpgradeCelebrationSO celebration = FindInstrumentCelebration(instrument.Type);
        if (celebration == null)
            return;

        Enqueue(celebration.CreatePending(instrument.Level, FindInstrumentIcon(instrument.Type, instrument.Level)));
    }

    private void OnEquipmentChanged(Equipment equipment)
    {
        if (!_popupsEnabled || equipment == null)
            return;

        EquipmentUpgradeCelebrationSO celebration = FindEquipmentCelebration(equipment.Type);
        if (celebration == null)
            return;

        Enqueue(celebration.CreatePending(equipment.Level, FindEquipmentIcon(equipment.Type, equipment.Level)));
    }

    private void OnLifetimeResourceChanged(ResourceType resourceType, int lifetimeAmount)
    {
        LifetimeResourceCelebrationSO celebration = FindLifetimeCelebration();
        int step = celebration != null ? celebration.StepAmount : LifetimeResourceMilestones.DefaultStep;
        int acknowledged = _acknowledgedLifetimeMilestones.GetValueOrDefault(resourceType);
        int milestone = LifetimeResourceMilestones.ConsumeNewlyReached(lifetimeAmount, step, ref acknowledged);
        _acknowledgedLifetimeMilestones[resourceType] = acknowledged;
        if (milestone <= 0 || celebration == null)
            return;

        TryEnqueueGated(celebration.CreatePending(resourceType, milestone, FindResourceIcon(resourceType)));
    }

    private void OnEnemiesKilledChanged(int kills)
    {
        var thresholds = new List<int>();
        foreach (EnemyKillsCelebrationSO enemy in Enumerate<EnemyKillsCelebrationSO>())
            thresholds.Add(enemy.KillThreshold);

        int? highest = GatedThresholdRules.ConsumeHighestNewlyReached(kills, thresholds, _acknowledgedKillThresholds);
        if (highest == null)
            return;

        EnemyKillsCelebrationSO celebration = FindEnemyCelebration(highest.Value);
        if (celebration == null)
            return;

        TryEnqueueGated(celebration.CreatePending());
    }

    private void OnIslandHexUnlocked(IslandProgressHandler island, HexTile _)
    {
        if (island == null)
            return;

        int acknowledged = _acknowledgedIslandPercents.GetValueOrDefault(island.IslandIndex, -1);
        if (!IslandPercentCelebrationRules.TryConsumeIncreasedPercent(
                island.GetOpenPercent(),
                ref acknowledged,
                out int percent))
        {
            _acknowledgedIslandPercents[island.IslandIndex] = acknowledged;
            return;
        }

        _acknowledgedIslandPercents[island.IslandIndex] = acknowledged;

        IslandPercentCelebrationSO celebration = FindIslandCelebration();
        if (celebration == null)
            return;

        TryEnqueueGated(celebration.CreatePending(percent));
    }

    private void OnOpenerRegistered(ResourcePaymentOpener opener)
    {
        if (opener == null || string.IsNullOrEmpty(opener.GoalOpenerId) || !opener.IsGoalComplete())
            return;

        _acknowledgedOpeners.Add(opener.GoalOpenerId);
    }

    private void OnPaymentOpenerChanged(ResourcePaymentOpener opener)
    {
        if (opener == null || string.IsNullOrEmpty(opener.GoalOpenerId) || !opener.IsGoalComplete())
            return;

        if (!_acknowledgedOpeners.Add(opener.GoalOpenerId))
            return;

        OpenerOpenedCelebrationSO celebration = FindOpenerCelebration(opener.GoalOpenerId);
        if (celebration == null)
            return;

        TryEnqueueGated(celebration.CreatePending());
    }

    private void TryEnqueueGated(PendingCelebration celebration)
    {
        if (!_popupsEnabled)
            return;

        if (_interstitialCooldown != null && !_interstitialCooldown.CanShowGatedCelebration())
            return;

        _interstitialCooldown?.MarkGatedCelebrationQueued();
        Enqueue(celebration);
    }

    private void Enqueue(PendingCelebration celebration)
    {
        _pending.Enqueue(celebration);
        CelebrationTriggered?.Invoke(celebration);
    }

    private InstrumentUpgradeCelebrationSO FindInstrumentCelebration(InstrumentType instrumentType)
    {
        foreach (InstrumentUpgradeCelebrationSO celebration in Enumerate<InstrumentUpgradeCelebrationSO>())
        {
            if (celebration.InstrumentType == instrumentType)
                return celebration;
        }

        return null;
    }

    private EquipmentUpgradeCelebrationSO FindEquipmentCelebration(EquipmentType equipmentType)
    {
        foreach (EquipmentUpgradeCelebrationSO celebration in Enumerate<EquipmentUpgradeCelebrationSO>())
        {
            if (celebration.EquipmentType == equipmentType)
                return celebration;
        }

        return null;
    }

    private IslandPercentCelebrationSO FindIslandCelebration()
    {
        foreach (IslandPercentCelebrationSO celebration in Enumerate<IslandPercentCelebrationSO>())
            return celebration;

        return null;
    }

    private LifetimeResourceCelebrationSO FindLifetimeCelebration()
    {
        foreach (LifetimeResourceCelebrationSO celebration in Enumerate<LifetimeResourceCelebrationSO>())
            return celebration;

        return null;
    }

    private EnemyKillsCelebrationSO FindEnemyCelebration(int threshold)
    {
        foreach (EnemyKillsCelebrationSO celebration in Enumerate<EnemyKillsCelebrationSO>())
        {
            if (celebration.KillThreshold == threshold)
                return celebration;
        }

        return null;
    }

    private OpenerOpenedCelebrationSO FindOpenerCelebration(string goalOpenerId)
    {
        foreach (OpenerOpenedCelebrationSO celebration in Enumerate<OpenerOpenedCelebrationSO>())
        {
            if (celebration.GoalOpenerId == goalOpenerId)
                return celebration;
        }

        return null;
    }

    private IEnumerable<T> Enumerate<T>() where T : CelebrationDefinitionSO
    {
        if (_catalog == null)
            yield break;

        foreach (CelebrationDefinitionSO celebration in _catalog.Celebrations)
        {
            if (celebration is T match)
                yield return match;
        }
    }

    private Sprite FindInstrumentIcon(InstrumentType instrumentType, int level)
    {
        if (_allInstruments == null)
            return null;

        IReadOnlyList<InstrumentConfig> configs = instrumentType switch
        {
            InstrumentType.Axe => _allInstruments.Axes,
            InstrumentType.Pickaxe => _allInstruments.Pickaxes,
            InstrumentType.Sword => _allInstruments.Swords,
            _ => null
        };

        if (configs == null)
            return null;

        for (int i = 0; i < configs.Count; i++)
        {
            InstrumentConfig config = configs[i];
            if (config != null && config.Level == level)
                return config.Icon;
        }

        return null;
    }

    private Sprite FindEquipmentIcon(EquipmentType equipmentType, int level)
    {
        return _allEquipment != null ? _allEquipment.Get(equipmentType, level)?.Icon : null;
    }

    private Sprite FindResourceIcon(ResourceType resourceType)
    {
        if (_allResources?.Resourses == null)
            return null;

        for (int i = 0; i < _allResources.Resourses.Count; i++)
        {
            ResourseConfig config = _allResources.Resourses[i];
            if (config != null && config.ResourseType == resourceType)
                return config.Icon;
        }

        return null;
    }
}
