using UnityEngine;

[CreateAssetMenu(menuName = "Goals/Fill Opening Hexagon Goal", fileName = "FillOpeningHexagonGoal")]
public class FillOpeningHexagonGoalSO : GoalDefinitionSO
{
    public override bool IsCompleted(GoalEvaluationContext context)
    {
        if (context?.GoalOpenerRegistry == null || string.IsNullOrEmpty(TargetId))
            return false;

        return context.GoalOpenerRegistry.IsFilled(TargetId);
    }

    public override string BuildDescription(GoalEvaluationContext context)
    {
        ResourcePaymentOpener opener = context?.GoalOpenerRegistry?.Find(TargetId);

        if (opener == null)
            return Title;

        int totalRequired = opener.GetTotalRequiredAmount();
        if (totalRequired <= 0)
            return Title;

        int totalPaid = opener.GetTotalPaidAmount();
        return $"{Title}: {totalPaid}/{totalRequired}";
    }
}
