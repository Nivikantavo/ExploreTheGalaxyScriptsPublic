using System;

public class EquipmentUpgraderIdle : State
{
    private readonly InstrumentUpgraderData _data;
    private readonly EquipmentUpgrader _equipmentUpgrader;

    public EquipmentUpgraderIdle(IStateSwitcher stateMachine, InstrumentUpgraderData data, EquipmentUpgrader equipmentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _equipmentUpgrader = equipmentUpgrader;
    }

    public override void Enter()
    {
        _equipmentUpgrader.SetProcessState(TimedProcessState.Idle);
        _equipmentUpgrader.IdleView.gameObject.SetActive(true);
        _equipmentUpgrader.IdleView.OnUpgradeButtonClicked += UpgradeButtonClicked;
        _equipmentUpgrader.IdleView.Initialize(_data);
        _equipmentUpgrader.RefreshResourceAvailability();
    }

    public override void Exit()
    {
        _equipmentUpgrader.IdleView.OnUpgradeButtonClicked -= UpgradeButtonClicked;
        _equipmentUpgrader.IdleView.gameObject.SetActive(false);
    }

    public override void Update() { }

    private void UpgradeButtonClicked()
    {
        foreach (var price in _data.Prices)
        {
            if (_equipmentUpgrader.GetResourceAmount(price.Key) < price.Value)
                return;
        }

        foreach (var price in _data.Prices)
            _equipmentUpgrader.SpendResource(price.Key, price.Value);

        _data.ProcessStartUtcTicks = DateTime.UtcNow.Ticks;
        _stateMachine.ChangeState<EquipmentUpgraderProcessing>();
    }
}
