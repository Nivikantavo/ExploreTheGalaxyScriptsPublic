using System;

public class LifecycleEventBus
{
    public event Action<LifecycleEventData> OnEvent;

    public void Publish(LifecycleEventData data) => OnEvent?.Invoke(data);
}
