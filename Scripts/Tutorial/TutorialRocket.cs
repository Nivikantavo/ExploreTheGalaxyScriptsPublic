using System;
using System.Collections;
using PrimeTween;
using UnityEngine;

public class TutorialRocket : MonoBehaviour
{
    public event Action Landed;

    [SerializeField] private ParticleSystem _launchParticleSystem;
    [SerializeField] private float _launchDelay = 1f;
    [SerializeField] private float _takeoffHeight = 30f;
    [SerializeField] private float _takeoffDuration = 2f;
    [SerializeField] private Ease _takeoffEase = Ease.InQuad;

    private bool _isLaunching;

    public IEnumerator Launch()
    {
        if (_isLaunching)
            yield break;

        _isLaunching = true;

        if (_launchParticleSystem != null)
            _launchParticleSystem.Play();

        yield return new WaitForSeconds(_launchDelay);

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + Vector3.up * _takeoffHeight;

        Tween tween = Tween.Custom(this, 0f, 1f, _takeoffDuration, ease: _takeoffEase,
            onValueChange: (self, progress) =>
            {
                self.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, progress);
            });

        yield return tween.ToYieldInstruction();
    }

    public IEnumerator Landing(Transform targetTransform)
    {
        Vector3 targetPosition = targetTransform.position;
        Vector3 startPosition = targetPosition + Vector3.up * 50f;
        transform.SetPositionAndRotation(startPosition, transform.rotation * Quaternion.Euler(180f, 0f, 0f));

        Tween tween = Tween.Custom(this, 0f, 1f, _takeoffDuration, ease: Ease.InQuad,
            onValueChange: (self, progress) =>
            {
                self.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, progress);
            });

        yield return tween.ToYieldInstruction();
        Landed?.Invoke();
    }
}
