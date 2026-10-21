using ECM2;
using UnityEngine;

public class MovementState : State
{
    protected readonly IStateSwitcher StateSwitcher;
    private readonly Character _character;

    public MovementState(IStateSwitcher stateSwitcher, Character character)
    {
        StateSwitcher = stateSwitcher;
        _character = character;
    }

    public override void Enter()
    {
        throw new System.NotImplementedException();
    }

    public override void Exit()
    {
        throw new System.NotImplementedException();
    }

    public override void Update()
    {
        throw new System.NotImplementedException();
    }
}
