using UnityEngine;

public class InstrumentUpgraderProcessing : UpgraderState
{
    public InstrumentUpgraderProcessing(IStateSwitcher stateMachine, InstrumentUpgraderData data, InstrumentUpgrader instrumentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _instrumentUpgrader = instrumentUpgrader;
    }

    public override void Enter()
    {
        _instrumentUpgrader.SetProcessState(TimedProcessState.InProgress);
        _instrumentUpgrader.ProgressView.gameObject.SetActive(true);
        _instrumentUpgrader.ProgressView.Initialize(_data);
        SyncElapsedFromUtc();
    }

    public override void Exit()
    {
        _data.OnComplited?.Invoke();
        _instrumentUpgrader.ProgressView.gameObject.SetActive(false);
    }

    public override void Update()
    {
        SyncElapsedFromUtc();

        if (_data.ElapsedTime >= _data.UpgradeTime)
        {
            _instrumentUpgrader.SetProcessState(TimedProcessState.Completed);
            _stateMachine.ChangeState<InstrumentUpgraderComplited>();
        }
    }

    private void SyncElapsedFromUtc()
    {
        if (_data.ProcessStartUtcTicks <= 0)
            return;

        int elapsed = SaveLoadSystem.CalculateElapsedSeconds(
            _data.ProcessStartUtcTicks,
            _data.UpgradeTime);

        if (elapsed != _data.ElapsedTime)
            _data.ElapsedTime = elapsed;
    }
}
