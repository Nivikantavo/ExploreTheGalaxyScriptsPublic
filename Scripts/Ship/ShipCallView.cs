using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class ShipCallView : WorldSpaceUI
{
    public event Action CallClicked;

    [SerializeField] private Button _callButton;

    private Vector3 _shownScale = Vector3.one;
    private bool _initializedScale;

    private void Awake()
    {
        CacheShownScale();
        HideOnAwake();
    }

    private void OnEnable()
    {
        if (_callButton != null)
            _callButton.onClick.AddListener(OnCallButtonClicked);
    }

    private void OnDisable()
    {
        if (_callButton != null)
            _callButton.onClick.RemoveListener(OnCallButtonClicked);
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

    private void OnCallButtonClicked()
    {
        CallClicked?.Invoke();
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
