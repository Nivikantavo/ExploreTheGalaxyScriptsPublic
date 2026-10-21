using UnityEngine;

public class ResourceCreatorProgressState : CreatingState
{
    public ResourceCreatorProgressState(IStateSwitcher stateMachine, CreatingData data, CreatingResourceObject creatingResourceObject)
    {
        _stateMachine = stateMachine;
        _data = data;
        _creatingResourceObject = creatingResourceObject;
    }

    public override void Enter()
    {
        _creatingResourceObject.SetProcessState(TimedProcessState.InProgress);
        _creatingResourceObject.ProgressView.gameObject.SetActive(true);
        _creatingResourceObject.ProgressView.Initialize(_data);
        SyncElapsedFromUtc();
    }

    public override void Exit()
    {
        _data.OnComplited?.Invoke();
        _creatingResourceObject.ProgressView.gameObject.SetActive(false);
    }

    public override void Update()
    {
        SyncElapsedFromUtc();

        if (_data.ElapsedTime >= _data.TimeForCreatingAll)
        {
            _creatingResourceObject.SetProcessState(TimedProcessState.Completed);
            _stateMachine.ChangeState<ResourceCreatorComplitedState>();
        }
    }

    private void SyncElapsedFromUtc()
    {
        if (_data.ProcessStartUtcTicks <= 0)
            return;

        int elapsed = SaveLoadSystem.CalculateElapsedSeconds(
            _data.ProcessStartUtcTicks,
            _data.TimeForCreatingAll);

        if (elapsed != _data.ElapsedTime)
            _data.ElapsedTime = elapsed;
    }
}
