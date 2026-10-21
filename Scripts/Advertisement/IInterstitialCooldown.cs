public interface IInterstitialCooldown
{
    bool CanShowGatedCelebration();

    void MarkGatedCelebrationQueued();
}
