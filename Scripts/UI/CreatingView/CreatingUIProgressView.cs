using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreatingUIProgressView : MonoBehaviour
{
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _resourceCountText;
    [SerializeField] private Image _resourceIcon;
    [SerializeField] private Button _speedUpButton;
    [SerializeField] private ProductionSpeedUpPopupView _speedUpPopup;

    private CreatingData _data;
    private int _timeForCreatingAll = 0;

    public void Initialize(CreatingData data)
    {
        CleanupSubscriptions();

        _data = data;
        _progressSlider.maxValue = data.TimeForCreatingAll;
        _timeForCreatingAll = data.TimeForCreatingAll;

        if (_speedUpPopup == null)
            _speedUpPopup = FindFirstObjectByType<ProductionSpeedUpPopupView>(FindObjectsInactive.Include);

        if (_speedUpButton != null)
        {
            _speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
            _speedUpButton.onClick.AddListener(OnSpeedUpClicked);
        }

        OnUpgradeTimeChanged(data.ElapsedTime);

        _resourceCountText.text = CompactNumberFormatter.Format(data.ResourceCount);
        _resourceIcon.sprite = data.ResourceIcon;

        _data.OnProgressTimeChanged += OnUpgradeTimeChanged;
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
            _data.OnProgressTimeChanged -= OnUpgradeTimeChanged;
            _data.OnComplited -= OnProgressEnded;
        }

        if (_speedUpButton != null)
            _speedUpButton.onClick.RemoveListener(OnSpeedUpClicked);
    }

    private void OnUpgradeTimeChanged(int elapsedTime)
    {
        _progressSlider.value = elapsedTime;
        int remaining = _timeForCreatingAll - elapsedTime;
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
