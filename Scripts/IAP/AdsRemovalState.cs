using System;

public class AdsRemovalState
{
    public event Action Changed;

    public bool AdsRemoved { get; private set; }

    public void SetAdsRemoved(bool value)
    {
        if (AdsRemoved == value)
            return;

        AdsRemoved = value;
        Changed?.Invoke();
    }
}
