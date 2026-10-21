using TMPro;
using UnityEngine;

public class ResoursePickupPopupView : PopapView
{

    public override void Setup(Sprite icon, string amount)
    {
        if (Icon != null)
            Icon.sprite = icon;

        if (Amount != null)
            Amount.text = $"+{amount}";

        if (CanvasGroup != null)
            CanvasGroup.alpha = 1f;
    }

    public override void Show()
    {
        gameObject.SetActive(true);
    }

    public override void Hide()
    {
        gameObject.SetActive(false);
    }
}