using System.Linq;
using UnityEngine;

public enum ExploreIslandCompletionType
{
    MainPathReached = 0,
    HexUnlocked = 1,
    FullyOpened = 2
}

[CreateAssetMenu(menuName = "Goals/Explore Island Goal", fileName = "ExploreIslandGoal")]
public class ExploreIslandGoalSO : GoalDefinitionSO
{
    [SerializeField] private int _islandIndex;
    [SerializeField] private ExploreIslandCompletionType _completionType = ExploreIslandCompletionType.MainPathReached;
    [SerializeField] private int _targetHexQ;
    [SerializeField] private int _targetHexR;
    [SerializeField] private string _requiredSceneName;

    public override string RequiredSceneName => _requiredSceneName;

    public override bool IsCompleted(GoalEvaluationContext context)
    {
        IslandProgressHandler island = GetIsland(context);
        if (island == null)
            return false;

        return _completionType switch
        {
            ExploreIslandCompletionType.MainPathReached =>
                island.IsMainPathReached || island.IsFullyOpened,
            ExploreIslandCompletionType.HexUnlocked => island.IsHexUnlocked(_targetHexQ, _targetHexR),
            ExploreIslandCompletionType.FullyOpened => island.IsFullyOpened,
            _ => false
        };
    }

    public override string BuildDescription(GoalEvaluationContext context)
    {
        if (_completionType == ExploreIslandCompletionType.HexUnlocked)
            return $"{Title}";

        return Title;
    }

    private IslandProgressHandler GetIsland(GoalEvaluationContext context)
    {
        if (context?.IslandRegistry == null)
            return null;

        return context.IslandRegistry.Islands
            .FirstOrDefault(item => GoalIslandMatch.Matches(
                _islandIndex,
                _requiredSceneName,
                item.IslandIndex,
                item.gameObject.scene.name));
    }
}
