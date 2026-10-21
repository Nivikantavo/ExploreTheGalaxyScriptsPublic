using UnityEngine;

public readonly struct LifecycleEventData
{
    public LifecycleEventType Type { get; }
    public Vector3 Position { get; }

    public LifecycleEventData(LifecycleEventType type, Vector3 position)
    {
        Type = type;
        Position = position;
    }
}
