using System;
using ECM2;
using PrimeTween;
using UnityEngine;
using Zenject;

public class ShipTransport : MonoBehaviour
{
    public event Action VoyageCompleted;

    [SerializeField] private Transform _ship;
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private Transform _passengerPoint;
    [SerializeField] private Transform _dockExitStart;
    [SerializeField] private Transform _dockExitEnd;
    [SerializeField] private ApproachUIShower _approachUIShower;
    [SerializeField] private Collider _boardTrigger;
    [SerializeField] private ShipSailView _sailView;
    [SerializeField] private CallPoint _callStart;
    [SerializeField] private CallPoint _callEnd;
    [SerializeField] private ResourceType _sailResource = ResourceType.Coin;
    [SerializeField, Min(1)] private int _sailCost = 150;
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _minSegmentDuration = 0.2f;
    [SerializeField, Tooltip("Local axis that points to the bow. Ship_Small is authored along +X.")]
    private Vector3 _localBowAxis = Vector3.right;

    [Serializable]
    private struct CallPoint
    {
        public ApproachUIShower Shower;
        public Collider Trigger;
        public ShipCallView View;
    }

    private ResourceTypeViews _resourceTypeViews;
    private Player _player;
    private Character _character;
    private Sequence _voyage;
    private bool _isActive;
    private bool _atDestination;
    private bool _voyaging;
    private bool _viewInitialized;
    private bool _passengerAttached;
    private RigidbodyInterpolation _savedInterpolation = RigidbodyInterpolation.Interpolate;

    public bool AtDestination => _atDestination;
    public bool IsActive => _isActive;

    [Inject]
    private void Construct(ResourceTypeViews resourceTypeViews)
    {
        _resourceTypeViews = resourceTypeViews;
    }

    private void Awake()
    {
        if (_approachUIShower == null)
            _approachUIShower = GetComponentInChildren<ApproachUIShower>(true);

        if (_sailView == null)
            _sailView = GetComponentInChildren<ShipSailView>(true);

        if (_boardTrigger == null && _approachUIShower != null)
            _boardTrigger = _approachUIShower.GetComponent<Collider>();

        SetBoardingEnabled(false);
        SetCallPointEnabled(_callStart, false);
        SetCallPointEnabled(_callEnd, false);
    }

    private void OnEnable()
    {
        if (_sailView != null)
            _sailView.SailClicked += OnSailClicked;

        SubscribeCallPoint(_callStart);
        SubscribeCallPoint(_callEnd);
    }

    private void OnDisable()
    {
        if (_sailView != null)
            _sailView.SailClicked -= OnSailClicked;

        UnsubscribeCallPoint(_callStart);
        UnsubscribeCallPoint(_callEnd);

        StopVoyage();
        ReleasePassenger(applyDockExit: false);
    }

    private void LateUpdate()
    {
        if (_voyaging && _passengerAttached)
            SnapPassengerToShip();
    }

    public void Activate()
    {
        _isActive = true;
        EnsureViewInitialized();
        RefreshInteractionUi();
    }

    public void ApplyDock(bool atDestination)
    {
        _atDestination = atDestination;
        SnapShipToDock();
        Activate();
    }

    private void EnsureViewInitialized()
    {
        if (_viewInitialized || _sailView == null)
            return;

        Sprite icon = null;
        if (_resourceTypeViews != null &&
            _resourceTypeViews.Icons != null &&
            _resourceTypeViews.Icons.TryGetValue(_sailResource, out Sprite typedIcon))
        {
            icon = typedIcon;
        }

        _sailView.Initialize(_sailCost, icon);
        _viewInitialized = true;
    }

    private void OnSailClicked()
    {
        if (!_isActive || _voyaging)
            return;

        Player player = ResolvePlayer();
        if (player == null || !player.TrySpendResource(_sailResource, _sailCost))
            return;

        StartVoyage(player);
    }

    private void OnCallClicked()
    {
        if (!_isActive || _voyaging)
            return;

        StartVoyage(passenger: null);
    }

