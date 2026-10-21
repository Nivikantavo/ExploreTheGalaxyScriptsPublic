using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Lifetime Resource Celebration", fileName = "LifetimeResourceCelebration")]
public class LifetimeResourceCelebrationSO : CelebrationDefinitionSO
{
    [SerializeField] private int _stepAmount = LifetimeResourceMilestones.DefaultStep;

    public int StepAmount => _stepAmount > 0 ? _stepAmount : LifetimeResourceMilestones.DefaultStep;

    public override bool IsSatisfied(CelebrationEvaluationContext context) => false;

    public int HighestReached(int lifetimeAmount) =>
        LifetimeResourceMilestones.HighestReached(lifetimeAmount, StepAmount);

    public string FormatBody(int milestone) =>
        LifetimeResourceMilestones.FormatBody(LocalizationSafe.Get(_body), milestone);

    public PendingCelebration CreatePending(ResourceType resourceType, int milestone, Sprite icon)
    {
        return new PendingCelebration(
            this,
            $"{Id}_{resourceType}_{milestone}",
            Title,
            FormatBody(milestone),
            icon);
    }
}
