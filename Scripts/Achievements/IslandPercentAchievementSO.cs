using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Achievements/Island Percent Achievement", fileName = "IslandPercentAchievement")]
public class IslandPercentAchievementSO : AchievementDefinitionSO
{
    [SerializeField] private int _islandIndex;
    [SerializeField, Range(1, 100)] private int _percentThreshold = 100;

    public override string Body => $"{LocalizationSafe.Get(_body)} {_percentThreshold}%";

    public override bool IsSatisfied(AchievementEvaluationContext context)
    {
        if (context?.IslandRegistry == null)
            return false;

        IslandProgressHandler island = context.IslandRegistry.Islands
            .FirstOrDefault(item => item.IslandIndex == _islandIndex);

        return island != null && island.GetOpenPercent() >= _percentThreshold;
    }
}
