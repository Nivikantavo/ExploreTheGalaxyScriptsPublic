using UnityEngine;

public abstract class UpgraderState : State
{
    protected InstrumentUpgraderData _data;
    protected InstrumentUpgrader _instrumentUpgrader;
    public override void Enter(){}
    public override void Exit(){}
    public override void Update(){}
}
