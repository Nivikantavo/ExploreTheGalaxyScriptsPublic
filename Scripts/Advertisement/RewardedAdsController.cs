using System;
using Playgama;
using Playgama.Modules.Advertisement;
using UnityEngine;
using Zenject;

public class RewardedAdsController : MonoBehaviour
{
    private bool _subscribedToAdvertisement;
    private bool _isGameplayPausedByAd;
    private Action _pendingRewardCallback;
    private bool _rewardGrantedForCurrentAd;
    private IAudioService _audioService;

    [Inject]
    private void Construct(IAudioService audioService)
    {
        _audioService = audioService;
    }

    private void OnEnable()
    {
        TrySubscribeAdvertisement();
    }

    private void Start()
    {
        // Bridge modules may finish wiring after Awake.
        TrySubscribeAdvertisement();
    }

    private void OnDisable()
    {
        UnsubscribeAdvertisement();
        ClearPendingReward();
        ResumeGameplay();
    }

    public bool TryShowRewarded(string placement, Action onRewarded)
    {
        if (Bridge.instance == null || Bridge.advertisement == null)
            return false;

        if (!Bridge.advertisement.isRewardedSupported)
            return false;

        if (_pendingRewardCallback != null)
            return false;

        _pendingRewardCallback = onRewarded;
        _rewardGrantedForCurrentAd = false;
        Bridge.advertisement.ShowRewarded(placement);
        return true;
    }

    private void TrySubscribeAdvertisement()
    {
        if (_subscribedToAdvertisement || Bridge.advertisement == null)
            return;

        Bridge.advertisement.rewardedStateChanged += OnRewardedStateChanged;
        _subscribedToAdvertisement = true;
    }

    private void UnsubscribeAdvertisement()
    {
        if (!_subscribedToAdvertisement || Bridge.advertisement == null)
            return;

        Bridge.advertisement.rewardedStateChanged -= OnRewardedStateChanged;
        _subscribedToAdvertisement = false;
    }

    private void OnRewardedStateChanged(RewardedState state)
    {
        switch (state)
        {
            case RewardedState.Loading:
            case RewardedState.Opened:
                PauseGameplay();
                break;

            case RewardedState.Rewarded:
                if (_rewardGrantedForCurrentAd)
                    break;

                _rewardGrantedForCurrentAd = true;
                _pendingRewardCallback?.Invoke();
                break;

            case RewardedState.Closed:
            case RewardedState.Failed:
                ResumeGameplay();
                ClearPendingReward();
                break;
        }
    }

    private void ClearPendingReward()
    {
        _pendingRewardCallback = null;
        _rewardGrantedForCurrentAd = false;
    }

    private void PauseGameplay()
    {
        if (_isGameplayPausedByAd)
            return;

        GameplayPause.Push();
        if (_audioService != null)
            _audioService.SetPausedByAd(true);
        else
            AudioListener.pause = true;
        _isGameplayPausedByAd = true;
    }

    private void ResumeGameplay()
    {
        if (!_isGameplayPausedByAd)
            return;

        GameplayPause.Pop();
        if (_audioService != null)
            _audioService.SetPausedByAd(false);
        else
            AudioListener.pause = false;
        _isGameplayPausedByAd = false;
    }
}
