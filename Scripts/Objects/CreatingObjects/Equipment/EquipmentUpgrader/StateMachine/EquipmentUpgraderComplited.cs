public class EquipmentUpgraderComplited : State
{
    private readonly InstrumentUpgraderData _data;
    private readonly EquipmentUpgrader _equipmentUpgrader;

    public EquipmentUpgraderComplited(IStateSwitcher stateMachine, InstrumentUpgraderData data, EquipmentUpgrader equipmentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _equipmentUpgrader = equipmentUpgrader;
    }

    public override void Enter()
    {
        _equipmentUpgrader.SetProcessState(TimedProcessState.Completed);
        _equipmentUpgrader.ComplitedView.gameObject.SetActive(true);
        _equipmentUpgrader.ComplitedView.Initialize(_data);

        _data.OnClaimButtonClicked += ClaimButtonClicked;
    }

    public override void Exit()
    {
        _equipmentUpgrader.ComplitedView.gameObject.SetActive(false);
        _data.OnClaimButtonClicked -= ClaimButtonClicked;
    }

    public override void Update() { }

    private void ClaimButtonClicked()
    {
        _equipmentUpgrader.ApplyEquipment();
    }
}
