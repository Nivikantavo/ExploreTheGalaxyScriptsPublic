using System.Collections;
using Playgama;
using Playgama.Modules.Advertisement;
using UnityEngine;
using Zenject;

public class InterstitialAdsController : MonoBehaviour, IInterstitialAds, IInterstitialCooldown
{
    [SerializeField] private CelebrationPopupView _celebrationPopup;

    private readonly CelebrationProgressGate _gatedCelebrationGate = new();

    private bool _subscribedToAdvertisement;
    private bool _isGameplayPausedByAd;
    private IAudioService _audioService;
    private AdsRemovalState _adsRemoval;
    private InterstitialWaitSession _waitSession;

    [Inject]
    private void Construct(IAudioService audioService, AdsRemovalState adsRemoval)
    {
        _audioService = audioService;
        _adsRemoval = adsRemoval;
    }

    private void Awake()
    {
        if (_celebrationPopup == null)
            _celebrationPopup = FindFirstObjectByType<CelebrationPopupView>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (_celebrationPopup != null)
            _celebrationPopup.CelebrationClosed += OnCelebrationClosed;

        TrySubscribeAdvertisement();
    }

    private void Start()
    {
        // Bridge modules may finish wiring after Awake.
        TrySubscribeAdvertisement();
    }

    private void Update()
    {
        // Bridge can become available after scene start on WebGL.
        if (!_subscribedToAdvertisement)
            TrySubscribeAdvertisement();
    }

    private void OnDisable()
    {
        if (_celebrationPopup != null)
            _celebrationPopup.CelebrationClosed -= OnCelebrationClosed;

        UnsubscribeAdvertisement();
        CompleteWaitSession();
        ResumeGameplay();
    }

    private void OnCelebrationClosed()
    {
        if (!CanShow())
        {
            MarkInterstitialFinished();
            return;
        }

        TryShowInterstitial(InterstitialPlacements.Celebration);
    }

    public bool CanShowGatedCelebration() => _gatedCelebrationGate.CanShowGated(Time.unscaledTime);

    public void MarkGatedCelebrationQueued() => _gatedCelebrationGate.MarkGatedPopupQueued();

    private void MarkInterstitialFinished() =>
        _gatedCelebrationGate.MarkInterstitialFinished(Time.unscaledTime);

    public void TryShowInterstitial(string placement = null)
    {
        if (!CanShow())
            return;

        Bridge.advertisement.ShowInterstitial(placement);
    }

    public IEnumerator WaitForInterstitial(string placement)
    {
        TrySubscribeAdvertisement();

        if (_waitSession != null && !_waitSession.IsComplete)
        {
            yield return WaitUntilSessionFinishes(_waitSession);
            yield break;
        }

        if (IsInterstitialOpen())
        {
            InterstitialWaitSession openSession = EnsureWaitSession();
            openSession.NotifyOpened();
            yield return WaitUntilSessionFinishes(openSession);
            yield break;
        }

        if (!CanShow())
            yield break;

        InterstitialWaitSession session = EnsureWaitSession();
        Bridge.advertisement.ShowInterstitial(placement);
        yield return WaitUntilSessionFinishes(session);
    }

    private IEnumerator WaitUntilSessionFinishes(InterstitialWaitSession session)
    {
        float startedUnscaled = Time.unscaledTime;
        while (!session.ShouldFinish(Time.unscaledTime - startedUnscaled))
            yield return null;

        if (_waitSession == session)
            _waitSession = null;
    }

    private bool CanShow()
    {
        if (_adsRemoval != null && _adsRemoval.AdsRemoved)
            return false;

        if (Bridge.instance == null || Bridge.advertisement == null)
            return false;

        return Bridge.advertisement.isInterstitialSupported;
    }

    private static bool IsInterstitialOpen()
    {
        if (Bridge.advertisement == null)
            return false;

        InterstitialState state = Bridge.advertisement.interstitialState;
        return state == InterstitialState.Loading || state == InterstitialState.Opened;
    }

    private InterstitialWaitSession EnsureWaitSession()
    {
        if (_waitSession == null || _waitSession.IsComplete)
            _waitSession = new InterstitialWaitSession();

        return _waitSession;
    }

    private void CompleteWaitSession()
    {
        if (_waitSession == null)
            return;

        _waitSession.NotifyFinished();
        _waitSession = null;
    }

    private void TrySubscribeAdvertisement()
    {
        if (_subscribedToAdvertisement || Bridge.advertisement == null)
            return;

        Bridge.advertisement.interstitialStateChanged += OnInterstitialStateChanged;
        _subscribedToAdvertisement = true;
    }

    private void UnsubscribeAdvertisement()
    {
        if (!_subscribedToAdvertisement || Bridge.advertisement == null)
            return;

        Bridge.advertisement.interstitialStateChanged -= OnInterstitialStateChanged;
        _subscribedToAdvertisement = false;
    }

    private void OnInterstitialStateChanged(InterstitialState state)
    {
        switch (state)
        {
            case InterstitialState.Loading:
            case InterstitialState.Opened:
                EnsureWaitSession().NotifyOpened();
                PauseGameplay();
                break;

            case InterstitialState.Closed:
            case InterstitialState.Failed:
                MarkInterstitialFinished();
                CompleteWaitSession();
                ResumeGameplay();
                break;
        }
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
