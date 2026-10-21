using System;

public class InstrumentUpgraderIdle : UpgraderState
{
    public InstrumentUpgraderIdle(IStateSwitcher stateMachine, InstrumentUpgraderData data, InstrumentUpgrader instrumentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _instrumentUpgrader = instrumentUpgrader;
    }

    public override void Enter()
    {
        _instrumentUpgrader.SetProcessState(TimedProcessState.Idle);
        _instrumentUpgrader.IdleView.gameObject.SetActive(true);
        _instrumentUpgrader.IdleView.OnUpgradeButtonClicked += UpgradeButtonClicked;
        _instrumentUpgrader.IdleView.Initialize(_data);
        _instrumentUpgrader.RefreshResourceAvailability();
    }

    public override void Exit()
    {
        _instrumentUpgrader.IdleView.OnUpgradeButtonClicked -= UpgradeButtonClicked;
        _instrumentUpgrader.IdleView.gameObject.SetActive(false);
    }

    public override void Update(){}

    private void UpgradeButtonClicked()
    {
        foreach(var price in _data.Prices)
        {
            if(_instrumentUpgrader.GetResourceAmount(price.Key) < price.Value)
            {
                return;
            }
        }
        foreach(var price in _data.Prices)
        {
            _instrumentUpgrader.SpendResource(price.Key, price.Value);
        }

        _data.ProcessStartUtcTicks = DateTime.UtcNow.Ticks;
        _stateMachine.ChangeState<InstrumentUpgraderProcessing>();
    }
}
