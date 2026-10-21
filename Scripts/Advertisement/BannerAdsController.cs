using System;
using Playgama;
using Playgama.Modules.Advertisement;
using UnityEngine;
using Zenject;

/// <summary>
/// Regular banner: crazy_games, yandex.
/// Advanced banners: playgama (and crazy_games as fallback).
/// y8: not supported — calls are no-ops.
/// Show on idle screens (main menu, pause); hide during gameplay (required on Crazy Games).
/// </summary>
public class BannerAdsController : MonoBehaviour
{
    private enum BannerMode
    {
        None,
        Regular,
        Advanced
    }

    [SerializeField] private BannerPosition _position = BannerPosition.Bottom;
    [SerializeField] private string _defaultPlacement = "main_menu";

    private bool _subscribedToAdvertisement;
    private bool _isVisible;
    private BannerMode _activeMode = BannerMode.None;
    private string _currentPlacement;
    private AdsRemovalState _adsRemoval;

    public bool IsVisible => _isVisible;
    public BannerPosition Position => _position;
    public string DefaultPlacement => _defaultPlacement;

    [Inject]
    private void Construct(AdsRemovalState adsRemoval)
    {
        _adsRemoval = adsRemoval;
        if (_adsRemoval != null)
            _adsRemoval.Changed += OnAdsRemovalChanged;
    }

    private void OnDestroy()
    {
        if (_adsRemoval != null)
            _adsRemoval.Changed -= OnAdsRemovalChanged;
    }

    private void OnAdsRemovalChanged()
    {
        if (_adsRemoval != null && _adsRemoval.AdsRemoved)
            Hide();
    }

    public BannerState CurrentState
    {
        get
        {
            if (Bridge.advertisement == null)
                return BannerState.Hidden;

            return _activeMode == BannerMode.Advanced
                ? Bridge.advertisement.advancedBannersState
                : Bridge.advertisement.bannerState;
        }
    }

    public bool IsSupported
    {
        get
        {
            if (Bridge.instance == null || Bridge.advertisement == null)
                return false;

            return Bridge.advertisement.isBannerSupported
                || Bridge.advertisement.isAdvancedBannersSupported;
        }
    }

    public string CurrentPlatformId
    {
        get
        {
            if (Bridge.instance == null || Bridge.platform == null)
                return null;

            return Bridge.platform.id;
        }
    }

    private void OnEnable()
    {
        TrySubscribeAdvertisement();
    }

    private void Start()
    {
        TrySubscribeAdvertisement();
    }

    private void Update()
    {
        if (!_subscribedToAdvertisement)
            TrySubscribeAdvertisement();
    }

    private void OnDisable()
    {
        Hide();
        UnsubscribeAdvertisement();
    }

    /// <summary>
    /// Safe to call repeatedly; reuses the active mode until hidden.
    /// </summary>
    public bool TryShow(string placement = null)
    {
        if (_adsRemoval != null && _adsRemoval.AdsRemoved)
            return false;

        if (Bridge.instance == null || Bridge.advertisement == null)
            return false;

        string resolvedPlacement = string.IsNullOrWhiteSpace(placement)
            ? _defaultPlacement
            : placement;

        BannerMode mode = ResolveBannerMode();
        if (mode == BannerMode.None)
            return false;

        if (_isVisible
            && _activeMode == mode
            && string.Equals(_currentPlacement, resolvedPlacement, StringComparison.Ordinal))
        {
            return true;
        }

        if (_isVisible && _activeMode != mode)
            HideInternal();

        switch (mode)
        {
            case BannerMode.Regular:
                Bridge.advertisement.ShowBanner(_position, resolvedPlacement);
                break;
            case BannerMode.Advanced:
                Bridge.advertisement.ShowAdvancedBanners(resolvedPlacement);
                break;
            default:
                return false;
        }

        _activeMode = mode;
        _currentPlacement = resolvedPlacement;
        _isVisible = true;
        return true;
    }

    public void Hide()
    {
        if (!_isVisible && _activeMode == BannerMode.None)
            return;

        HideInternal();
    }

    private void HideInternal()
    {
        if (Bridge.advertisement != null)
        {
            switch (_activeMode)
            {
                case BannerMode.Regular:
                    Bridge.advertisement.HideBanner();
                    break;
                case BannerMode.Advanced:
                    Bridge.advertisement.HideAdvancedBanners();
                    break;
                case BannerMode.None:
                    // Mode unknown (e.g. after failed show) — clear both safely.
                    if (Bridge.advertisement.isBannerSupported)
                        Bridge.advertisement.HideBanner();
                    if (Bridge.advertisement.isAdvancedBannersSupported)
                        Bridge.advertisement.HideAdvancedBanners();
                    break;
            }
        }

        _isVisible = false;
        _activeMode = BannerMode.None;
        _currentPlacement = null;
    }

    private static BannerMode ResolveBannerMode()
    {
        if (Bridge.advertisement == null)
            return BannerMode.None;

        // Prefer regular banner (crazy_games, yandex). Fall back to advanced (playgama).
        if (Bridge.advertisement.isBannerSupported)
            return BannerMode.Regular;

        if (Bridge.advertisement.isAdvancedBannersSupported)
            return BannerMode.Advanced;

        // y8 and other unsupported platforms
        return BannerMode.None;
    }

    private void TrySubscribeAdvertisement()
    {
        if (_subscribedToAdvertisement || Bridge.advertisement == null)
            return;

        Bridge.advertisement.bannerStateChanged += OnBannerStateChanged;
        Bridge.advertisement.advancedBannersStateChanged += OnAdvancedBannersStateChanged;
        _subscribedToAdvertisement = true;
    }

    private void UnsubscribeAdvertisement()
    {
        if (!_subscribedToAdvertisement || Bridge.advertisement == null)
            return;

        Bridge.advertisement.bannerStateChanged -= OnBannerStateChanged;
        Bridge.advertisement.advancedBannersStateChanged -= OnAdvancedBannersStateChanged;
        _subscribedToAdvertisement = false;
    }

    private void OnBannerStateChanged(BannerState state)
    {
        if (_activeMode != BannerMode.Regular)
            return;

        ApplyState(state);
    }

    private void OnAdvancedBannersStateChanged(BannerState state)
    {
        if (_activeMode != BannerMode.Advanced)
            return;

        ApplyState(state);
    }

    private void ApplyState(BannerState state)
    {
        switch (state)
        {
            case BannerState.Shown:
            case BannerState.Loading:
                _isVisible = true;
                break;

            case BannerState.Hidden:
            case BannerState.Failed:
                _isVisible = false;
                if (state == BannerState.Failed || state == BannerState.Hidden)
                {
                    _activeMode = BannerMode.None;
                    _currentPlacement = null;
                }

                break;
        }
    }
}
