using UnityEngine;

public interface IInput
{
    public Vector2 Move { get; }
    public void Enable();
    public void Disable();
}
