public class InstrumentUpgraderMaxLevel : UpgraderState
{
    public InstrumentUpgraderMaxLevel(IStateSwitcher stateMachine, InstrumentUpgraderData data, InstrumentUpgrader instrumentUpgrader)
    {
        _stateMachine = stateMachine;
        _data = data;
        _instrumentUpgrader = instrumentUpgrader;
    }

    public override void Enter()
    {
        _instrumentUpgrader.SetProcessState(TimedProcessState.Idle);
        _instrumentUpgrader.IdleView.gameObject.SetActive(false);
        _instrumentUpgrader.ProgressView.gameObject.SetActive(false);
        _instrumentUpgrader.ComplitedView.gameObject.SetActive(false);
        _instrumentUpgrader.MaxLevelView.gameObject.SetActive(true);
        _instrumentUpgrader.MaxLevelView.Initialize(_data);
    }

    public override void Exit()
    {
        _instrumentUpgrader.MaxLevelView.gameObject.SetActive(false);
    }

    public override void Update(){}
}
