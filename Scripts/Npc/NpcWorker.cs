using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcWorker : MonoBehaviour
{
    private static readonly int DuckHash = Animator.StringToHash("Duck");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int WaveHash = Animator.StringToHash("Wave");

    public event Action Arrived;

    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _arrivedDistance = 0.4f;
    [SerializeField] private float _warpTimeout = 8f;
    [SerializeField] private float _navMeshSampleDistance = 2f;
    [SerializeField] private float _animationFadeDuration = 0.15f;

    private Transform _destination;
    private bool _isTraveling;
    private bool _parentOnArrival;
    private bool _playWaveOnArrival = true;
    private float _travelTimer;
    private int _poseHash;

    public bool IsTraveling => _isTraveling;

    private void Reset()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>(true);
    }

    private void Awake()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        if (_animator != null)
            _animator.applyRootMotion = false;

        if (_agent != null)
            _agent.enabled = false;

        if (_animator != null)
            _animator.keepAnimatorStateOnDisable = true;

        PlayState(DuckHash);
    }

    private void OnEnable()
    {
        if (_poseHash != 0)
            PlayState(_poseHash);
    }

    public void SitInCage()
    {
        _isTraveling = false;
        _destination = null;
        _parentOnArrival = false;
        _playWaveOnArrival = true;
        _travelTimer = 0f;
        DisableAgent();
        PlayState(DuckHash);
    }

    public void RunTo(Transform destination)
    {
        RunTo(destination, parentOnArrival: false, playWaveOnArrival: true);
    }

    public void RunTo(Transform destination, bool parentOnArrival, bool playWaveOnArrival)
    {
        _destination = destination;
        _parentOnArrival = parentOnArrival;
        _playWaveOnArrival = playWaveOnArrival;
        _isTraveling = true;
        _travelTimer = 0f;

        if (_destination == null)
        {
            CompleteArrival();
            return;
        }

        EnableAgentAndSetDestination();
        PlayState(RunHash);
    }

    public void SnapTo(Transform destination)
    {
        SnapTo(destination, parentToDestination: false);
    }

    public void SnapTo(Transform destination, bool parentToDestination)
    {
        _isTraveling = false;
        _destination = destination;
        _parentOnArrival = parentToDestination;
        _playWaveOnArrival = false;
        _travelTimer = 0f;
        DisableAgent();
        ApplyArrivalPose();
        PlayState(IdleHash);
    }

    private void Update()
    {
        if (!_isTraveling || _destination == null)
            return;

        _travelTimer += Time.deltaTime;
        TryKeepAgentOnPath();

        if (HasReachedDestination() || _travelTimer >= _warpTimeout)
            CompleteArrival();
    }

    private void EnableAgentAndSetDestination()
    {
        if (_agent == null || _destination == null)
            return;

        _agent.enabled = true;
        _agent.stoppingDistance = Mathf.Max(_agent.stoppingDistance, _arrivedDistance);
        TryPlaceOnNavMesh(transform.position);
        SetDestinationIfPossible();
    }

    private void TryKeepAgentOnPath()
    {
        if (_agent == null)
            return;

        if (!_agent.enabled)
            _agent.enabled = true;

        if (!_agent.isOnNavMesh)
            TryPlaceOnNavMesh(transform.position);

        SetDestinationIfPossible();
    }

    private void SetDestinationIfPossible()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _destination == null)
            return;

        if (_agent.hasPath || _agent.pathPending)
            return;

        _agent.SetDestination(_destination.position);
    }

    private void TryPlaceOnNavMesh(Vector3 source)
    {
        if (_agent == null || !_agent.enabled)
            return;

        if (_agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(source, out NavMeshHit hit, _navMeshSampleDistance, NavMesh.AllAreas))
            _agent.Warp(hit.position);
    }

    private bool HasReachedDestination()
    {
        if (_destination == null)
            return true;

        Vector3 from = transform.position;
        Vector3 to = _destination.position;
        from.y = 0f;
        to.y = 0f;
        if ((from - to).sqrMagnitude <= _arrivedDistance * _arrivedDistance)
            return true;

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return false;

        return !_agent.pathPending && _agent.remainingDistance <= Mathf.Max(_arrivedDistance, _agent.stoppingDistance);
    }

    private void CompleteArrival()
    {
        if (!_isTraveling)
            return;

        _isTraveling = false;
        DisableAgent();
        ApplyArrivalPose();
        PlayState(_playWaveOnArrival ? WaveHash : IdleHash);
        Arrived?.Invoke();
    }

    private void ApplyArrivalPose()
    {
        if (_destination == null)
            return;

        if (_parentOnArrival)
        {
            transform.SetParent(_destination, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return;
        }

        transform.SetPositionAndRotation(_destination.position, _destination.rotation);
    }

    private void DisableAgent()
    {
        if (_agent == null)
            return;

        if (_agent.enabled && _agent.isOnNavMesh)
            _agent.ResetPath();

        _agent.enabled = false;
    }

    private void PlayState(int stateHash)
    {
        _poseHash = stateHash;

        if (_animator == null || !_animator.isActiveAndEnabled)
            return;

        if (_animator.runtimeAnimatorController == null)
            return;

        _animator.CrossFade(stateHash, _animationFadeDuration, 0);
    }
}