    private void StartVoyage(Player passenger)
    {
        if (_ship == null || _waypoints == null || _waypoints.Length == 0)
            return;

        _voyaging = true;
        RefreshInteractionUi();
        _sailView?.Hide();
        _callStart.View?.Hide();
        _callEnd.View?.Hide();

        if (passenger != null)
        {
            _player = passenger;
            _character = passenger.GetComponent<Character>();
            AttachPassenger();
        }

        Transform[] path = BuildPath();
        _voyage = Sequence.Create();
        Vector3 current = _ship.position;

        for (int i = 0; i < path.Length; i++)
        {
            Transform waypoint = path[i];
            if (waypoint == null)
                continue;

            Vector3 target = waypoint.position;
            Vector3 delta = target - current;
            delta.y = 0f;
            float distance = delta.magnitude;
            float duration = Mathf.Max(_minSegmentDuration, distance / Mathf.Max(0.01f, _moveSpeed));
            Quaternion rotation = distance > 0.001f
                ? GetTravelRotation(delta.normalized)
                : waypoint.rotation;

            _voyage.Chain(Tween.Position(_ship, target, duration, Ease.InOutSine));
            _voyage.Group(Tween.Rotation(_ship, rotation, duration, Ease.InOutSine));
            current = target;
        }

        _voyage.ChainCallback(this, transport => transport.CompleteVoyage());
    }

    private Transform[] BuildPath()
    {
        Transform[] fullPath;
        if (_atDestination)
        {
            fullPath = new Transform[_waypoints.Length];
            for (int i = 0; i < _waypoints.Length; i++)
                fullPath[i] = _waypoints[_waypoints.Length - 1 - i];
        }
        else
        {
            fullPath = _waypoints;
        }

        if (fullPath.Length <= 1)
            return fullPath;

        var path = new Transform[fullPath.Length - 1];
        for (int i = 0; i < path.Length; i++)
            path[i] = fullPath[i + 1];

        return path;
    }

    private Quaternion GetTravelRotation(Vector3 worldDirection)
    {
        Vector3 bow = _localBowAxis.sqrMagnitude > 0.0001f
            ? _localBowAxis.normalized
            : Vector3.forward;

        Quaternion bowToForward = Quaternion.Inverse(Quaternion.LookRotation(bow, Vector3.up));
        return Quaternion.LookRotation(worldDirection, Vector3.up) * bowToForward;
    }

    private void CompleteVoyage()
    {
        _voyage = default;
        _atDestination = !_atDestination;
        _voyaging = false;
        SnapShipToDock();
        ReleasePassenger(applyDockExit: true);
        RefreshInteractionUi();
        VoyageCompleted?.Invoke();
    }

    private void SnapShipToDock()
    {
        Transform dock = GetCurrentDock();
        if (_ship == null || dock == null)
            return;

        Vector3 heading = GetDockHeading();
        Quaternion rotation = heading.sqrMagnitude > 0.001f
            ? GetTravelRotation(heading)
            : dock.rotation;

        _ship.SetPositionAndRotation(dock.position, rotation);
    }

    private Vector3 GetDockHeading()
    {
        if (_waypoints == null || _waypoints.Length < 2)
            return Vector3.zero;

        Transform from;
        Transform to;
        if (_atDestination)
        {
            from = _waypoints[_waypoints.Length - 2];
            to = _waypoints[_waypoints.Length - 1];
        }
        else
        {
            from = _waypoints[0];
            to = _waypoints[1];
        }

        if (from == null || to == null)
            return Vector3.zero;

        Vector3 heading = to.position - from.position;
        heading.y = 0f;
        return heading.sqrMagnitude > 0.001f ? heading.normalized : Vector3.zero;
    }

    private Transform GetCurrentDock()
    {
        if (_waypoints == null || _waypoints.Length == 0)
            return null;

        return _atDestination ? _waypoints[_waypoints.Length - 1] : _waypoints[0];
    }

