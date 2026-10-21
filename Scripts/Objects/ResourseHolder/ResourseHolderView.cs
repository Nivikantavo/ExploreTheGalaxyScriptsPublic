using System;
using System.Collections;
using UnityEngine;

public class ResourseHolderView : MonoBehaviour, IHitAnimated, IReparable, IResourcesTransition, IResourceKeeper
{
    public event Action OnHit;
    public event Action OnRepairCompleted;
    public event Action<ResourcesTransitionViewData> OnResourcesTransition;
    public event Action<LowLevelHitData> OnHitLevelLow;
    public ResourceType ResourseType => _resourseHolder.ResourseType;
    public InstrumentType InstrumentType => _resourseHolder.InstrumentType;
    public ResourseHolder ResourseHolder => _resourseHolder;
    public int MinInstrumentLevel => _resourseHolder.MinInstrumentLevel;
    public bool IsOneTime => _isOneTime;
    public bool IsHarvested => _isHarvested;

    [SerializeField] private GameObject[] _breakableParts;
    [SerializeField] private float _minBreakableScale = 0.2f;
    [SerializeField] private float _maxBreakableScale = 1f;
    [SerializeField] private ResourseConfig _resourseConfig;
    [SerializeField] private CapsuleCollider _collider;
    [SerializeField, Tooltip("Уменьшать объект в скейле при добыче. Если выключено — части скрываются (разрушение).")]
    private bool _shrinkOnHarvest;
    [SerializeField, Tooltip("Ресурс можно получить только один раз. Состояние сохраняется между сессиями.")]
    private bool _isOneTime;
    [SerializeField, Tooltip("Стабильный id для сохранения. Если пусто — генерируется для инстанса в сцене.")]
    private string _saveId;

    private ResourseHolder _resourseHolder;
    private Health _health => _resourseHolder.Health;
    private Coroutine _repairCoroutine;
    private LowLevelHitData _lowLevelHitData;
    private bool _isHarvested;
    private bool _repairPending;
    private float _repairReadyAt;

    private void Awake()
    {
        _resourseHolder = new ResourseHolder(_resourseConfig.MaxResourseCount, _resourseConfig.MaxHealth, _resourseConfig.ResourseType, _resourseConfig.InstrumentType, _resourseConfig.MinInstrumentLevel);
        _resourseHolder.OnResourceChanged += UpdateBreakableParts;

        _lowLevelHitData = new LowLevelHitData(MinInstrumentLevel, InstrumentType, transform.position);
    }

    private void OnEnable()
    {
        if (_resourseHolder == null)
            return;

        if (_repairPending)
        {
            ResumeRepair();
            return;
        }

        if (_shrinkOnHarvest)
            ResetBreakablePartsScale();
    }

    private void OnDisable()
    {
        if (_repairCoroutine == null)
            return;

        StopCoroutine(_repairCoroutine);
        _repairCoroutine = null;
    }

    private void OnValidate()
    {
        _minBreakableScale = Mathf.Max(0f, _minBreakableScale);
        _maxBreakableScale = Mathf.Max(_minBreakableScale, _maxBreakableScale);
        AssignSaveIdIfEmpty();
    }

    private void OnDestroy()
    {
        if (_resourseHolder != null)
            _resourseHolder.OnResourceChanged -= UpdateBreakableParts;

        if (_repairCoroutine != null)
            StopCoroutine(_repairCoroutine);
    }

    public string GetSaveId()
    {
        if (!string.IsNullOrEmpty(_saveId))
            return _saveId;

        HexMapPlacedObject placed = GetComponent<HexMapPlacedObject>()
            ?? GetComponentInParent<HexMapPlacedObject>(true);
        IslandProgressHandler island = GetComponentInParent<IslandProgressHandler>(true);
        if (placed != null && island != null && _resourseConfig != null)
            return $"{island.IslandIndex}:{placed.Q}:{placed.R}:{_resourseConfig.ResourseType}";

        return gameObject.name;
    }

