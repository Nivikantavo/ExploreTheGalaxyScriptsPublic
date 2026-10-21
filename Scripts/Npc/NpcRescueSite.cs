using System;
using PrimeTween;
using UnityEngine;
using Zenject;

public class NpcRescueSite : MonoBehaviour
{
    private const string CageDoorName = "Cylinder.018";

    public event Action Arrived;

    [SerializeField] private string _saveId;
    [SerializeField] private EnemySpawnPoint _guardSpawnPoint;
    [SerializeField] private NpcWorker _worker;
    [SerializeField] private GameObject _cageVisual;
    [SerializeField] private Transform _cageDoor;
    [SerializeField] private Transform _workPoint;
    [SerializeField] private NpcStaffGate[] _stations;
    [SerializeField] private Sprite _workerIcon;
    [SerializeField] private float _cageOpenDuration = 1f;
    [SerializeField] private float _cageOpenAngleY = 120f;

    private bool _appliedFromSave;
    private bool _hasArrived;
    private bool _released;
    private bool _subscribedToGuard;
    private bool _subscribedToWorker;
    private Vector3 _cageDoorClosedEuler;
    private bool _cageDoorClosedEulerCached;
    private Tween _cageOpenTween;
    private NpcRescueRegistry _registry;

    public bool HasArrived => _hasArrived;
    public NpcWorker Worker => _worker;

    [Inject]
    private void Construct(NpcRescueRegistry registry)
    {
        _registry = registry;
        _registry?.Register(this);
    }

    private void OnValidate()
    {
        AssignSaveIdIfEmpty();
        if (_cageDoor == null)
            _cageDoor = FindChildByName(_cageVisual != null ? _cageVisual.transform : transform, CageDoorName);
    }

    private void Awake()
    {
        SubscribeWorker();
        CacheCageDoorClosedEuler();
        BindStationRequirements();
    }

    private void Start()
    {
        if (_appliedFromSave)
            return;

        BeginFromWorldState();
    }

    private void OnEnable()
    {
        if (!_released)
            return;

        if (_hasArrived)
        {
            OpenCageImmediate();
            _worker?.SnapTo(_workPoint);
            return;
        }

        if (_worker != null && _worker.IsTraveling)
        {
            OpenCageImmediate();
            return;
        }

        if (_cageOpenTween.isAlive)
            return;

        if (_worker != null && _workPoint != null)
            OpenCageThenRun();
    }

    private void OnDestroy()
    {
        StopCageOpenTween();
        UnsubscribeGuard();
        UnsubscribeWorker();
        _registry?.Unregister(this);
    }

    public string GetSaveId()
    {
        if (!string.IsNullOrEmpty(_saveId))
            return _saveId;

        HexMapPlacedObject placed = GetComponent<HexMapPlacedObject>()
            ?? GetComponentInParent<HexMapPlacedObject>(true);
        IslandProgressHandler island = GetComponentInParent<IslandProgressHandler>(true);
        if (placed != null && island != null)
            return $"{island.IslandIndex}:{placed.Q}:{placed.R}:NpcWorker";

        return gameObject.name;
    }

    public bool AssignSaveIdIfEmpty()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(_saveId) || Application.isPlaying)
            return false;

        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            return false;

        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
            return false;

        _saveId = Guid.NewGuid().ToString("N");
        return true;
#else
        return false;
