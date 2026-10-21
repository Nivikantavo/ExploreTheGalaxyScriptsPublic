using PrimeTween;
using UnityEngine;

public class ShakingAnimation : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _shakeDuration = 0.1f;
    private IHitAnimated _hitAnimated;
    
    
    private void Awake()
    {
        _hitAnimated = GetComponentInParent<IHitAnimated>();
        if(_target == null)
        {
            _target = transform;
        }
    }

    private void OnEnable() 
    {
        _hitAnimated.OnHit += PlayHitAnimation;
    }

    private void OnDisable() 
    {
        _hitAnimated.OnHit -= PlayHitAnimation;
    }

    private void PlayHitAnimation()
    {
        _target.localRotation = Quaternion.identity * Quaternion.Euler(0, 0, UnityEngine.Random.Range(-10, 10));
        Tween.LocalRotation(_target, Quaternion.identity, _shakeDuration, Ease.InOutSine, useUnscaledTime: true);
    }
}
