using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShipSailView : WorldSpaceUI
{
    public event Action SailClicked;

    [SerializeField] private Button _sailButton;
    [SerializeField] private Image _resourceIcon;
    [SerializeField] private TextMeshProUGUI _amountText;

    private Vector3 _shownScale = Vector3.one;
    private bool _initializedScale;

    private void Awake()
    {
        CacheShownScale();
        // A disabled copy of this component sits on the call-ship canvas.
        // Its Awake must not turn that canvas off when the call button is shown.
        if (GetComponent<ShipCallView>() != null)
            return;

        HideOnAwake();
    }

    private void OnEnable()
    {
        if (_sailButton != null)
            _sailButton.onClick.AddListener(OnSailButtonClicked);
    }

    private void OnDisable()
    {
        if (_sailButton != null)
            _sailButton.onClick.RemoveListener(OnSailButtonClicked);
    }

    public void Initialize(int amount, Sprite icon)
    {
        if (_resourceIcon != null)
        {
            _resourceIcon.sprite = icon;
            _resourceIcon.enabled = icon != null;
        }

        if (_amountText != null)
            _amountText.text = $"x{CompactNumberFormatter.Format(Mathf.Max(1, amount))}";
    }

    public override void Show()
    {
        CacheShownScale();
        SetActiveWithoutAwakeHide(gameObject);
        transform.localScale = Vector3.zero;
        Tween.Scale(transform, _shownScale, 0.35f, Ease.InOutBack);
    }

    public override void Hide()
    {
        CacheShownScale();
        Tween.Scale(transform, Vector3.zero, 0.25f, Ease.InOutSine)
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void OnSailButtonClicked()
    {
        SailClicked?.Invoke();
    }

    private void CacheShownScale()
    {
        if (_initializedScale)
            return;

        Vector3 scale = transform.localScale;
        if (scale.sqrMagnitude > 0.0001f)
            _shownScale = scale;

        _initializedScale = true;
    }
}
