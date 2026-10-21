using UnityEngine;

[CreateAssetMenu(menuName = "Goals/Collect Resource Goal", fileName = "CollectResourceGoal")]
public class CollectResourceGoalSO : GoalDefinitionSO
{
    [SerializeField] private ResourceType _resourceType;
    [SerializeField] private int _requiredAmount = 1;

    public override bool IsCompleted(GoalEvaluationContext context)
    {
        if (context == null || context.Inventory == null)
            return false;

        return context.Inventory.GetResourceAmount(_resourceType) >= _requiredAmount;
    }

    public override string BuildDescription(GoalEvaluationContext context)
    {
        int currentAmount = 0;
        if (context != null && context.Inventory != null)
            currentAmount = context.Inventory.GetResourceAmount(_resourceType);

        return $"{Title}: {Mathf.Max(0, currentAmount)}/{Mathf.Max(1, _requiredAmount)}";
    }
}
