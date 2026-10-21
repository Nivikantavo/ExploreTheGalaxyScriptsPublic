using UnityEngine;

[CreateAssetMenu(menuName = "Achievements/Lifetime Resource Achievement", fileName = "LifetimeResourceAchievement")]
public class LifetimeResourceAchievementSO : AchievementDefinitionSO
{
    [SerializeField] private ResourceType _resourceType;
    [SerializeField] private int _requiredAmount = 100000;

    public ResourceType ResourceType => _resourceType;
    public int RequiredAmount => _requiredAmount;

    public override string Body => $"{_requiredAmount} {LocalizationSafe.Get(_body)}";

    public override bool IsSatisfied(AchievementEvaluationContext context)
    {
        if (context == null || _requiredAmount <= 0)
            return false;

        return context.GetLifetimeAmount(_resourceType) >= _requiredAmount;
    }
}
