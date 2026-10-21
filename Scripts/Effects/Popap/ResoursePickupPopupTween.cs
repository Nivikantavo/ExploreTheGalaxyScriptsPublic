using System;
using System.Collections;
using PrimeTween;
using UnityEngine;

public sealed class ResoursePickupPopupTween : MonoBehaviour
{
    [Header("Rise")]
    [SerializeField] private float _riseDuration = 0.8f;
    [SerializeField] private float _riseDistance = 1.2f;
    [SerializeField] private Ease _riseEase = Ease.OutCubic;

    [Header("Fade")]
    [SerializeField] private float _fadeDelay = 0.25f;
    [SerializeField] private float _fadeDuration = 0.45f;
    [SerializeField] private Ease _fadeEase = Ease.InQuad;

    [Header("Scale")]
    [SerializeField] private float _scaleDuration = 0.2f;
    [SerializeField] private Ease _scaleEase = Ease.OutBack;

    [SerializeField] private CanvasGroup _canvasGroup;

    private Coroutine _routine;

    private void Awake()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Play(Vector3 fromPosition, Action release, Camera camera)
    {
        if (_routine != null)
            StopCoroutine(_routine);

        Tween.StopAll(transform);

        transform.position = fromPosition;
        transform.localScale = Vector3.one * 0.5f;

        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);

        _routine = StartCoroutine(PlayRoutine(release));
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        Tween.StopAll(transform);
    }

    private IEnumerator PlayRoutine(Action release)
    {
        var riseTo = transform.position + Vector3.up * _riseDistance;

        Tween.Scale(transform, Vector3.one, _scaleDuration, _scaleEase, useUnscaledTime: true);
        Tween.LocalPosition(transform, riseTo, _riseDuration, _riseEase, useUnscaledTime: true);

        yield return new WaitForSecondsRealtime(_fadeDelay);

        if (_canvasGroup != null)
            Tween.Alpha(_canvasGroup, 0f, _fadeDuration, _fadeEase, useUnscaledTime: true);

        var totalDuration = Mathf.Max(_riseDuration, _fadeDelay + _fadeDuration);
        yield return new WaitForSecondsRealtime(totalDuration - _fadeDelay);

        release?.Invoke();
    }
}
