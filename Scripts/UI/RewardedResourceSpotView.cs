using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space canvas panel for <see cref="RewardedResourceSpot"/>.
/// Assign this to <see cref="ApproachUIShower"/> as the UI to show on approach.
/// </summary>
public class RewardedResourceSpotView : WorldSpaceUI
{
    public event Action WatchClicked;
    public event Action PayClicked;

    [SerializeField] private Button _watchAdButton;
    [SerializeField] private Image _resourceIcon;
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private GameObject _adGroup;
    [SerializeField] private GameObject _payGroup;
    [SerializeField] private Button _payButtonSwitch;
    [SerializeField] private Button _adButtonSwitch;
    [SerializeField] private Button _payButton;
    [SerializeField] private TextMeshProUGUI _gemCostText;
    [SerializeField] private Image _payResourceIcon;
    [SerializeField] private TextMeshProUGUI _payAmountText;

    private Vector3 _shownScale = Vector3.one;
    private bool _initializedScale;

    /// <summary>
    /// 1 gem per full thousand resources, with a minimum of 1.
    /// 0–1999 = 1, 2000–2999 = 2, and so on.
    /// </summary>
    public static int GetGemCost(int resourceAmount)
    {
        int amount = Mathf.Max(0, resourceAmount);
        return Mathf.Max(1, amount / 1000);
    }

    private void Awake()
    {
        CacheShownScale();
        ShowAdGroup();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (_watchAdButton != null)
            _watchAdButton.onClick.AddListener(OnWatchButtonClicked);

        if (_payButtonSwitch != null)
            _payButtonSwitch.onClick.AddListener(ShowPayGroup);

        if (_adButtonSwitch != null)
            _adButtonSwitch.onClick.AddListener(ShowAdGroup);

        if (_payButton != null)
            _payButton.onClick.AddListener(OnPayButtonClicked);
    }

    private void OnDisable()
    {
        if (_watchAdButton != null)
            _watchAdButton.onClick.RemoveListener(OnWatchButtonClicked);

        if (_payButtonSwitch != null)
            _payButtonSwitch.onClick.RemoveListener(ShowPayGroup);

        if (_adButtonSwitch != null)
            _adButtonSwitch.onClick.RemoveListener(ShowAdGroup);

        if (_payButton != null)
            _payButton.onClick.RemoveListener(OnPayButtonClicked);
    }

    public void Initialize(ResourceType resourceType, int amount, Sprite icon)
    {
        ApplyResourcePresentation(_resourceIcon, _amountText, amount, icon);
        ApplyResourcePresentation(_payResourceIcon, _payAmountText, amount, icon);

        if (_gemCostText != null)
            _gemCostText.text = GetGemCost(amount).ToString();
    }

    public override void Show()
    {
        CacheShownScale();
        ShowAdGroup();
        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;
        Tween.Scale(transform, _shownScale, 0.35f, Ease.InOutBack);
    }

    public override void Hide()
    {
        CacheShownScale();
        Tween.Scale(transform, Vector3.zero, 0.25f, Ease.InOutSine)
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void OnWatchButtonClicked()
    {
        WatchClicked?.Invoke();
    }

    private void OnPayButtonClicked()
    {
        PayClicked?.Invoke();
    }

    private void ShowAdGroup()
    {
        if (_adGroup != null)
            _adGroup.SetActive(true);

        if (_payGroup != null)
            _payGroup.SetActive(false);
    }

    private void ShowPayGroup()
    {
        if (_adGroup != null)
            _adGroup.SetActive(false);

        if (_payGroup != null)
            _payGroup.SetActive(true);
    }

    private static void ApplyResourcePresentation(Image iconImage, TextMeshProUGUI amountText, int amount, Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (amountText != null)
            amountText.text = $"x{CompactNumberFormatter.Format(Mathf.Max(1, amount))}";
    }

    private void CacheShownScale()
    {
        if (_initializedScale)
            return;

        Vector3 scale = transform.localScale;
        if (scale.sqrMagnitude > 0.0001f)
            _shownScale = scale;

        _initializedScale = true;
    }
}
