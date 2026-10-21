using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreatingUIComplitedView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _resourceCount;
    [SerializeField] private Image _instrumentIcon;
    [SerializeField] private Button _claimButton;
    private CreatingData _data;

    public void Initialize(CreatingData data)
    {
        _data = data;
        _resourceCount.text = CompactNumberFormatter.Format(_data.ResourceCount);
        _instrumentIcon.sprite = _data.ResourceIcon;
        _claimButton.onClick.AddListener(ClaimButtonClicked);
    }

    private void ClaimButtonClicked()
    {
        _data.OnClaimButtonClicked?.Invoke();
    }
}
