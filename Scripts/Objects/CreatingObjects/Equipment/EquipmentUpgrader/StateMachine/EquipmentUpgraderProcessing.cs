public class EquipmentUpgraderProcessing : State
{
    private readonly InstrumentUpgraderData _data;
    private readonly EquipmentUpgrader _equipmentUpgrader;

    public EquipmentUpgraderProcessing(IStateSwitcher stateMachine, InstrumentUpgraderData data, EquipmentUpgrader equipmentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _equipmentUpgrader = equipmentUpgrader;
    }

    public override void Enter()
    {
        _equipmentUpgrader.SetProcessState(TimedProcessState.InProgress);
        _equipmentUpgrader.ProgressView.gameObject.SetActive(true);
        _equipmentUpgrader.ProgressView.Initialize(_data);
        SyncElapsedFromUtc();
    }

    public override void Exit()
    {
        _data.OnComplited?.Invoke();
        _equipmentUpgrader.ProgressView.gameObject.SetActive(false);
    }

    public override void Update()
    {
        SyncElapsedFromUtc();

        if (_data.ElapsedTime >= _data.UpgradeTime)
        {
            _equipmentUpgrader.SetProcessState(TimedProcessState.Completed);
            _stateMachine.ChangeState<EquipmentUpgraderComplited>();
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
