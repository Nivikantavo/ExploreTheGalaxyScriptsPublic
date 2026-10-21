using System;
using UnityEngine;

public interface IHealthObserver
{
    public event Action<float> HealthValueChange;
    public event Action<float> OnHealthInitialized;
}
