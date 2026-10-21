using UnityEngine;

public interface IStateSwitcher
{
    public void ChangeState<T>() where T : State;
}