#endif
    }

    public void ApplySave(bool hasArrived)
    {
        _appliedFromSave = true;

        if (hasArrived)
        {
            CompleteImmediately();
            return;
        }

        BeginFromWorldState();
    }

    private void BeginFromWorldState()
    {
        if (_guardSpawnPoint != null && _guardSpawnPoint.IsCleared)
        {
            ReleaseAndRun();
            return;
        }

        SubscribeGuard();
        _worker?.SitInCage();
    }

    private void SubscribeGuard()
    {
        if (_subscribedToGuard || _guardSpawnPoint == null)
            return;

        _guardSpawnPoint.Cleared += OnGuardCleared;
        _subscribedToGuard = true;
    }

    private void UnsubscribeGuard()
    {
        if (!_subscribedToGuard || _guardSpawnPoint == null)
            return;

        _guardSpawnPoint.Cleared -= OnGuardCleared;
        _subscribedToGuard = false;
    }

    private void SubscribeWorker()
    {
        if (_subscribedToWorker || _worker == null)
            return;

        _worker.Arrived += OnWorkerArrived;
        _subscribedToWorker = true;
    }

    private void UnsubscribeWorker()
    {
        if (!_subscribedToWorker || _worker == null)
            return;

        _worker.Arrived -= OnWorkerArrived;
        _subscribedToWorker = false;
    }

    private void OnGuardCleared()
    {
        UnsubscribeGuard();
        ReleaseAndRun();
    }

    private void ReleaseAndRun()
    {
        if (_hasArrived || _released)
            return;

        _released = true;

        if (_worker != null && _workPoint != null)
        {
            OpenCageThenRun();
            return;
        }

        OpenCageImmediate();
        MarkArrived();
    }

    private void CompleteImmediately()
    {
        if (_hasArrived)
            return;

        _released = true;
        OpenCageImmediate();
        _worker?.SnapTo(_workPoint);
        MarkArrived(notify: false);
    }

    private void OnWorkerArrived()
    {
        MarkArrived();
    }

    private void MarkArrived(bool notify = true)
    {
        if (_hasArrived)
            return;

        _hasArrived = true;
        StaffStations();
        _registry?.NotifyChanged();

        if (notify)
            Arrived?.Invoke();
    }

    private void OpenCageThenRun()
    {
        Transform door = ResolveCageDoor();
        if (door == null || _cageOpenDuration <= 0f)
        {
            OpenCageImmediate();
            _worker.RunTo(_workPoint);
            return;
        }

        CacheCageDoorClosedEuler();
        StopCageOpenTween();

        _cageOpenTween = Tween.LocalEulerAngles(
                door,
                _cageDoorClosedEuler,
                GetOpenedEuler(),
                _cageOpenDuration,
                Ease.InOutSine)
            .OnComplete(this, site => site._worker.RunTo(site._workPoint));
    }

    private void OpenCageImmediate()
    {
        Transform door = ResolveCageDoor();
        if (door == null)
            return;

        CacheCageDoorClosedEuler();
        StopCageOpenTween();
        door.localEulerAngles = GetOpenedEuler();
    }

    private Vector3 GetOpenedEuler()
    {
        return _cageDoorClosedEuler + new Vector3(0f, _cageOpenAngleY, 0f);
    }

    private void CacheCageDoorClosedEuler()
    {
        if (_cageDoorClosedEulerCached)
            return;

        Transform door = ResolveCageDoor();
        if (door == null)
            return;

        _cageDoorClosedEuler = door.localEulerAngles;
        _cageDoorClosedEulerCached = true;
    }

    private Transform ResolveCageDoor()
    {
        if (_cageDoor != null)
            return _cageDoor;

        Transform searchRoot = _cageVisual != null ? _cageVisual.transform : transform;
        _cageDoor = FindChildByName(searchRoot, CageDoorName);
        return _cageDoor;
    }

    private void StopCageOpenTween()
    {
        if (_cageOpenTween.isAlive)
            _cageOpenTween.Stop();
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent == null)
            return null;

        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private void BindStationRequirements()
    {
        if (_stations == null)
            return;

        for (int i = 0; i < _stations.Length; i++)
            _stations[i]?.BindRequiredWorker(_workerIcon);
    }

    private void StaffStations()
    {
        if (_stations == null)
            return;

        for (int i = 0; i < _stations.Length; i++)
            _stations[i]?.SetStaffed(true);
    }
}
