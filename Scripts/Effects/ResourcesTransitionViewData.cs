using UnityEngine;

public readonly struct ResourcesTransitionViewData
{
    public int Count { get; }
    public ResourseConfig Config { get; }
    public Vector3 StartPosition { get; }
    public Transform PickupTarget { get; }

    public ResourcesTransitionViewData(int count, ResourseConfig config, Vector3 startPosition, Transform pickupTarget)
    {
        Count = count;
        Config = config;
        StartPosition = startPosition;
        PickupTarget = pickupTarget;
    }
}
