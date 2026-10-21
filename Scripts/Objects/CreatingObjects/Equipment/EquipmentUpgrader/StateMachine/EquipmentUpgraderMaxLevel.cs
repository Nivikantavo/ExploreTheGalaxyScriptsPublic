public class EquipmentUpgraderMaxLevel : State
{
    private readonly InstrumentUpgraderData _data;
    private readonly EquipmentUpgrader _equipmentUpgrader;

    public EquipmentUpgraderMaxLevel(IStateSwitcher stateMachine, InstrumentUpgraderData data, EquipmentUpgrader equipmentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _equipmentUpgrader = equipmentUpgrader;
    }

    public override void Enter()
    {
        _equipmentUpgrader.SetProcessState(TimedProcessState.Idle);
        _equipmentUpgrader.IdleView.gameObject.SetActive(false);
        _equipmentUpgrader.ProgressView.gameObject.SetActive(false);
        _equipmentUpgrader.ComplitedView.gameObject.SetActive(false);

        if (_equipmentUpgrader.MaxLevelView != null)
        {
            _equipmentUpgrader.MaxLevelView.gameObject.SetActive(true);
            _equipmentUpgrader.MaxLevelView.Initialize(_data);
        }
    }

    public override void Exit()
    {
        if (_equipmentUpgrader.MaxLevelView != null)
            _equipmentUpgrader.MaxLevelView.gameObject.SetActive(false);
    }

    public override void Update() { }
}
