using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceCellView : MonoBehaviour
{
    public ResourceType ResourseType { get; private set; }
    public int Amount { get; private set; }
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amount;

    public void Initialize(ResourseConfig config, int amount)
    {
        ResourseType = config.ResourseType;
        _icon.sprite = config.Icon;
        Amount = amount;
        _amount.text = CompactNumberFormatter.Format(Amount);
    }

    public void UpdateAmount(int amount)
    {
        Amount = amount;
        _amount.text = CompactNumberFormatter.Format(Amount);
    }
}
