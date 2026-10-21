using UnityEngine;
using UnityEngine.Localization;

public class LowLevelHitPopupView : PopapView
{
    [SerializeField] private LocalizedString _localizedString;
    public override void Setup(Sprite icon, string amount)
    {
        if (Icon != null)
            Icon.sprite = icon;
        string text = LocalizationSafe.Get(_localizedString);
        if (Amount != null)
            Amount.text = $"{text} {amount}";

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
