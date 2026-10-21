using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpeningObject : ResourcePaymentOpener
{
    public GameObject OpenedObject => _openedObject;
    public bool IsCompleted => _isCompleted;

    [SerializeField] private GameObject _openedObject;
    [SerializeField] private GameObject _additiveOpenedObject;

    private bool _isCompleted;

    protected override Transform PickupTarget =>
        _openedObject != null ? _openedObject.transform : transform;

    protected override void Awake()
    {
        base.Awake();
        OppeningObjectTransform = transform;
        if (!_isCompleted && _openedObject != null)
            _openedObject.SetActive(false);
    }

    public OpeningObject GetNextStep()
    {
        if (_additiveOpenedObject == null)
            return null;

        if (_additiveOpenedObject.TryGetComponent<OpeningObject>(out var opener))
            return opener;

        return _additiveOpenedObject.GetComponentInChildren<OpeningObject>(true);
    }

    protected override void NotifyOpened()
    {
        // Listeners may deactivate this object inside OnOpened. That stops
        // CompleteOpeningCoroutine before ApplyCompletedState, so the built
        // flag has to be set before the event.
        _isCompleted = true;
        base.NotifyOpened();
    }

    protected override IEnumerator CompleteOpeningCoroutine()
    {
        yield return new WaitForSeconds(OpenDelay);
        ApplyCompletedState();
    }

    public void ApplyCompletedFromSave()
    {
        ApplyCompletedState();
        ResetPaymentState();
    }

    public void ApplyInProgressFromSave(ResourceAmountSave[] remains, float accumulatedTransferTime)
    {
        _isCompleted = false;
        ApplyRemainsToPay(remains);
        _accumulatedTransferTime = accumulatedTransferTime;
        gameObject.SetActive(true);
        RaiseValueChanged();
    }

    public void ApplyLockedFromSave()
    {
        _isCompleted = false;
        ResetPaymentState();

        if (_openedObject != null)
            _openedObject.SetActive(false);

        gameObject.SetActive(false);
    }

    protected override bool IsPaymentComplete() => IsCompleted || IsFilled;

    public void DisableBecauseAlreadyOpened()
    {
        _isCompleted = true;
        ResetPaymentState();
        gameObject.SetActive(false);
    }

    private void ApplyCompletedState()
    {
        _isCompleted = true;

        if (_openedObject != null)
            _openedObject.SetActive(true);

        if (_additiveOpenedObject != null)
            _additiveOpenedObject.SetActive(true);

        gameObject.SetActive(false);
    }
}
