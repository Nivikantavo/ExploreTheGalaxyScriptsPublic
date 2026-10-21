using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zenject;

public class CreatingResourceObject : MonoBehaviour
{
    public ResourceType ResourseType => _resourseConfig.ResultResource;
    public CreatingData Data {get; private set;}
    public TimedProcessState ProcessState { get; private set; } = TimedProcessState.Idle;
    public CreatingUIIdleView IdleView => _idleView;
    public CreatingUIProgressView ProgressView => _progressView;
    public CreatingUIComplitedView ComplitedView => _complitedView;

    [SerializeField] private string _saveId;
    [SerializeField] private CreatingUIIdleView _idleView;
    [SerializeField] private CreatingUIProgressView _progressView;
    [SerializeField] private CreatingUIComplitedView _complitedView;
    [SerializeField] private CreatingResourceConfig _resourseConfig;
    [SerializeField] private ApproachUIShower _approachUIShower;

    private Inventory _inventory;
    private StateMachine _stateMachine;
    private bool _initialized;


    [Inject]
    private void Construct(Inventory inventory)
    {
        _inventory = inventory;
    }

    private void Start() 
    {
        Initialize();
    }

    private void OnDestroy() 
    {
        _approachUIShower.OnPlayerEnter -= SetMaxRequestedCount;
    }

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        SetData();
        
        _stateMachine = new StateMachine();
        _stateMachine.Initialize(new List<State> 
        { 
            new ResourceCreatorIdleState(_stateMachine as IStateSwitcher, Data, this), 
            new ResourceCreatorProgressState(_stateMachine as IStateSwitcher, Data, this), 
            new ResourceCreatorComplitedState(_stateMachine as IStateSwitcher, Data, this) 
        });

        _approachUIShower.OnPlayerEnter += SetMaxRequestedCount;
    }

    public void GiveResultResource()
    {
        _inventory.AddResource(ResourseType, Data.ResourceCount);
        SetProcessState(TimedProcessState.Idle);
        SetData();
        _stateMachine.ChangeState<ResourceCreatorIdleState>();
    }

    public ProductionSaveData CreateSaveData()
    {
        if (ProcessState == TimedProcessState.Idle || Data == null)
            return null;

        return new ProductionSaveData
        {
            SaveId = GetSaveId(),
            State = ProcessState,
            ResourceCount = Data.ResourceCount,
            TotalDuration = Data.TimeForCreatingAll,
            StartUtcTicks = Data.ProcessStartUtcTicks
        };
    }

    public void ApplySave(ISaveLoader saveLoader)
    {
        var save = saveLoader.GetProductionSave(GetSaveId());
        if (save == null)
            return;

        if (Data == null || _stateMachine == null)
        {
            SaveSystemTestLog.Warn(
                $"CreatingResourceObject.ApplySave skipped saveId={GetSaveId()} " +
                $"dataNull={Data == null} stateMachineNull={_stateMachine == null}");
            return;
        }

        Data.IsCountLocked = false;
        Data.ResourceCount = save.ResourceCount;
        Data.ProcessStartUtcTicks = save.StartUtcTicks;
        int elapsed = SaveLoadSystem.CalculateElapsedSeconds(save.StartUtcTicks, save.TotalDuration);
        Data.ElapsedTime = elapsed;

        if (save.State == TimedProcessState.Completed || elapsed >= save.TotalDuration)
        {
            _stateMachine.ChangeState<ResourceCreatorComplitedState>();
            return;
        }

        _stateMachine.ChangeState<ResourceCreatorProgressState>();
    }

    public void SetProcessState(TimedProcessState processState)
    {
        ProcessState = processState;
        if (Data != null)
            Data.IsCountLocked = processState != TimedProcessState.Idle;
    }

    public string GetSaveId() =>
        string.IsNullOrEmpty(_saveId) ? _resourseConfig.ResultResource.ToString() : _saveId;

    public int GetResourceAmount(ResourceType resourseType)
    {
        return _inventory.GetResourceAmount(resourseType);
    }

    public void SpendResource(ResourceType resourseType, int amount)
    {
        _inventory.RemoveResource(resourseType, amount);
    }

    private void SetData()
    {
        if(Data == null)
            Data = new CreatingData();
        
        int resultCount = _inventory.GetResourceAmount(_resourseConfig.RequestedResource) / _resourseConfig.RequestedForResource / 2;

        Data.Initialize(
            requestedForResult: _resourseConfig.RequestedForResource, 
            creatingTime: _resourseConfig.TimeToCreatingOne, 
            resourceIcon: _resourseConfig.ResultResourceIcon,
            resultResourceType: _resourseConfig.ResultResource,
            requestedResourceType: _resourseConfig.RequestedResource,
            resourceCount: resultCount);

        SetMaxRequestedCount();
    }

    public void RefreshResourceAvailability()
    {
        SetMaxRequestedCount();
    }

    private void SetMaxRequestedCount()
    {
        if (ProcessState != TimedProcessState.Idle || Data == null)
            return;

        Data.MaxRequestedCount = _inventory.GetResourceAmount(Data.RequestedResourceType);
    }

    private void Update()
    {
        _stateMachine?.Update();
    }
}