    public bool AssignSaveIdIfEmpty()
    {
#if UNITY_EDITOR
        if (!_isOneTime || !string.IsNullOrEmpty(_saveId) || Application.isPlaying)
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

    public void ApplyHarvestedFromSave()
    {
        MarkHarvested();
    }

    public int KnockOutResources(Instrument instrument, Transform pickupTarget)
    {
        if (_isHarvested || _health.CurrentHealth <= 0)
            return 0;

        OnHit?.Invoke();
        if (instrument.Type != _resourseHolder.InstrumentType)
            return 0;

        if (instrument.Level < _resourseHolder.MinInstrumentLevel)
        {
            OnHitLevelLow?.Invoke(_lowLevelHitData);
            return 0;
        }

        int resourcesAmount = _resourseHolder.GetResource(instrument.DamagePerHit);

        if (resourcesAmount > 0)
        {
            OnResourcesTransition?.Invoke(new ResourcesTransitionViewData(
                count: resourcesAmount,
                config: _resourseConfig,
                startPosition: transform.position,
                pickupTarget: pickupTarget
            ));
        }

        if (_isOneTime && resourcesAmount > 0)
        {
            MarkHarvested();
            return resourcesAmount;
        }

        StartRepairCoroutine();
        return resourcesAmount;
    }

    private void MarkHarvested()
    {
        _isHarvested = true;
        _repairPending = false;

        if (_repairCoroutine != null)
        {
            StopCoroutine(_repairCoroutine);
            _repairCoroutine = null;
        }

        if (_collider != null)
            _collider.enabled = false;

        gameObject.SetActive(false);
    }

    private void UpdateBreakableParts()
    {
        if (_breakableParts == null || _breakableParts.Length == 0)
            return;

        float ratio = _health.MaxHealth > 0
            ? Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth)
            : 0f;

        if (_shrinkOnHarvest)
        {
            float scale = Mathf.Lerp(_minBreakableScale, _maxBreakableScale, ratio);
            for (int i = 0; i < _breakableParts.Length; i++)
            {
                if (_breakableParts[i] != null)
                    _breakableParts[i].transform.localScale = Vector3.one * scale;
            }
        }
        else
        {
            int visibleCount = Mathf.CeilToInt(ratio * _breakableParts.Length);
            int hiddenCount = _breakableParts.Length - visibleCount;

            for (int i = 0; i < _breakableParts.Length; i++)
            {
                if (_breakableParts[i] != null)
                    SetBreakablePartVisible(_breakableParts[i], i >= hiddenCount);
            }
        }

        if (_collider != null)
            _collider.enabled = _health.CurrentHealth > 0 && !_isHarvested;
    }

    private void SetBreakablePartVisible(GameObject part, bool visible)
    {
        if (part == gameObject)
        {
            Renderer[] renderers = part.GetComponents<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;
            return;
        }

        part.SetActive(visible);
    }

    private void ResetBreakablePartsScale()
    {
        if (_breakableParts == null)
            return;

        for (int i = 0; i < _breakableParts.Length; i++)
        {
            if (_breakableParts[i] != null)
                _breakableParts[i].transform.localScale = Vector3.one * _maxBreakableScale;
        }
    }

    private void StartRepairCoroutine()
    {
        _repairPending = true;
        _repairReadyAt = Time.time + Mathf.Max(0f, _resourseConfig.RepairDuration);

        if (!isActiveAndEnabled)
            return;

        ResumeRepair();
    }

    private void ResumeRepair()
    {
        if (_repairCoroutine != null)
            StopCoroutine(_repairCoroutine);

        _repairCoroutine = StartCoroutine(ReparingCoroutine());
    }

    private IEnumerator ReparingCoroutine()
    {
        float remaining = _repairReadyAt - Time.time;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
        else
            yield return null;

        FinishRepair();
    }

    private void FinishRepair()
    {
        _repairPending = false;
        _repairCoroutine = null;

        if (_resourseHolder == null)
            return;

        _resourseHolder.Repair();
        OnRepairCompleted?.Invoke();
    }
}
