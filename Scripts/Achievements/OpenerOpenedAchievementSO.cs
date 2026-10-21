using UnityEngine;

[CreateAssetMenu(menuName = "Achievements/Opener Opened Achievement", fileName = "OpenerOpenedAchievement")]
public class OpenerOpenedAchievementSO : AchievementDefinitionSO
{
    [SerializeField] private string _goalOpenerId;

    public override bool IsSatisfied(AchievementEvaluationContext context)
    {
        if (context?.GoalOpenerRegistry == null || string.IsNullOrEmpty(_goalOpenerId))
            return false;

        return context.GoalOpenerRegistry.IsFilled(_goalOpenerId);
    }
}
