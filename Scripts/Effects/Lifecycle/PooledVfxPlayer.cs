using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PooledVfxPlayer : MonoBehaviour
{
    private ParticleSystem[] _particleSystems;
    private Coroutine _waitCoroutine;
    private Action _release;

    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
    }

    public void Play(Action release)
    {
        if (_waitCoroutine != null)
            StopCoroutine(_waitCoroutine);

        _release = release;

        if (_particleSystems.Length == 0)
        {
            Debug.LogWarning($"{nameof(PooledVfxPlayer)} on '{name}' has no particle systems.", this);
            _release?.Invoke();
            return;
        }

        foreach (var particleSystem in _particleSystems)
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        _waitCoroutine = StartCoroutine(WaitForCompletion());
    }

    private IEnumerator WaitForCompletion()
    {
        yield return null;

        while (HasAliveParticles())
            yield return null;

        var release = _release;
        _release = null;
        _waitCoroutine = null;
        release?.Invoke();
    }

    private bool HasAliveParticles()
    {
        foreach (var particleSystem in _particleSystems)
        {
            if (particleSystem.IsAlive(true))
                return true;
        }

        return false;
    }
}
