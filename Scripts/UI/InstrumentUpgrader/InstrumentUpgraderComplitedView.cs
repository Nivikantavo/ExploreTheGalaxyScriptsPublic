using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class InstrumentUpgraderComplitedView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private LocalizedString _levelString;
    [SerializeField] private Image _instrumentIcon;
    [SerializeField] private Button _claimButton;
    private InstrumentUpgraderData _data;

    public void Initialize(InstrumentUpgraderData data)
    {
        _data = data;
        _levelText.text = $"{LocalizationSafe.Get(_levelString)} {_data.NextLevel}";
        _instrumentIcon.sprite = _data.NextLevelIcon;
        _claimButton.onClick.RemoveListener(ClaimButtonClicked);
        _claimButton.onClick.AddListener(ClaimButtonClicked);
    }

    private void ClaimButtonClicked()
    {
        _data.OnClaimButtonClicked?.Invoke();
    }
}
