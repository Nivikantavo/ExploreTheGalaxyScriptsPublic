using UnityEngine;

public class Mobileinput : IInput
{
    public Vector2 Move => _joystick.Direction;
    private Joystick _joystick;

    public Mobileinput(Joystick joystick)
    {
        _joystick = joystick;
    }

    public void Enable()
    {
        _joystick.gameObject.SetActive(true);

        if (_joystick.GetComponent<JoystickWorldUIPassthrough>() == null)
            _joystick.gameObject.AddComponent<JoystickWorldUIPassthrough>();
    }
    public void Disable()
    {
        _joystick.gameObject.SetActive(false);
    }
}
