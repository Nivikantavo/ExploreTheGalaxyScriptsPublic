using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthView : WorldSpaceUI
{
    [SerializeField] private Canvas _healthCanvas;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Slider _healthBar;
    [SerializeField] private TextMeshProUGUI _healthText;
    [SerializeField] private float _disablingTime;

    private IHealthObserver _healthObserver;
    private Coroutine _disablingCoroutine;
    private WaitForSeconds _waitForDisabling;

    private void Awake() 
    {
        _healthObserver = GetComponentInParent<IHealthObserver>();
        _waitForDisabling = new WaitForSeconds(_disablingTime);
        _healthCanvas.worldCamera = Camera.main;
        _healthObserver.HealthValueChange += OnHealthValueChange;
        _healthObserver.OnHealthInitialized += OnHealthInitialized;
        _healthCanvas.enabled = false;
        Hide();
    }

    private void OnDestroy()
    {
        _healthObserver.HealthValueChange -= OnHealthValueChange;
        _healthObserver.OnHealthInitialized -= OnHealthInitialized;
    }

    private void OnHealthValueChange(float newValue)
    {
        Show();
        MoveSliderSmoothly(newValue, 0.1f);

        if(_disablingCoroutine != null)
        {
            StopCoroutine(_disablingCoroutine);
            _disablingCoroutine = null;
        }

        _disablingCoroutine = StartCoroutine(DisablingCoroutine());
    }

    private void OnHealthInitialized(float maxHealth)
    {
        _healthBar.maxValue = maxHealth;
        _healthBar.value = maxHealth;
        _healthText.text = maxHealth.ToString();
    }

    private void MoveSliderSmoothly(float targetValue, float duration)
    {
        Tween.Custom(
            startValue: _healthBar.value, 
            endValue: targetValue, 
            duration: duration, 
            onValueChange: newVal => _healthBar.value = newVal,
            ease: Ease.OutCubic
        );
    }

    private IEnumerator DisablingCoroutine()
    {
        yield return _waitForDisabling;
        Hide();
    }

    public override void Show()
    {
        _healthCanvas.enabled = true;
        _canvasGroup.alpha = 1;
    }

    public override void Hide()
    {
        Tween.Custom(
            startValue: _canvasGroup.alpha, 
            endValue: 0, 
            duration: 1, 
            onValueChange: newVal => _canvasGroup.alpha = newVal,
            ease: Ease.InQuad
        ).OnComplete(() => 
        {
            _healthCanvas.enabled = false;
        });
    }
}
