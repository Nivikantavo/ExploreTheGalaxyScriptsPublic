using UnityEngine;
using ECM2.Examples.ThirdPerson;
using Zenject;

public class PlayerController : ThirdPersonController
{
    public IInput Input { get; private set; }

    [Inject]
    private void Construct(IInput input)
    {
        Input = input;
        Debug.Log(Input);
    }

    private void OnEnable() 
    {
        Input.Enable();
    }

    protected override void Update() 
    {
        Vector2 inputMove = Input.Move;
        
        Vector3 movementDirection = Vector3.zero;

        movementDirection += Vector3.right * inputMove.x;
        movementDirection += Vector3.forward * inputMove.y;

        _character.SetMovementDirection(movementDirection);
    }
}
