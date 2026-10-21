using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;

public class InstrumentUpgraderProgressView : MonoBehaviour
{
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _nextLevelText;
    [SerializeField] private LocalizedString _nextLevelString;
    [SerializeField] private Image _nextLevelIcon;
    [SerializeField] private Button _speedUpButton;
    [SerializeField] private ProductionSpeedUpPopupView _speedUpPopup;

    private InstrumentUpgraderData _data;
    private int _timeForUpgrade = 0;

    public void Initialize(InstrumentUpgraderData data)
    {
        CleanupSubscriptions();

        _data = data;
        _progressSlider.maxValue = data.UpgradeTime;
        _timeForUpgrade = data.UpgradeTime;

        if (_speedUpPopup == null)
            _speedUpPopup = FindFirstObjectByType<ProductionSpeedUpPopupView>(FindObjectsInactive.Include);

        if (_speedUpButton != null)
        {
            _speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
            _speedUpButton.onClick.AddListener(OnSpeedUpClicked);
        }

        OnUpgradeTimeChanged(data.ElapsedTime);
        _nextLevelText.text = $"{LocalizationSafe.Get(_nextLevelString)} {data.NextLevel}";
        _nextLevelIcon.sprite = data.NextLevelIcon;

        _data.OnUpgradeTimeChanged += OnUpgradeTimeChanged;
        _data.OnComplited += OnProgressEnded;
    }

    public void OnProgressEnded()
    {
        CleanupSubscriptions();
    }

    private void OnDestroy()
    {
        CleanupSubscriptions();
    }

    private void CleanupSubscriptions()
    {
        if (_data != null)
        {
            _data.OnUpgradeTimeChanged -= OnUpgradeTimeChanged;
            _data.OnComplited -= OnProgressEnded;
        }

        if (_speedUpButton != null)
            _speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
    }

    private void OnUpgradeTimeChanged(int elapsedTime)
    {
        _progressSlider.value = elapsedTime;
        int remaining = _timeForUpgrade - elapsedTime;
        _progressText.text = $"{remaining / 60}:{remaining % 60:00}";
        UpdateSpeedUpButton(remaining);
    }

    private void UpdateSpeedUpButton(int remainingSeconds)
    {
        if (_speedUpButton == null)
            return;

        bool canOffer = _speedUpPopup != null && _speedUpPopup.CanOfferSpeedUp(remainingSeconds);
        _speedUpButton.gameObject.SetActive(canOffer);
        _speedUpButton.interactable = canOffer;
    }

    private void OnSpeedUpClicked()
    {
        if (_data == null || _speedUpPopup == null)
            return;

        _speedUpPopup.Show(
            () => _data.RemainingSeconds,
            seconds => _data.ApplyTimeReduction(seconds));
    }
}
