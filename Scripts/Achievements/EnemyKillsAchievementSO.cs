using UnityEngine;

[CreateAssetMenu(menuName = "Achievements/Enemy Kills Achievement", fileName = "EnemyKillsAchievement")]
public class EnemyKillsAchievementSO : AchievementDefinitionSO
{
    [SerializeField] private int _killThreshold = 100;

    public int KillThreshold => _killThreshold;

    public override string Body => $"{LocalizationSafe.Get(_body)} {_killThreshold}";

    public override bool IsSatisfied(AchievementEvaluationContext context)
    {
        if (context == null || _killThreshold <= 0)
            return false;

        return context.EnemiesKilled >= _killThreshold;
    }
}
