using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class InstrumentUpgrader : MonoBehaviour
{
    public InstrumentType InstrumentType => _instrumentType;
    public InstrumentUpgraderData Data {get; private set;}
    public TimedProcessState ProcessState { get; private set; } = TimedProcessState.Idle;
    public GameObject CurrentUpgradePrefab => _instrumentConfig != null ? _instrumentConfig.InstrumentObj : null;
    public InstrumentUpgraderIdleView IdleView => _idleView;
    public InstrumentUpgraderProgressView ProgressView => _progressView;
    public InstrumentUpgraderComplitedView ComplitedView => _complitedView;
    public InstrumentUpgraderMaxLevelView MaxLevelView => _maxLevelView;

    [SerializeField] private InstrumentUpgraderIdleView _idleView;
    [SerializeField] private InstrumentUpgraderProgressView _progressView;
    [SerializeField] private InstrumentUpgraderComplitedView _complitedView;
    [SerializeField] private InstrumentUpgraderMaxLevelView _maxLevelView;
    [SerializeField] private InstrumentType _instrumentType;
    [Tooltip("Highest instrument level this station can upgrade to. 0 uses the highest configured level.")]
    [SerializeField, Min(0)] private int _maxUpgradeLevel;
    [SerializeField] private ApproachUIShower _approachUIShower;

    private AllInstruments _allInstruments;

    private Inventory _inventory;
    private InstrumentConfig _instrumentConfig;
    private StateMachine _stateMachine;
    private bool _initialized;


    [Inject]
    private void Construct(AllInstruments allInstruments, Inventory inventory)
    {
        _allInstruments = allInstruments;
        _inventory = inventory;
    }

    private void Start() 
    {
        Initialize();
    }

    private void OnDestroy() 
    {
        _approachUIShower.OnPlayerEnter -= OnPlayerEnter;
    }

    public void Initialize()
    {
        if (_initialized)
        {
            // Start can run before the WebGL save arrives and cache the default level-1 offer.
            SyncOfferFromInventory();
            return;
        }

        _initialized = true;
        SetCurrentSaleInstrument();
        
        _stateMachine = new StateMachine();
        var idle = new InstrumentUpgraderIdle(_stateMachine as IStateSwitcher, Data, this);
        var processing = new InstrumentUpgraderProcessing(_stateMachine as IStateSwitcher, Data, this);
        var completed = new InstrumentUpgraderComplited(_stateMachine as IStateSwitcher, Data, this);
        var maxLevel = new InstrumentUpgraderMaxLevel(_stateMachine as IStateSwitcher, Data, this);

        _stateMachine.Initialize(Data.IsMaxLevel
            ? new List<State> { maxLevel, idle, processing, completed }
            : new List<State> { idle, processing, completed, maxLevel });

        _approachUIShower.OnPlayerEnter += OnPlayerEnter;
    }

    public void UpgradeInstrument()
    {
        if (_instrumentConfig == null || IsAboveUpgradeCap(_instrumentConfig.Level))
            return;

        _inventory.UpgradeInstrument(_instrumentConfig);
        RefreshFromInventory();
    }

    public void RefreshFromInventory()
    {
        SetCurrentSaleInstrument();
        ProcessState = TimedProcessState.Idle;

        if (_stateMachine == null)
            return;

        if (Data.IsMaxLevel)
            _stateMachine.ChangeState<InstrumentUpgraderMaxLevel>();
        else
            _stateMachine.ChangeState<InstrumentUpgraderIdle>();
    }

    public InstrumentUpgradeSaveData CreateSaveData()
    {
        if (ProcessState == TimedProcessState.Idle || Data == null || Data.IsMaxLevel)
            return null;

        return new InstrumentUpgradeSaveData
        {
            InstrumentType = InstrumentType,
            State = ProcessState,
            TargetLevel = Data.NextLevel,
            TotalDuration = Data.UpgradeTime,
            StartUtcTicks = Data.ProcessStartUtcTicks
        };
    }

    public void ApplySave(ISaveLoader saveLoader)
    {
        var save = saveLoader.GetInstrumentUpgradeSave(InstrumentType);
        if (save == null)
            return;

        if (Data == null || _stateMachine == null)
        {
            SaveSystemTestLog.Warn(
                $"InstrumentUpgrader.ApplySave skipped type={InstrumentType} " +
                $"dataNull={Data == null} stateMachineNull={_stateMachine == null}");
            return;
        }

        if (Data.IsMaxLevel)
            return;

        if (save.TargetLevel > 0 && save.TargetLevel != Data.NextLevel)
        {
            SaveSystemTestLog.Warn(
                $"InstrumentUpgrader.ApplySave skipped stale type={InstrumentType} " +
                $"savedTarget={save.TargetLevel} next={Data.NextLevel}");
            return;
        }

        Data.ProcessStartUtcTicks = save.StartUtcTicks;
        int elapsed = SaveLoadSystem.CalculateElapsedSeconds(save.StartUtcTicks, save.TotalDuration);
        Data.ElapsedTime = elapsed;

        if (save.State == TimedProcessState.Completed || elapsed >= save.TotalDuration)
        {
            ProcessState = TimedProcessState.Completed;
            _stateMachine.ChangeState<InstrumentUpgraderComplited>();
            return;
        }

        ProcessState = TimedProcessState.InProgress;
        _stateMachine.ChangeState<InstrumentUpgraderProcessing>();
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

    private void SyncOfferFromInventory()
    {
        if (_inventory == null || !TryGetOwnedLevel(out int ownedLevel))
            return;

        int nextLevel = ownedLevel + 1;
        bool isMaxLevel = IsAboveUpgradeCap(nextLevel) || FindInstrumentConfig(nextLevel) == null;
        bool offerMatches = Data != null
            && Data.IsMaxLevel == isMaxLevel
            && (isMaxLevel ? Data.CurrentLevel == ownedLevel : Data.NextLevel == nextLevel);

        if (offerMatches)
            return;

        SetCurrentSaleInstrument();
        ProcessState = TimedProcessState.Idle;

        if (_stateMachine == null || Data == null)
            return;

        if (Data.IsMaxLevel)
            _stateMachine.ChangeState<InstrumentUpgraderMaxLevel>();
        else
            _stateMachine.ChangeState<InstrumentUpgraderIdle>();
    }

    private bool TryGetOwnedLevel(out int level)
    {
        Instrument currentInstrument = _inventory.Instruments.FirstOrDefault(instrument => instrument.Type == InstrumentType);
        if (currentInstrument == null)
        {
            level = 0;
            return false;
        }

        level = currentInstrument.Level;
        return true;
    }

    private InstrumentConfig FindInstrumentConfig(int level)
    {
        if (_allInstruments == null)
            return null;

        return _allInstruments.Instruments.FirstOrDefault(instrument =>
            instrument.Type == InstrumentType && instrument.Level == level);
    }

    private void SetCurrentSaleInstrument()
    {
        if (!TryGetOwnedLevel(out int currentLevel))
            return;

        int nextLevel = currentLevel + 1;

        _instrumentConfig = IsAboveUpgradeCap(nextLevel)
            ? null
            : FindInstrumentConfig(nextLevel);
        
        if(Data == null)
            Data = new InstrumentUpgraderData();

        if (_instrumentConfig == null)
        {
            var maxLevelConfig = _allInstruments.Instruments.FirstOrDefault(
                instrument => instrument.Type == InstrumentType && instrument.Level == currentLevel);
            Data.InitializeMaxLevel(currentLevel, maxLevelConfig != null ? maxLevelConfig.Icon : null);
            return;
        }
            
        Data.Initialize(
            currentLevel, 
            nextLevel, 
            _instrumentConfig.UpgradeTime,
            _instrumentConfig.Price, 
            _instrumentConfig.Icon);
    }

    public void RefreshResourceAvailability()
    {
        if (Data.IsMaxLevel)
            return;

        Dictionary<ResourceType,bool> resourceAvailable = new Dictionary<ResourceType, bool>();
        foreach(var price in Data.Prices)
        {
            resourceAvailable.Add(price.Key, GetResourceAmount(price.Key) >= price.Value);
        }
        Data.PlayerEnter?.Invoke(resourceAvailable);
    }

    private bool IsAboveUpgradeCap(int level) =>
        _maxUpgradeLevel > 0 && level > _maxUpgradeLevel;

    private void OnPlayerEnter()
    {
        RefreshResourceAvailability();
    }

    private void Update()
    {
        _stateMachine?.Update();
    }
}
