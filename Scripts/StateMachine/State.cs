using UnityEngine;

public abstract class State
{
    protected IStateSwitcher _stateMachine;
    
    
    public abstract void Enter();
    public abstract void Exit();
    public abstract void Update();
}
