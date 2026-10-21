using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StateMachine : IStateSwitcher
{
    private State _currentState;
    private List<State> _states;
    public void Initialize(List<State> states)
    {
        _states = states;
        _currentState = _states[0];
        _currentState.Enter();
    }

    public void ChangeState<T>() where T : State
    {
        _currentState.Exit();
        _currentState = _states.FirstOrDefault(state => state is T);
        _currentState.Enter();
    }

    public void Update()
    {
        _currentState.Update();
    }
}
