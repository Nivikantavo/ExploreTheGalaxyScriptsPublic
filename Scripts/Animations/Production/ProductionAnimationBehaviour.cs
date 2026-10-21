using UnityEngine;

public abstract class ProductionAnimationBehaviour : MonoBehaviour
{
    private bool _isPlaying;

    protected bool IsPlaying => _isPlaying;

    private void Awake()
    {
        OnAnimationAwake();
    }

    private void Update()
    {
        bool shouldPlay = ProductionWorkGate.IsInProgress(this);
        if (shouldPlay == _isPlaying)
            return;

        _isPlaying = shouldPlay;
        if (shouldPlay)
            OnWorkStarted();
        else
            OnWorkStopped();
    }

    private void OnDisable()
    {
        if (!_isPlaying)
            return;

        _isPlaying = false;
        OnWorkStopped();
    }

    protected virtual void OnAnimationAwake()
    {
    }

    protected abstract void OnWorkStarted();

    protected abstract void OnWorkStopped();
}
