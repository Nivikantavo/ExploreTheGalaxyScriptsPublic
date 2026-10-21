using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using Zenject;

public abstract class ResourcePaymentOpener : MonoBehaviour, IResourcesTransition
{
    public Action<ResourcePaymentOpener> OnOpened;
    public Action<ResourcePaymentOpener> ValueChanged;
    public event Action<ResourcesTransitionViewData> OnResourcesTransition;
    public Transform OppeningObjectTransform {get; protected set;}
    public IReadOnlyDictionary<ResourceType, int> RemainsToPay => _remainsToPay;
    public bool IsFilled => _remainsToPay.Count > 0 && _remainsToPay.All(pair => pair.Value <= 0);
    public bool HasPaymentProgress => _remainsToPay.Values.Any(amount => amount > 0)
                                      && _remainsToPay.Any(pair => pair.Value < GetOriginalPrice(pair.Key));

    [SerializeField] private string _goalOpenerId;
    [SerializeField] private SerializedDictionary<ResourceType, int> _price;
    [SerializeField] private float _transferTime = 2f;
    [SerializeField, Min(1)] private int _maxTransferIterationsPerResource = 20;
    [SerializeField] private float _openDelay = 0.2f;

    public string GoalOpenerId => _goalOpenerId;

    protected Dictionary<ResourceType, int> _remainsToPay = new();
    protected float _accumulatedTransferTime;
    protected float OpenDelay => _openDelay;
    private bool _paymentInitialized;

    private AllResoursesConfig _resourseConfig;
    private GoalOpenerRegistry _goalOpenerRegistry;
    private Coroutine _paymentCoroutine;

    protected abstract Transform PickupTarget { get; }
    protected abstract IEnumerator CompleteOpeningCoroutine();

    [Inject]
    private void Construct(AllResoursesConfig resourseConfig, GoalOpenerRegistry goalOpenerRegistry)
    {
        _resourseConfig = resourseConfig;
        _goalOpenerRegistry = goalOpenerRegistry;
    }

    protected virtual void Awake()
    {
        EnsurePaymentInitialized();
    }

    protected virtual void Start()
    {
        _goalOpenerRegistry?.Register(this);
    }

    protected virtual void OnDestroy()
    {
        _goalOpenerRegistry?.Unregister(this);
    }

    public bool IsGoalComplete() => IsPaymentComplete();

