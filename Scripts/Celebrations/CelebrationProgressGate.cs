public sealed class CelebrationProgressGate
{
    public const float CooldownSeconds = 60f;

    private bool _hasInterstitialFinished;
    private float _lastInterstitialUnscaledTime;
    private bool _gatedPopupPending;

    public bool CanShowGated(float unscaledTime)
    {
        if (_gatedPopupPending)
            return false;

        if (!_hasInterstitialFinished)
            return true;

        return unscaledTime - _lastInterstitialUnscaledTime >= CooldownSeconds;
    }

    public void MarkGatedPopupQueued() => _gatedPopupPending = true;

    public void MarkInterstitialFinished(float unscaledTime)
    {
        _hasInterstitialFinished = true;
        _lastInterstitialUnscaledTime = unscaledTime;
        _gatedPopupPending = false;
    }
}
