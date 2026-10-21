using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Enemy Kills Celebration", fileName = "EnemyKillsCelebration")]
public class EnemyKillsCelebrationSO : CelebrationDefinitionSO
{
    [SerializeField] private int _killThreshold = 10;

    public int KillThreshold => _killThreshold;

    public override string Body => $"{LocalizationSafe.Get(_body)} {_killThreshold}";

    public PendingCelebration CreatePending()
    {
        return new PendingCelebration(this, Id, Title, Body, Icon);
    }

    public override bool IsSatisfied(CelebrationEvaluationContext context)
    {
        if (context == null || _killThreshold <= 0)
            return false;

        return context.EnemiesKilled >= _killThreshold;
    }
}