    protected virtual bool IsPaymentComplete() => IsFilled;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player) && _paymentCoroutine == null)
            _paymentCoroutine = StartCoroutine(PaymentCoroutine(player));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<Player>(out _))
            return;

        if (_paymentCoroutine == null)
            return;

        StopCoroutine(_paymentCoroutine);
        _paymentCoroutine = null;
    }

    private IEnumerator PaymentCoroutine(Player player)
    {
        var resourcesConfigByType = _remainsToPay.Keys.ToDictionary(
            type => type,
            type => _resourseConfig.Resourses.FirstOrDefault(r => r.ResourseType == type));
        int totalIterations = Mathf.Max(1, _maxTransferIterationsPerResource);
        int completedIterations = GetTargetIterations(totalIterations);

        while (!_remainsToPay.All(r => r.Value <= 0))
        {
            bool hasPayableResources = GetPayableResourceTypes(player).Count > 0;
            if (!hasPayableResources)
                break;

            float previousTransferTime = _accumulatedTransferTime;
            bool transferTimeBudgetExhausted = _transferTime > 0f && _accumulatedTransferTime >= _transferTime;

            if (!transferTimeBudgetExhausted && _transferTime > 0f)
                _accumulatedTransferTime += Time.deltaTime;

            int targetIterations = GetTargetIterations(totalIterations);
            int currentIteration = Mathf.Max(completedIterations, targetIterations);
            bool forceRemainingPayment = transferTimeBudgetExhausted || currentIteration >= totalIterations;

            if (!forceRemainingPayment && currentIteration <= completedIterations)
            {
                yield return null;
                continue;
            }

            bool valueChanged = TryTransferResources(
                player,
                resourcesConfigByType,
                totalIterations,
                forceRemainingPayment ? totalIterations : currentIteration,
                forceRemainingPayment,
                out bool duePaymentExists);

            if (valueChanged)
            {
                completedIterations = forceRemainingPayment ? totalIterations : currentIteration;
                NotifyValueChanged();
            }
            else if (!duePaymentExists && !forceRemainingPayment)
            {
                // Iteration advanced, but the schedule has not unlocked a payable batch yet
                // (common when price amount < totalIterations). Keep waiting, do not refund time.
                completedIterations = currentIteration;
            }
            else
            {
                // A due batch exists, but resources could not be spent — exit without burning budget.
                _accumulatedTransferTime = previousTransferTime;
                break;
            }

            if (_transferTime > 0f && _accumulatedTransferTime >= _transferTime && !_remainsToPay.All(r => r.Value <= 0))
            {
                if (!TryTransferResources(
                        player,
                        resourcesConfigByType,
                        totalIterations,
                        totalIterations,
                        forceAllRemaining: true,
                        out _))
                {
                    break;
                }

                NotifyValueChanged();
            }

            yield return null;
        }

        _paymentCoroutine = null;
        NotifyValueChanged();

        if (!_remainsToPay.All(r => r.Value <= 0))
            yield break;

        _accumulatedTransferTime = 0f;
        NotifyOpened();
        yield return CompleteOpeningCoroutine();
    }

    private bool TryTransferResources(
        Player player,
        Dictionary<ResourceType, ResourseConfig> resourcesConfigByType,
        int totalIterations,
        int currentIteration,
        bool forceAllRemaining,
        out bool duePaymentExists)
    {
        bool valueChanged = false;
        duePaymentExists = false;

        foreach (var resourceType in _remainsToPay.Keys.ToList())
        {
            int initialAmount = GetOriginalPrice(resourceType);
            int remains = _remainsToPay[resourceType];
            if (initialAmount <= 0 || remains <= 0)
                continue;

            int plannedBatchAmount;
            if (forceAllRemaining)
            {
                plannedBatchAmount = remains;
            }
            else
            {
                int alreadyPaid = initialAmount - remains;
                int shouldBePaidByNow = currentIteration >= totalIterations
                    ? initialAmount
                    : Mathf.FloorToInt((float)initialAmount * currentIteration / totalIterations);
                plannedBatchAmount = shouldBePaidByNow - alreadyPaid;
            }

            if (plannedBatchAmount <= 0)
                continue;

            duePaymentExists = true;

            int spendAmount = Mathf.Min(plannedBatchAmount, remains, player.GetResourceAmount(resourceType));
            if (spendAmount <= 0 || !player.TrySpendResource(resourceType, spendAmount))
                continue;

            _remainsToPay[resourceType] -= spendAmount;
            valueChanged = true;
            OnResourcesTransition?.Invoke(new ResourcesTransitionViewData(
                count: 1,
                config: resourcesConfigByType[resourceType],
                startPosition: player.transform.position,
                pickupTarget: PickupTarget
            ));
        }

        return valueChanged;
    }

    protected virtual void NotifyValueChanged()
    {
        ValueChanged?.Invoke(this);
        NotifyGoalOpenerChanged();
    }

    protected virtual void NotifyOpened()
    {
        OnOpened?.Invoke(this);
        NotifyGoalOpenerChanged();
    }

    protected void NotifyGoalOpenerChanged() => _goalOpenerRegistry?.NotifyChanged(this);

    private int GetTargetIterations(int totalIterations)
    {
        if (_transferTime <= 0f)
            return totalIterations;

        float progressRatio = Mathf.Clamp01(_accumulatedTransferTime / _transferTime);
        return Mathf.Clamp(Mathf.FloorToInt(progressRatio * totalIterations), 0, totalIterations);
    }

    private List<ResourceType> GetPayableResourceTypes(Player player)
    {
        var payableTypes = new List<ResourceType>();

        foreach (var resource in _remainsToPay)
        {
            if (resource.Value > 0 && player.GetResourceAmount(resource.Key) > 0)
                payableTypes.Add(resource.Key);
        }

        return payableTypes;
    }

    protected void RaiseValueChanged() => NotifyValueChanged();
    protected void RaiseOpened() => NotifyOpened();

    protected void EnsurePaymentInitialized()
    {
        if (_paymentInitialized)
            return;

        _paymentInitialized = true;
        foreach (var price in _price)
        {
            if (!_remainsToPay.ContainsKey(price.Key))
                _remainsToPay.Add(price.Key, price.Value);
        }
    }

    protected void ApplyRemainsToPay(ResourceAmountSave[] remains)
    {
        EnsurePaymentInitialized();
        if (remains == null)
            return;

        foreach (var resource in remains)
        {
            if (_remainsToPay.ContainsKey(resource.Type))
                _remainsToPay[resource.Type] = resource.Amount;
        }
    }

    public float AccumulatedTransferTime => _accumulatedTransferTime;

    public int GetOriginalPrice(ResourceType resourceType)
    {
        return _price.TryGetValue(resourceType, out int amount) ? amount : 0;
    }

    public int GetTotalRequiredAmount()
    {
        int total = 0;
        foreach (var price in _price)
            total += Mathf.Max(0, price.Value);

        return total;
    }

    public int GetTotalPaidAmount()
    {
        int totalRequired = GetTotalRequiredAmount();
        int totalRemaining = 0;

        foreach (var pair in _remainsToPay)
            totalRemaining += Mathf.Max(0, pair.Value);

        return Mathf.Max(0, totalRequired - totalRemaining);
    }

    protected void ResetPaymentState()
    {
        foreach (var resourceType in _remainsToPay.Keys.ToList())
            _remainsToPay[resourceType] = 0;

        _accumulatedTransferTime = 0f;
    }

    protected void RestoreOriginalPaymentState()
    {
        _remainsToPay.Clear();
        foreach (var price in _price)
            _remainsToPay[price.Key] = price.Value;

        _accumulatedTransferTime = 0f;
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (!string.IsNullOrEmpty(_goalOpenerId))
            return;

        _goalOpenerId = gameObject.name;
    }
#endif
}
