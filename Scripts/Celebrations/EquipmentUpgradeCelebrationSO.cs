using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Equipment Upgrade Celebration", fileName = "EquipmentUpgradeCelebration")]
public class EquipmentUpgradeCelebrationSO : CelebrationDefinitionSO
{
    [SerializeField] private EquipmentType _equipmentType;

    public EquipmentType EquipmentType => _equipmentType;

    public override bool IsSatisfied(CelebrationEvaluationContext context) => false;

    public PendingCelebration CreatePending(int level, Sprite icon)
    {
        string localizedTitle = LocalizationSafe.Get(_title);
        if (string.IsNullOrEmpty(localizedTitle))
            localizedTitle = name;

        return new PendingCelebration(
            this,
            Id,
            InstrumentUpgradeCelebrationText.FormatTitle(localizedTitle, level),
            Body,
            icon);
    }
}
