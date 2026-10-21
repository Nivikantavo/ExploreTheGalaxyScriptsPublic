using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class EquipmentUpgrader : MonoBehaviour
{
    public EquipmentType EquipmentType => _equipmentType;
    public InstrumentUpgraderData Data { get; private set; }
    public TimedProcessState ProcessState { get; private set; } = TimedProcessState.Idle;
    public InstrumentUpgraderIdleView IdleView => _idleView;
    public InstrumentUpgraderProgressView ProgressView => _progressView;
    public InstrumentUpgraderComplitedView ComplitedView => _complitedView;
    public InstrumentUpgraderMaxLevelView MaxLevelView => _maxLevelView;

    [SerializeField] private InstrumentUpgraderIdleView _idleView;
    [SerializeField] private InstrumentUpgraderProgressView _progressView;
    [SerializeField] private InstrumentUpgraderComplitedView _complitedView;
    [SerializeField] private InstrumentUpgraderMaxLevelView _maxLevelView;
    [SerializeField] private EquipmentType _equipmentType;
    [SerializeField] private ApproachUIShower _approachUIShower;

    private AllEquipment _allEquipment;
    private Inventory _inventory;
    private EquipmentConfig _equipmentConfig;
    private StateMachine _stateMachine;
    private bool _initialized;

    [Inject]
    private void Construct(AllEquipment allEquipment, Inventory inventory)
    {
        _allEquipment = allEquipment;
        _inventory = inventory;
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (_approachUIShower != null)
            _approachUIShower.OnPlayerEnter -= OnPlayerEnter;
    }

    public void Initialize()
    {
        if (_initialized)
        {
            SyncOfferFromInventory();
            return;
        }

        _initialized = true;
        SetCurrentSaleEquipment();

        _stateMachine = new StateMachine();
        var idle = new EquipmentUpgraderIdle(_stateMachine as IStateSwitcher, Data, this);
        var processing = new EquipmentUpgraderProcessing(_stateMachine as IStateSwitcher, Data, this);
        var completed = new EquipmentUpgraderComplited(_stateMachine as IStateSwitcher, Data, this);
        var maxLevel = new EquipmentUpgraderMaxLevel(_stateMachine as IStateSwitcher, Data, this);

        _stateMachine.Initialize(Data.IsMaxLevel
            ? new List<State> { maxLevel, idle, processing, completed }
            : new List<State> { idle, processing, completed, maxLevel });

        if (_approachUIShower != null)
            _approachUIShower.OnPlayerEnter += OnPlayerEnter;
    }

    public void ApplyEquipment()
    {
        _inventory.ApplyEquipment(_equipmentConfig);
        RefreshFromInventory();
    }

    public void RefreshFromInventory()
    {
        SetCurrentSaleEquipment();
        ProcessState = TimedProcessState.Idle;

        if (_stateMachine == null)
            return;

        if (Data.IsMaxLevel)
            _stateMachine.ChangeState<EquipmentUpgraderMaxLevel>();
        else
            _stateMachine.ChangeState<EquipmentUpgraderIdle>();
    }

    public EquipmentUpgradeSaveData CreateSaveData()
    {
        if (ProcessState == TimedProcessState.Idle || Data == null || Data.IsMaxLevel)
            return null;

        return new EquipmentUpgradeSaveData
        {
            EquipmentType = EquipmentType,
            State = ProcessState,
            TargetLevel = Data.NextLevel,
            TotalDuration = Data.UpgradeTime,
            StartUtcTicks = Data.ProcessStartUtcTicks
        };
    }

    public void ApplySave(ISaveLoader saveLoader)
    {
        var save = saveLoader.GetEquipmentUpgradeSave(EquipmentType);
        if (save == null)
            return;

        if (Data == null || _stateMachine == null)
        {
            SaveSystemTestLog.Warn(
                $"EquipmentUpgrader.ApplySave skipped type={EquipmentType} " +
                $"dataNull={Data == null} stateMachineNull={_stateMachine == null}");
            return;
        }

        if (Data.IsMaxLevel)
            return;

        if (save.TargetLevel > 0 && save.TargetLevel != Data.NextLevel)
        {
            SaveSystemTestLog.Warn(
                $"EquipmentUpgrader.ApplySave skipped stale type={EquipmentType} " +
                $"savedTarget={save.TargetLevel} next={Data.NextLevel}");
            return;
        }

        Data.ProcessStartUtcTicks = save.StartUtcTicks;
        int elapsed = SaveLoadSystem.CalculateElapsedSeconds(save.StartUtcTicks, save.TotalDuration);
        Data.ElapsedTime = elapsed;

        if (save.State == TimedProcessState.Completed || elapsed >= save.TotalDuration)
        {
            ProcessState = TimedProcessState.Completed;
            _stateMachine.ChangeState<EquipmentUpgraderComplited>();
            return;
        }

        ProcessState = TimedProcessState.InProgress;
        _stateMachine.ChangeState<EquipmentUpgraderProcessing>();
    }

    public void SetProcessState(TimedProcessState processState) => ProcessState = processState;

    public int GetResourceAmount(ResourceType resourseType)
    {
        return _inventory.GetResourceAmount(resourseType);
    }

    public void SpendResource(ResourceType resourseType, int amount)
    {
        _inventory.RemoveResource(resourseType, amount);
    }

    public void RefreshResourceAvailability()
    {
        if (Data == null || Data.IsMaxLevel)
            return;

        Dictionary<ResourceType, bool> resourceAvailable = new Dictionary<ResourceType, bool>();
        foreach (var price in Data.Prices)
        {
            resourceAvailable.Add(price.Key, GetResourceAmount(price.Key) >= price.Value);
        }

        Data.PlayerEnter?.Invoke(resourceAvailable);
    }

    private void SyncOfferFromInventory()
    {
        if (_inventory == null)
            return;

        int ownedLevel = _inventory.GetEquipmentLevel(EquipmentType);
        int nextLevel = ownedLevel + 1;
        EquipmentConfig nextConfig = _allEquipment != null ? _allEquipment.Get(EquipmentType, nextLevel) : null;
        bool isMaxLevel = nextConfig == null;
        bool offerMatches = Data != null
            && Data.IsMaxLevel == isMaxLevel
            && (isMaxLevel ? Data.CurrentLevel == ownedLevel : Data.NextLevel == nextLevel);

        if (offerMatches)
            return;

        SetCurrentSaleEquipment();
        ProcessState = TimedProcessState.Idle;

        if (_stateMachine == null || Data == null)
            return;

        if (Data.IsMaxLevel)
            _stateMachine.ChangeState<EquipmentUpgraderMaxLevel>();
        else
            _stateMachine.ChangeState<EquipmentUpgraderIdle>();
    }

    private void SetCurrentSaleEquipment()
    {
        int currentLevel = _inventory.GetEquipmentLevel(EquipmentType);
        int nextLevel = currentLevel + 1;

        _equipmentConfig = _allEquipment != null ? _allEquipment.Get(EquipmentType, nextLevel) : null;

        if (Data == null)
            Data = new InstrumentUpgraderData();

        if (_equipmentConfig == null)
        {
            EquipmentConfig maxLevelConfig = _allEquipment != null
                ? _allEquipment.Get(EquipmentType, currentLevel)
                : null;
            Data.InitializeMaxLevel(currentLevel, maxLevelConfig != null ? maxLevelConfig.Icon : null);
            return;
        }

        Data.Initialize(
            currentLevel,
            nextLevel,
            _equipmentConfig.UpgradeTime,
            _equipmentConfig.Price,
            _equipmentConfig.Icon);
    }

    private void OnPlayerEnter()
    {
        RefreshResourceAvailability();
    }

    private void Update()
    {
        _stateMachine?.Update();
    }
}
