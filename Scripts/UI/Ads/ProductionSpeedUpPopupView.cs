using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class ProductionSpeedUpPopupView : MonoBehaviour
{
    [SerializeField] private ProductionSpeedUpConfigSO _config;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _remainingTimeText;
    [SerializeField] private Button _watchAdButton;
    [SerializeField] private TMP_Text _watchAdLabel;
    [SerializeField] private Button _softCurrencySkipButton;
    [SerializeField] private TMP_Text _softCurrencySkipLabel;
    [SerializeField] private Button _closeButton;
    [SerializeField] private ShopPanelView _shopPanel;

    private RewardedAdsController _rewardedAdsController;
    private Inventory _inventory;
    private ISaveLoader _saveLoader;
    private Action<int> _onTimeReductionGranted;
    private Func<int> _getRemainingSeconds;
    private bool _isShowing;

    public ProductionSpeedUpConfigSO Config => _config;

    [Inject]
    private void Construct(
        RewardedAdsController rewardedAdsController,
        Inventory inventory,
        ISaveLoader saveLoader)
    {
        _rewardedAdsController = rewardedAdsController;
        _inventory = inventory;
        _saveLoader = saveLoader;
    }

    private void Awake()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        if (_shopPanel == null)
            _shopPanel = FindFirstObjectByType<ShopPanelView>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (_watchAdButton != null)
            _watchAdButton.onClick.AddListener(OnWatchAdClicked);

        if (_softCurrencySkipButton != null)
            _softCurrencySkipButton.onClick.AddListener(OnSoftCurrencySkipClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        if (_watchAdButton != null)
            _watchAdButton.onClick.RemoveListener(OnWatchAdClicked);

        if (_softCurrencySkipButton != null)
            _softCurrencySkipButton.onClick.RemoveListener(OnSoftCurrencySkipClicked);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
    }

    public bool CanOfferSpeedUp(int remainingSeconds)
    {
        if (_config == null)
            return false;

        return remainingSeconds > _config.MinRemainingSecondsToShow;
    }

    public void Show(Func<int> getRemainingSeconds, Action<int> onTimeReductionGranted)
    {
        if (_config == null || getRemainingSeconds == null || onTimeReductionGranted == null)
            return;

        if (!CanOfferSpeedUp(getRemainingSeconds()))
            return;

        _getRemainingSeconds = getRemainingSeconds;
        _onTimeReductionGranted = onTimeReductionGranted;
        _isShowing = true;

        RefreshCopy();

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private void Update()
    {
        if (!_isShowing)
            return;

        RefreshRemainingTime();
    }

    public void Hide()
    {
        _isShowing = false;
        _getRemainingSeconds = null;
        _onTimeReductionGranted = null;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void RefreshCopy()
    {
        RefreshRemainingTime();

        bool softCurrencyEnabled = _config != null && _config.SoftCurrencySkipEnabled;
        if (_softCurrencySkipButton != null)
        {
            _softCurrencySkipButton.gameObject.SetActive(softCurrencyEnabled);
            _softCurrencySkipButton.interactable = softCurrencyEnabled;
        }
    }

    private void RefreshRemainingTime()
    {
        if (_config == null || _getRemainingSeconds == null)
            return;

        int remaining = Mathf.Max(0, _getRemainingSeconds());

        if (_remainingTimeText != null)
            _remainingTimeText.text = ProductionSpeedUpConfigSO.FormatRemainingTime(remaining);

        if (_watchAdLabel != null)
            _watchAdLabel.text = _config.GetSkipTitle(remaining);

        RefreshSoftCurrencyLabel(remaining);
    }

    private void RefreshSoftCurrencyLabel(int remainingSeconds)
    {
        if (_softCurrencySkipLabel == null || _config == null || !_config.SoftCurrencySkipEnabled)
            return;

        int cost = _config.GetSoftCurrencySkipCost(remainingSeconds);
        _softCurrencySkipLabel.text = cost.ToString();
    }

    private void OnWatchAdClicked()
    {
        if (!_isShowing || _config == null || _rewardedAdsController == null)
            return;

        int remaining = _getRemainingSeconds?.Invoke() ?? 0;
        if (!CanOfferSpeedUp(remaining))
        {
            Hide();
            return;
        }

        bool started = _rewardedAdsController.TryShowRewarded(
            _config.RewardedPlacement,
            OnRewardedCompleted);

        if (!started)
            return;
    }

    private void OnRewardedCompleted()
    {
        if (!_isShowing || _config == null)
            return;

        _onTimeReductionGranted?.Invoke(_config.RewardedTimeReductionSeconds);

        int remaining = _getRemainingSeconds?.Invoke() ?? 0;
        if (!CanOfferSpeedUp(remaining))
            Hide();
        else
            RefreshCopy();
    }

    private void OnSoftCurrencySkipClicked()
    {
        if (!_isShowing || _config == null || !_config.SoftCurrencySkipEnabled)
            return;

        int remaining = _getRemainingSeconds?.Invoke() ?? 0;
        if (!CanOfferSpeedUp(remaining))
        {
            Hide();
            return;
        }

        int cost = _config.GetSoftCurrencySkipCost(remaining);
        int gemBalance = _inventory != null ? _inventory.GetResourceAmount(ResourceType.Gem) : 0;

        if (gemBalance < cost)
        {
            Hide();
            OpenGemShop();
            return;
        }

        if (_inventory == null || cost <= 0)
            return;

        _inventory.RemoveResource(ResourceType.Gem, cost);
        _onTimeReductionGranted?.Invoke(remaining);
        _saveLoader?.Save();
        Hide();
    }

    private void OpenGemShop()
    {
        if (_shopPanel == null)
            _shopPanel = FindFirstObjectByType<ShopPanelView>(FindObjectsInactive.Include);

        _shopPanel?.ShowCurrency();
    }
}
