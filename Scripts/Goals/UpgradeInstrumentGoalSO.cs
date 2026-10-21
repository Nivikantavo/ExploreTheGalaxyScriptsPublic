using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Goals/Upgrade Instrument Goal", fileName = "UpgradeInstrumentGoal")]
public class UpgradeInstrumentGoalSO : GoalDefinitionSO
{
    [SerializeField] private InstrumentType _instrumentType;
    [SerializeField] private int _targetLevel = 2;

    public override bool IsCompleted(GoalEvaluationContext context)
    {
        if (context?.Inventory == null)
            return false;

        Instrument instrument = context.Inventory.Instruments
            .FirstOrDefault(item => item.Type == _instrumentType);

        return instrument != null && instrument.Level >= _targetLevel;
    }

    public override string BuildDescription(GoalEvaluationContext context)
    {
        int currentLevel = 0;
        if (context?.Inventory != null)
        {
            Instrument instrument = context.Inventory.Instruments
                .FirstOrDefault(item => item.Type == _instrumentType);
            currentLevel = instrument != null ? instrument.Level : 0;
        }

        return $"{Title}: {Mathf.Max(0, currentLevel)}/{Mathf.Max(1, _targetLevel)}";
    }
}
