using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class PopapView : WorldSpaceUI
{
    [SerializeField] protected Image Icon;
    [SerializeField] protected TextMeshProUGUI Amount;
    [SerializeField] protected CanvasGroup CanvasGroup;
    public abstract void Setup(Sprite icon, string amount);
}
