using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ItemPriceView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amount;

    public ResourceType ResourseType { get; private set; }
    public void Initialize(ResourceType resourseType, int amount, Sprite icon)
    {
        ResourseType = resourseType;
        _icon.sprite = icon;
        _amount.text = CompactNumberFormatter.Format(amount);
    }

    public void UpdateAmount(int amount)
    {
        _amount.text = CompactNumberFormatter.Format(amount);
    }

    public void SetAvailable(bool isAvailable)
    {
        _amount.color = isAvailable ? Color.green : Color.red;
    }

    public void ShowIcon(Sprite icon)
    {
        _icon.sprite = icon;
        _icon.preserveAspect = true;
        _icon.enabled = icon != null;

        if (_amount != null)
            _amount.gameObject.SetActive(false);

        RectTransform iconRect = _icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(0.28f, 0.28f);
    }
}