    private Transform GetCurrentExit()
    {
        if (_atDestination)
            return _dockExitEnd != null ? _dockExitEnd : GetCurrentDock();

        return _dockExitStart != null ? _dockExitStart : GetCurrentDock();
    }

    private void AttachPassenger()
    {
        if (_player == null)
            return;

        _player.SetGameplayControlEnabled(false);

        if (_character != null)
        {
            CharacterMovement movement = _character.characterMovement;
            if (movement != null)
            {
                _savedInterpolation = movement.interpolation;
                movement.interpolation = RigidbodyInterpolation.None;
            }

            _character.Pause(true);
        }

        _passengerAttached = true;
        SnapPassengerToShip();
    }

    private void SnapPassengerToShip()
    {
        Transform attach = _passengerPoint != null ? _passengerPoint : _ship;
        if (attach == null)
            return;

        Quaternion facing = GetPassengerFacing(attach);
        if (_character != null)
        {
            _character.SetPosition(attach.position);
            _character.SetRotation(facing);
            return;
        }

        if (_player != null)
            _player.transform.SetPositionAndRotation(attach.position, facing);
    }

    private Quaternion GetPassengerFacing(Transform attach)
    {
        if (_ship == null)
            return attach.rotation;

        Vector3 bow = _ship.TransformDirection(
            _localBowAxis.sqrMagnitude > 0.0001f ? _localBowAxis : Vector3.forward);
        bow.y = 0f;
        if (bow.sqrMagnitude < 0.001f)
            return attach.rotation;

        return Quaternion.LookRotation(bow.normalized, Vector3.up);
    }

    private void ReleasePassenger(bool applyDockExit)
    {
        if (_player == null)
            return;

        _passengerAttached = false;
        Transform exit = applyDockExit ? GetCurrentExit() : null;

        if (_character != null)
        {
            CharacterMovement movement = _character.characterMovement;
            if (movement != null)
                movement.interpolation = _savedInterpolation;

            _character.Pause(false);

            if (exit != null)
            {
                _character.TeleportPosition(exit.position, interpolating: true, updateGround: true);
                _character.SetRotation(exit.rotation);
            }
        }
        else if (exit != null)
        {
            _player.transform.SetPositionAndRotation(exit.position, exit.rotation);
        }

        _player.SetGameplayControlEnabled(true);
        _player = null;
        _character = null;
    }

    private void StopVoyage()
    {
        if (_voyage.isAlive)
            _voyage.Stop();

        _voyage = default;
        _voyaging = false;
    }

    private void RefreshInteractionUi()
    {
        bool idle = _isActive && !_voyaging;
        SetBoardingEnabled(idle);
        SetCallPointEnabled(_callStart, idle && _atDestination);
        SetCallPointEnabled(_callEnd, idle && !_atDestination);
    }

    private void SetBoardingEnabled(bool enabled)
    {
        if (_approachUIShower != null)
            _approachUIShower.enabled = enabled;

        if (_boardTrigger != null)
            _boardTrigger.enabled = enabled;

        if (!enabled)
            _sailView?.Hide();
        else
            _approachUIShower?.TryShowIfPlayerInside();
    }

    private static void SetCallPointEnabled(CallPoint point, bool enabled)
    {
        if (point.Shower != null)
            point.Shower.enabled = enabled;

        if (point.Trigger != null)
            point.Trigger.enabled = enabled;

        if (!enabled)
            point.View?.Hide();
        else
            point.Shower?.TryShowIfPlayerInside();
    }

    private void SubscribeCallPoint(CallPoint point)
    {
        if (point.View != null)
            point.View.CallClicked += OnCallClicked;
    }

    private void UnsubscribeCallPoint(CallPoint point)
    {
        if (point.View != null)
            point.View.CallClicked -= OnCallClicked;
    }

    private Player ResolvePlayer()
    {
        if (_player != null)
            return _player;

        return FindFirstObjectByType<Player>();
    }
}
