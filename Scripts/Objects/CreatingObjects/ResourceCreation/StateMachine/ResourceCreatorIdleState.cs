using System;

public class ResourceCreatorIdleState : CreatingState
{
    public ResourceCreatorIdleState(IStateSwitcher stateMachine, CreatingData data, CreatingResourceObject creatingResourceObject)
    {
        _stateMachine = stateMachine;
        _data = data;
        _creatingResourceObject = creatingResourceObject;
    }

    public override void Enter()
    {
        _creatingResourceObject.SetProcessState(TimedProcessState.Idle);
        _creatingResourceObject.IdleView.gameObject.SetActive(true);
        _creatingResourceObject.IdleView.OnUpgradeButtonClicked += UpgradeButtonClicked;
        _creatingResourceObject.IdleView.Initialize(_data);
        _creatingResourceObject.RefreshResourceAvailability();
    }

    public override void Exit()
    {
        _creatingResourceObject.IdleView.OnUpgradeButtonClicked -= UpgradeButtonClicked;
        _creatingResourceObject.IdleView.gameObject.SetActive(false);
    }

    public override void Update(){}

    private void UpgradeButtonClicked()
    {
        if(_creatingResourceObject.GetResourceAmount(_data.RequestedResourceType) < _data.RequestedResourceCount || _data.RequestedResourceCount == 0)
        {
            return;
        }
        
        _creatingResourceObject.SpendResource(_data.RequestedResourceType, _data.RequestedResourceCount);
        _data.ProcessStartUtcTicks = DateTime.UtcNow.Ticks;
        
        _stateMachine.ChangeState<ResourceCreatorProgressState>();
    }
}
