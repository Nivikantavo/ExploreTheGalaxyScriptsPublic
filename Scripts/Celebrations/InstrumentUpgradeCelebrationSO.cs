using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Instrument Upgrade Celebration", fileName = "InstrumentUpgradeCelebration")]
public class InstrumentUpgradeCelebrationSO : CelebrationDefinitionSO
{
    [SerializeField] private InstrumentType _instrumentType;

    public InstrumentType InstrumentType => _instrumentType;

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
