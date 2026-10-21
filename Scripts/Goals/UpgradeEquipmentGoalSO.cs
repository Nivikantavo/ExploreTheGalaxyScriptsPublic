using UnityEngine;

[CreateAssetMenu(menuName = "Goals/Upgrade Equipment Goal", fileName = "UpgradeEquipmentGoal")]
public class UpgradeEquipmentGoalSO : GoalDefinitionSO
{
    [SerializeField] private EquipmentType _equipmentType;
    [SerializeField] private int _targetLevel = 1;

    public override bool IsCompleted(GoalEvaluationContext context)
    {
        if (context?.Inventory == null)
            return false;

        return context.Inventory.GetEquipmentLevel(_equipmentType) >= _targetLevel;
    }

    public override string BuildDescription(GoalEvaluationContext context)
    {
        int currentLevel = context?.Inventory != null
            ? context.Inventory.GetEquipmentLevel(_equipmentType)
            : 0;

        return $"{Title}: {Mathf.Max(0, currentLevel)}/{Mathf.Max(1, _targetLevel)}";
    }
}
