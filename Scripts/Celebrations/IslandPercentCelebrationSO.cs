using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Island Percent Celebration", fileName = "IslandPercentCelebration")]
public class IslandPercentCelebrationSO : CelebrationDefinitionSO
{
    public override bool IsSatisfied(CelebrationEvaluationContext context) => false;

    public string FormatBody(int percent) =>
        IslandPercentCelebrationRules.FormatBody(LocalizationSafe.Get(_body), percent);

    public PendingCelebration CreatePending(int percent)
    {
        return new PendingCelebration(this, Id, Title, FormatBody(percent), Icon);
    }
}
