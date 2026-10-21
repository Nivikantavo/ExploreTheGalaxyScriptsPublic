using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopStarterPackResourceSlotView : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _amountText;

    public void Bind(Sprite icon, string amountLabel)
    {
        if (_iconImage != null)
        {
            if (icon != null)
            {
                _iconImage.enabled = true;
                _iconImage.sprite = icon;
            }
            else
            {
                _iconImage.enabled = false;
            }
        }

        if (_amountText != null)
            _amountText.text = amountLabel ?? string.Empty;
    }
}
