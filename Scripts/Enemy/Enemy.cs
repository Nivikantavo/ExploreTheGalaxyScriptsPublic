using System;
using System.Collections;
using ECM2;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour, IResourceKeeper, IResourcesTransition, IHealthObserver
{
    public event Action<float> HealthValueChange;
    public event Action EnemyDied;
    public event Action Hit;
    public event Action<ResourcesTransitionViewData> OnResourcesTransition;
    public event Action<float> OnHealthInitialized;

    public ResourceType ResourseType => _resourseConfig.ResourseType;

    private const string AttackStateName = "Attack";
    private const float AttackLockTimeout = 3f;
    private const float NavMeshSampleDistance = 2f;

    [SerializeField] private NavMeshCharacter _navMeshCharacter;
    [SerializeField] private float _attackCooldown = 0.5f;
    [SerializeField] private float _attackDistance = 0.5f;
    [SerializeField] private float _faceAngleThreshold = 10f;
    [SerializeField] private EnemySword _enemySword;
    private readonly int _playerLayerMask = 1 << 7;
    private Transform _guardianFieldCenter;
    private float _guardianFieldRadius;
    private float _attackTimer = 0f;
    private Health _health;
    private Transform _target;
    private WaitForSeconds _waitInterval;
    private float _offset = 0.5f;
    private ResourseConfig _resourseConfig;
    private int _resourceCount;
    private Animator _animator;
    private bool _isAttacking;
    private bool _attackStateEntered;
    private float _attackLockTimer;
    private bool _isReturningHome;
    private Coroutine _returnHomeRoutine;
    public void Initialize(int maxHealth, InstrumentConfig instrumentConfig, Transform guardianFieldCenter,
                         float guardRadius, ResourseConfig resourceConfig, int resourceCount)
    {
        Instrument instrument = new Instrument(instrumentConfig.Type ,instrumentConfig.Level ,instrumentConfig.DamagePerHit, instrumentConfig.InstrumentObj);
        _enemySword.Initialize(instrument, instrumentConfig.RotationOffset);
        _health = new Health(maxHealth);
        OnHealthInitialized?.Invoke(maxHealth);
        _guardianFieldCenter = guardianFieldCenter;
        _guardianFieldRadius = guardRadius;
        _attackTimer = _attackCooldown;
        _waitInterval = new(1.5f);
        _resourseConfig = resourceConfig;
        _resourceCount = resourceCount;
    }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    public void Reset()
    {
        StopAllCoroutines();
        _returnHomeRoutine = null;
        _isReturningHome = false;
        _attackTimer = _attackCooldown;
        _health.RestoreHealth();
        DisableSword();
        OnHealthInitialized?.Invoke(_health.MaxHealth);
        _target = null;
        ClearAttackLock();
        HaltMovement();
    }

    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        Character character = _navMeshCharacter != null ? _navMeshCharacter.character : null;
        if(character != null)
        {
            character.TeleportPosition(position, true, true);
            character.SetRotation(rotation);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        SnapAgentToNavMesh();
        HaltMovement();
    }

    public void EnableSword() => _enemySword.EnableCollide();
    public void DisableSword() => _enemySword.DisableCollide();

    public int KnockOutResources(Instrument instrument, Transform pickupTarget)
    {
        int knokedResources = 0;

        _health.TakeDamage(instrument.DamagePerHit);

        if(_health.CurrentHealth <= 0)
        {
            OnResourcesTransition?.Invoke(new ResourcesTransitionViewData(
                            count: _resourceCount,
                            config: _resourseConfig,
                            startPosition: transform.position,
                            pickupTarget: pickupTarget));

            knokedResources = _resourceCount;
        }
        else
        {
            HealthValueChange?.Invoke(_health.CurrentHealth);
        }

        if(_health.CurrentHealth <= 0)
        {
            Die();
        }
        
        return knokedResources;
    }

    private void Update() 
    {
        _attackTimer += Time.deltaTime;

        if(_isAttacking)
        {
            _navMeshCharacter.StopMovement();
            UpdateAttackLock();
            return;
        }

        if(_target == null)
        {
            _target = FindPlayer();
            if(_target != null)
                CancelReturnHome();
            else if(!_isReturningHome)
                HaltMovement();

            return;
        }

        if(Vector3.Distance(_guardianFieldCenter.position, _target.position) > _guardianFieldRadius + _offset)
        {
            _target = null;
            BeginReturnHome();
            return;
        }

        if(Vector3.Distance(transform.position, _target.position) > _attackDistance)
        {
            MoveTo(_target.position);
            return;
        }

        _navMeshCharacter.StopMovement();

        if(!IsFacingTarget())
        {
            RotateTowardsTarget();
            return;
        }

        if(_attackTimer >= _attackCooldown)
            BeginAttack();
    }

    private void BeginAttack()
    {
        _attackTimer = 0f;
        _isAttacking = true;
        _attackStateEntered = false;
        _attackLockTimer = 0f;
        _navMeshCharacter.StopMovement();
        HitArea();
    }

    private void UpdateAttackLock()
    {
        _attackLockTimer += Time.deltaTime;

        bool inAttack = IsInAttackState();
        if(inAttack)
            _attackStateEntered = true;

        if(_attackStateEntered && !inAttack)
        {
            ClearAttackLock();
            return;
        }

        if(!_attackStateEntered && _attackLockTimer >= AttackLockTimeout)
            ClearAttackLock();
    }

    private bool IsInAttackState()
    {
        if(_animator == null)
            return false;

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
        if(!_animator.IsInTransition(0))
            return current.IsName(AttackStateName);

        AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
        return current.IsName(AttackStateName) || next.IsName(AttackStateName);
    }

    private void ClearAttackLock()
    {
        _isAttacking = false;
        _attackStateEntered = false;
        _attackLockTimer = 0f;
    }

    private bool IsFacingTarget()
    {
        Vector3 toTarget = GetPlanarDirectionToTarget();
        if(toTarget.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if(forward.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Angle(forward, toTarget) <= _faceAngleThreshold;
    }

    private void RotateTowardsTarget()
    {
        Vector3 toTarget = GetPlanarDirectionToTarget();
        if(toTarget.sqrMagnitude <= 0.0001f)
            return;

        _navMeshCharacter.character.RotateTowards(toTarget, Time.deltaTime, true);
    }

    private Vector3 GetPlanarDirectionToTarget()
    {
        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;
        return toTarget;
    }

    private void HitArea()
    {
        Hit?.Invoke();
    }

    private Transform FindPlayer()
    {
        Collider[] targets = Physics.OverlapSphere(_guardianFieldCenter.position, _guardianFieldRadius, _playerLayerMask);
        
        foreach(Collider collider in targets)
        {
            if(collider.TryGetComponent(out Player player))
            {
                return player.transform;
            }
        }

        return null;
    }

    private void Die()
    {
        StopAllCoroutines();
        _returnHomeRoutine = null;
        _isReturningHome = false;
        _target = null;
        ClearAttackLock();
        HaltMovement();
        EnemyDied?.Invoke();
        gameObject.SetActive(false);
    }

    private void BeginReturnHome()
    {
        if(_returnHomeRoutine != null)
            return;

        _isReturningHome = true;
        _returnHomeRoutine = StartCoroutine(WaitAndGo(_guardianFieldCenter.position));
    }

    private void CancelReturnHome()
    {
        if(_returnHomeRoutine != null)
        {
            StopCoroutine(_returnHomeRoutine);
            _returnHomeRoutine = null;
        }

        _isReturningHome = false;
    }

    private void HaltMovement()
    {
        if(_navMeshCharacter == null)
            return;

        Character character = _navMeshCharacter.character;
        if(character != null)
        {
            character.SetMovementDirection(Vector3.zero);
            character.SetVelocity(Vector3.zero);
        }

        NavMeshAgent agent = _navMeshCharacter.agent;
        if(agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.nextPosition = transform.position;
    }

    private void MoveTo(Vector3 destination)
    {
        NavMeshAgent agent = _navMeshCharacter.agent;
        if(agent != null && agent.isActiveAndEnabled)
            agent.isStopped = false;

        _navMeshCharacter.MoveToDestination(destination);
    }

    private void SnapAgentToNavMesh()
    {
        NavMeshAgent agent = _navMeshCharacter != null ? _navMeshCharacter.agent : null;
        if(agent == null || !agent.isActiveAndEnabled)
            return;

        if(!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshSampleDistance, NavMesh.AllAreas))
            return;

        agent.Warp(hit.position);
    }

    private IEnumerator WaitAndGo(Vector3 targetPosition)
    {
        yield return _waitInterval;
        _returnHomeRoutine = null;

        if(_target != null)
        {
            _isReturningHome = false;
            yield break;
        }

        MoveTo(targetPosition);
    }
}
