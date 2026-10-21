using UnityEngine;

public class DesctopInput : IInput
{
    public Vector2 Move => _input.Movement.Move.ReadValue<Vector2>();
    private PlayerInput _input;

    public DesctopInput(PlayerInput input)
    {
        _input = input;
    }

    public void Enable()
    {
        _input.Enable();
    }

    public void Disable()
    {
        _input.Disable();
    }
}
