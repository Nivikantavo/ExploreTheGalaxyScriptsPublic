using System;
using PrimeTween;
using UnityEngine;

public class ShipSite : MonoBehaviour
{
    public event Action Changed;

    [SerializeField] private string _saveId;
    [SerializeField] private NpcRescueSite _captainSite;
    [SerializeField] private NpcWorker _captain;
    [SerializeField] private OpeningObject _constructionOpener;
    [SerializeField] private GameObject _shipVisual;
    [SerializeField] private Transform _boardPoint;
    [SerializeField] private ShipTransport _transport;
    [SerializeField] private float _appearDuration = 0.5f;

    private bool _appliedFromSave;
    private bool _waitingForBoard;
    private bool _subscribedToCaptainSite;
    private bool _subscribedToCaptain;
    private bool _subscribedToOpener;
    private Vector3 _shipShownScale = Vector3.one;
    private bool _shipScaleCached;

    public bool IsBuilt => _constructionOpener != null && _constructionOpener.IsCompleted;
    public bool AtDestination => _transport != null && _transport.AtDestination;

    private void OnValidate()
    {
        AssignSaveIdIfEmpty();
        if (_captain == null && _captainSite != null)
            _captain = _captainSite.Worker;
        if (_transport == null)
            _transport = GetComponent<ShipTransport>();
        if (_constructionOpener == null)
            _constructionOpener = GetComponentInChildren<OpeningObject>(true);
        if (_shipVisual == null && _constructionOpener != null)
            _shipVisual = _constructionOpener.OpenedObject;
    }

    private void Awake()
    {
        CacheShipScale();
        SubscribeOpener();
        HideUntilReady();
    }

    private void Start()
    {
        if (_appliedFromSave)
            return;

        BeginFromWorldState();
    }

    private void OnDestroy()
    {
        UnsubscribeCaptainSite();
        UnsubscribeCaptain();
        UnsubscribeOpener();
    }

    public string GetSaveId()
    {
        if (!string.IsNullOrEmpty(_saveId))
            return _saveId;

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

    public ShipSaveData CreateSaveData()
    {
        if (string.IsNullOrEmpty(GetSaveId()) || !IsBuilt)
            return null;

        return new ShipSaveData
        {
            SaveId = GetSaveId(),
            AtDestination = AtDestination
        };
    }

    public void ApplySave(ShipSaveData saveData)
    {
        _appliedFromSave = true;

        bool built = saveData != null || IsBuilt;
        if (!built)
        {
            BeginFromWorldState();
            return;
        }

        if (!IsBuilt)
            _constructionOpener?.ApplyCompletedFromSave();

        CompleteImmediately(saveData != null && saveData.AtDestination);
    }

    private void BeginFromWorldState()
    {
        HideUntilReady();

        if (IsBuilt)
        {
            CompleteImmediately(atDestination: false);
            return;
        }

        if (_captainSite != null && _captainSite.HasArrived)
        {
            ShowConstructionOpener();
            return;
        }

        SubscribeCaptainSite();
    }

    private void CompleteImmediately(bool atDestination)
    {
        ShowShip(playAppear: false);
        HideConstructionOpener();
        BoardCaptainImmediate();
        _transport?.ApplyDock(atDestination);
        Changed?.Invoke();
    }

    private void HideUntilReady()
    {
        if (_constructionOpener != null && !_constructionOpener.IsCompleted)
            _constructionOpener.gameObject.SetActive(false);

        if (_shipVisual != null && !IsBuilt)
            _shipVisual.SetActive(false);

        if (_transport != null && !IsBuilt)
            _transport.enabled = true;
    }

    private void SubscribeCaptainSite()
    {
        if (_subscribedToCaptainSite || _captainSite == null)
            return;

        _captainSite.Arrived += OnCaptainArrivedAtConstruction;
        _subscribedToCaptainSite = true;
    }

    private void UnsubscribeCaptainSite()
    {
        if (!_subscribedToCaptainSite || _captainSite == null)
            return;

        _captainSite.Arrived -= OnCaptainArrivedAtConstruction;
        _subscribedToCaptainSite = false;
    }

    private void SubscribeCaptain()
    {
        if (_subscribedToCaptain || _captain == null)
            return;

        _captain.Arrived += OnCaptainArrivedAtShip;
        _subscribedToCaptain = true;
    }

    private void UnsubscribeCaptain()
    {
        if (!_subscribedToCaptain || _captain == null)
            return;

        _captain.Arrived -= OnCaptainArrivedAtShip;
        _subscribedToCaptain = false;
    }

    private void SubscribeOpener()
    {
        if (_subscribedToOpener || _constructionOpener == null)
            return;

        _constructionOpener.OnOpened += OnConstructionOpened;
        _constructionOpener.ValueChanged += OnConstructionValueChanged;
        _subscribedToOpener = true;
    }

    private void UnsubscribeOpener()
    {
        if (!_subscribedToOpener || _constructionOpener == null)
            return;

        _constructionOpener.OnOpened -= OnConstructionOpened;
        _constructionOpener.ValueChanged -= OnConstructionValueChanged;
        _subscribedToOpener = false;
    }

    private void OnCaptainArrivedAtConstruction()
    {
        UnsubscribeCaptainSite();
        ShowConstructionOpener();
        Changed?.Invoke();
    }

    private void ShowConstructionOpener()
    {
        if (_constructionOpener == null || _constructionOpener.IsCompleted)
            return;

        _constructionOpener.gameObject.SetActive(true);
    }

    private void HideConstructionOpener()
    {
        if (_constructionOpener == null)
            return;

        _constructionOpener.gameObject.SetActive(false);
    }

    private void OnConstructionValueChanged(ResourcePaymentOpener _)
    {
        Changed?.Invoke();
    }

    private void OnConstructionOpened(ResourcePaymentOpener _)
    {
        ShowShip(playAppear: true);
        HideConstructionOpener();
        SendCaptainToShip();
        Changed?.Invoke();
    }

    private void ShowShip(bool playAppear)
    {
        if (_shipVisual == null)
            return;

        CacheShipScale();
        _shipVisual.SetActive(true);

        if (!playAppear || _appearDuration <= 0f)
        {
            _shipVisual.transform.localScale = _shipShownScale;
            return;
        }

        _shipVisual.transform.localScale = Vector3.zero;
        Tween.Scale(_shipVisual.transform, _shipShownScale, _appearDuration, Ease.InOutSine);
    }

    private void SendCaptainToShip()
    {
        if (_captain == null)
        {
            BoardCaptainImmediate();
            _transport?.Activate();
            return;
        }

        _waitingForBoard = true;
        SubscribeCaptain();
        _captain.RunTo(_boardPoint, parentOnArrival: true, playWaveOnArrival: false);
    }

    private void OnCaptainArrivedAtShip()
    {
        if (!_waitingForBoard)
            return;

        _waitingForBoard = false;
        UnsubscribeCaptain();
        BoardCaptainImmediate();
        _transport?.Activate();
        Changed?.Invoke();
    }

    private void BoardCaptainImmediate()
    {
        _waitingForBoard = false;
        _captain?.SnapTo(_boardPoint, parentToDestination: true);
    }

    private void CacheShipScale()
    {
        if (_shipScaleCached || _shipVisual == null)
            return;

        Vector3 scale = _shipVisual.transform.localScale;
        if (scale.sqrMagnitude > 0.0001f)
            _shipShownScale = scale;

        _shipScaleCached = true;
    }
}
