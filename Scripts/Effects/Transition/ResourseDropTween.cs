using System;
using System.Collections;
using PrimeTween;
using UnityEngine;

public sealed class ResourseDropTween : MonoBehaviour {
    [Header("Eject")]
    [SerializeField] float ejectDuration = 1f;
    [SerializeField] float ejectDistance = 1.0f;
    [SerializeField] float ejectUp = 1.6f;
    [SerializeField] Ease ejectEase = Ease.Default;

    [Header("Follow")]
    [SerializeField] float followDelay = 0f;
    [SerializeField] float flyDuration = 0.2f;
    [SerializeField] Ease flyEase = Ease.Default;

    [Header("Spin")]
    [SerializeField] float spinDuration = 1.5f;

    Coroutine routine;
    Tween ejectTween;
    Tween flyTween;

    public void Play(Vector3 fromPosition, Transform pickupTarget, Action release) 
    {
        if (routine != null) 
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopTweens();

        transform.position = fromPosition;

        routine = StartCoroutine(PlayRoutine(pickupTarget, release));
    }

    private void OnDisable() 
    {
        if (routine != null) 
        {
            StopCoroutine(routine);
            routine = null;
        }
        StopTweens();
    }

    private void StopTweens() 
    {
        if (ejectTween.isAlive) 
        {
            ejectTween.Stop();
        }
        if (flyTween.isAlive) 
        {
            flyTween.Stop();
        }
    }

    private IEnumerator PlayRoutine(Transform pickupTarget, Action release) 
    {
        var start = transform.position;

        var planar = UnityEngine.Random.insideUnitCircle.normalized;
        if (planar.sqrMagnitude < 0.001f) 
        {
            planar = Vector2.right;
        }

        var flightDir = new Vector3(planar.x, 0f, planar.y).normalized;
        var ejectTo = start + flightDir * ejectDistance;
        ejectTo.y += ejectUp;
        var peakHeight = ejectUp * UnityEngine.Random.Range(0.7f, 1.1f);
        var totalSpin = UnityEngine.Random.Range(540f, 900f);

        var spinAroundZ = Mathf.Abs(flightDir.x) >= Mathf.Abs(flightDir.z);
        var spinSign = spinAroundZ ? -Mathf.Sign(flightDir.x) : Mathf.Sign(flightDir.z);
        if (Mathf.Approximately(spinSign, 0f))
        {
            spinSign = 1f;
        }

        transform.localRotation = Quaternion.identity;

        ejectTween = Tween.Custom(this, 0f, 1f, ejectDuration, ease: ejectEase, useUnscaledTime: true,
            onValueChange: (self, t) => {
                var pos = Vector3.LerpUnclamped(start, ejectTo, t);
                pos.y += peakHeight * 4f * t * (1f - t);
                self.transform.position = pos;

                var spinProgress = Mathf.Clamp01(t * ejectDuration / spinDuration);
                var spin = totalSpin * spinSign * spinProgress;
                self.transform.localRotation = spinAroundZ
                    ? Quaternion.Euler(0f, 0f, spin)
                    : Quaternion.Euler(spin, 0f, 0f);
            });
        yield return new WaitForSeconds(ejectDuration - 0.1f);
        ejectTween.Stop();
        /*while (ejectTween.isAlive) 
        {
            yield return null;
        }*/

        yield return new WaitForSeconds(followDelay);

        if (pickupTarget == null) {
            release?.Invoke();
            yield break;
        }

        var targetPos = pickupTarget.position;
        flyTween = Tween.LocalPosition(transform, targetPos, flyDuration, flyEase, useUnscaledTime: true);

        while (flyTween.isAlive) 
        {
            yield return null;
        }

        release?.Invoke();
    }
}

