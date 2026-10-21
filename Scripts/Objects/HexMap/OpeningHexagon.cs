using System.Collections;
using UnityEngine;

public class OpeningHexagon : ResourcePaymentOpener
{
    public HexTile OpenedHexagon => _openedHexagon;
    public GameObject ObjectToDisable => _objectToDisable;

    [SerializeField] private HexTile _openedHexagon;
    [SerializeField] private GameObject _additiveOpenedObject;
    [SerializeField] private GameObject _objectToDisable;

    protected override Transform PickupTarget =>
        _openedHexagon != null ? _openedHexagon.transform : transform;

    protected override bool IsPaymentComplete() =>
        IsFilled || (_openedHexagon != null && !_openedHexagon.IsLocked);

    protected override void Awake() 
    {
        base.Awake();
        OppeningObjectTransform = _openedHexagon != null ? _openedHexagon.transform : transform;
    }

    protected override void NotifyOpened()
    {
        // Listeners may deactivate this opener inside OnOpened and stop
        // CompleteOpeningCoroutine, so the target has to be disabled first.
        DisableTargetObject();
        base.NotifyOpened();
    }

    protected override IEnumerator CompleteOpeningCoroutine()
    {
        yield return new WaitForSeconds(OpenDelay);
        DisableTargetObject();

        if (_openedHexagon == null)
        {
            gameObject.SetActive(false);
            yield break;
        }

        bool animationEnded = false;
        void OnAnimationEnded() => animationEnded = true;

        _openedHexagon.OnUnlockAnimationEnded += OnAnimationEnded;
        _openedHexagon.Unlock();

        while (!animationEnded && _openedHexagon != null)
            yield return null;

        if (_openedHexagon != null)
            _openedHexagon.OnUnlockAnimationEnded -= OnAnimationEnded;

        // Additive props used to enable in the same frame as Unlock and could embed the player
        // without going through HexTile's collider disable / push-out.
        if (_additiveOpenedObject != null)
        {
            _additiveOpenedObject.SetActive(true);

            var movement = HexUnlockPlayerDepenetration.FindPlayerMovement();
            Collider[] additiveColliders = HexUnlockPlayerDepenetration.CollectSolidColliders(_additiveOpenedObject);
            Vector3 center = _openedHexagon != null
                ? _openedHexagon.transform.position
                : _additiveOpenedObject.transform.position;
            HexUnlockPlayerDepenetration.DepenetrateFromHex(movement, center, additiveColliders);
        }

        gameObject.SetActive(false);
    }

    private bool HasRemainingPayment()
    {
        foreach (var pair in _remainsToPay)
        {
            if (pair.Value > 0)
                return true;
        }

        return false;
    }

    public OpeningHexSaveData CreateSaveData()
    {
        if (_openedHexagon == null || !HasRemainingPayment())
            return null;

        if (!HasPaymentProgress)
            return null;

        return new OpeningHexSaveData
        {
            Q = _openedHexagon.Q,
            R = _openedHexagon.R,
            RemainsToPay = SaveDataConversion.ToSaveArray(_remainsToPay),
            AccumulatedTransferTime = _accumulatedTransferTime
        };
    }

    public void ApplySave(OpeningHexSaveData saveData)
    {
        if (saveData == null)
            return;

        if (saveData.RemainsToPay != null)
        {
            foreach (var resource in saveData.RemainsToPay)
            {
                if (_remainsToPay.ContainsKey(resource.Type))
                    _remainsToPay[resource.Type] = resource.Amount;
            }
        }

        _accumulatedTransferTime = saveData.AccumulatedTransferTime;

        if (!HasRemainingPayment())
        {
            RaiseOpened();
            if (_openedHexagon != null)
                _openedHexagon.UnlockFromSave();
            if (_additiveOpenedObject != null)
                _additiveOpenedObject.SetActive(true);

            DisableBecauseHexAlreadyOpened();
            return;
        }

        gameObject.SetActive(true);
        RaiseValueChanged();
    }

    public void DisableBecauseHexAlreadyOpened()
    {
        if (_openedHexagon != null && _openedHexagon.StartsLocked && _openedHexagon.IsLocked)
            _openedHexagon.UnlockFromSave();

        DisableTargetObject();
        ResetPaymentState();
        gameObject.SetActive(false);
    }

    private void DisableTargetObject()
    {
        if (_objectToDisable != null)
            _objectToDisable.SetActive(false);
    }

    public void EnableBecauseTargetStillLocked()
    {
        if (_openedHexagon == null || !_openedHexagon.IsLocked)
        {
            DisableBecauseHexAlreadyOpened();
            return;
        }

        RestoreOriginalPaymentState();
        gameObject.SetActive(true);
        RaiseValueChanged();
    }
}
