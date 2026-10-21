using ECM2;
using UnityEngine;
using UnityEngine.AI;

public class CrocodileAnimation : MonoBehaviour
{
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private Animator _animator;
    [SerializeField] private Enemy _enemy;
    [SerializeField] private float _movingSpeedThreshold = 0.05f;
    [SerializeField] private bool _isMoving;

    private void Reset()
    {
        _animator = GetComponent<Animator>();
        _enemy = GetComponentInParent<Enemy>();
    }

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_enemy == null)
            _enemy = GetComponentInParent<Enemy>();
    }

    private void OnEnable()
    {
        if (_enemy != null)
            _enemy.Hit += OnHit;
    }

    private void OnDisable()
    {
        if (_enemy != null)
            _enemy.Hit -= OnHit;
    }

    private void Update()
    {
        if (_animator == null)
            return;

        _animator.SetBool(IsRunningHash, _isMoving || GetPlanarSpeed() > _movingSpeedThreshold);
    }

    public void SetMoving(bool moving)
    {
        _isMoving = moving;
    }

    public void PlayAttack()
    {
        if (_animator != null)
            _animator.SetTrigger(AttackHash);
    }

    public void StartHit()
    {
        if (_enemy != null)
            _enemy.EnableSword();
    }

    public void EndHit()
    {
        if (_enemy != null)
            _enemy.DisableSword();
    }

    private void OnHit()
    {
        PlayAttack();
    }

    private float GetPlanarSpeed()
    {
        if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
            return agent.velocity.magnitude;

        Character character = GetComponentInParent<Character>();
        if (character != null)
            return character.speed;

        return 0f;
    }
}
