using UnityEngine;

public class ResourceCreatorComplitedState : CreatingState
{
    public ResourceCreatorComplitedState(IStateSwitcher stateMachine, CreatingData data, CreatingResourceObject creatingResourceObject)
    {
        _stateMachine = stateMachine;
        _data = data;
        _creatingResourceObject = creatingResourceObject;
    }

    public override void Enter()
    {
        _creatingResourceObject.SetProcessState(TimedProcessState.Completed);
        _creatingResourceObject.ComplitedView.gameObject.SetActive(true);
        _creatingResourceObject.ComplitedView.Initialize(_data);

        _data.OnClaimButtonClicked += ClaimButtonClicked;
    }

    public override void Exit()
    {
        _creatingResourceObject.ComplitedView.gameObject.SetActive(false);
        _data.OnClaimButtonClicked -= ClaimButtonClicked;
    }

    public override void Update(){}

    private void ClaimButtonClicked()
    {
        _creatingResourceObject.GiveResultResource();
    }
}
