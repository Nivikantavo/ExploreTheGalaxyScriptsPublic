using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Opener Opened Celebration", fileName = "OpenerOpenedCelebration")]
public class OpenerOpenedCelebrationSO : CelebrationDefinitionSO
{
    [SerializeField] private string _goalOpenerId;

    public string GoalOpenerId => _goalOpenerId;

    public PendingCelebration CreatePending()
    {
        return new PendingCelebration(this, Id, Title, Body, Icon);
    }

    public override bool IsSatisfied(CelebrationEvaluationContext context)
    {
        if (context?.GoalOpenerRegistry == null || string.IsNullOrEmpty(_goalOpenerId))
            return false;

        return context.GoalOpenerRegistry.IsFilled(_goalOpenerId);
    }
}
