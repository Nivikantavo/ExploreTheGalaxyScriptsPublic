public class InstrumentUpgraderComplited : UpgraderState
{
    public InstrumentUpgraderComplited(IStateSwitcher stateMachine, InstrumentUpgraderData data, InstrumentUpgrader instrumentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _instrumentUpgrader = instrumentUpgrader;
    }

    public override void Enter()
    {
        _instrumentUpgrader.SetProcessState(TimedProcessState.Completed);
        _instrumentUpgrader.ComplitedView.gameObject.SetActive(true);
        _instrumentUpgrader.ComplitedView.Initialize(_data);

        _data.OnClaimButtonClicked += ClaimButtonClicked;
    }

    public override void Exit()
    {
        _instrumentUpgrader.ComplitedView.gameObject.SetActive(false);
        _data.OnClaimButtonClicked -= ClaimButtonClicked;
    }

    public override void Update(){}

    private void ClaimButtonClicked()
    {
        _instrumentUpgrader.UpgradeInstrument();
    }
}
