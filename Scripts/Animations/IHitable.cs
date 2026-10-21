using System;
using UnityEngine;

public interface IHitAnimated
{
    public event Action OnHit;
    public event Action<LowLevelHitData>  OnHitLevelLow;
}
