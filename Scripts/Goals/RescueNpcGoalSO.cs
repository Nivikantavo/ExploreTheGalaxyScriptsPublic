using UnityEngine;

[CreateAssetMenu(menuName = "Goals/Rescue Npc Goal", fileName = "RescueNpcGoal")]
public class RescueNpcGoalSO : GoalDefinitionSO
{
    public override bool IsCompleted(GoalEvaluationContext context)
    {
        if (context?.NpcRescueRegistry == null || string.IsNullOrEmpty(TargetId))
            return false;

        return context.NpcRescueRegistry.IsRescued(TargetId);
    }

    public override string BuildDescription(GoalEvaluationContext context)
    {
        return Title;
    }
}
