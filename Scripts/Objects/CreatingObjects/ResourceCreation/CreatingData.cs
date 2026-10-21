using System;
using System.Collections.Generic;
using UnityEngine;

public class CreatingData
{
    public Action OnRequestedResourceCountChanged;
    public event Action<int> OnProgressTimeChanged;
    public Action OnClaimButtonClicked;
    public Action OnComplited;
    
    public void Initialize(int requestedForResult, int creatingTime, Sprite resourceIcon,
            ResourceType resultResourceType, ResourceType requestedResourceType, int resourceCount)
    {
        IsCountLocked = false;
        ProcessStartUtcTicks = 0;
        _elapsedTime = 0;
        TimeForCreatingAll = 0;
        _requestedForResult = requestedForResult;
        _creatingTime = creatingTime;
        ResourceIcon = resourceIcon;
        _resultResourceType = resultResourceType;
        _requestedResourceType = requestedResourceType;
        ResourceCount = resourceCount;
    }

    public ResourceType ResultResourceType => _resultResourceType;
    public ResourceType RequestedResourceType => _requestedResourceType;
    public Sprite ResourceIcon {get; private set;}
    public int TimeForCreatingAll {get; private set;}
    public int RequestedResourceCount {get; private set;}
    public int MaxRequestedCount
    {
        get => _maxRequestCount;
        set
        {
            if(value >= 0)
            {
                _maxRequestCount = value;
                OnRequestedResourceCountChanged?.Invoke();
            }
        }
    }

    public int ResourceCount 
    {
        get => _resourceCount;
        set
        {
            if (IsCountLocked)
                return;

            if(value >= 0)
            {
                _resourceCount = value;
                TimeForCreatingAll = _creatingTime * _resourceCount;
                RequestedResourceCount = _resourceCount * _requestedForResult;
            }
        }
    }

    public int ElapsedTime 
    {
        get => _elapsedTime; 
        set
        {
            if(value < 0) value = 0;
            _elapsedTime = value;
            OnProgressTimeChanged?.Invoke(_elapsedTime);
        }   
    }
    public int RequestedForResult 
    {
        get => _requestedForResult; 
        set
        {
            if(value < 0) value = 1;
            _requestedForResult = value;
        }
    }
    public int CreatingTime 
    {
        get => _creatingTime;
        set
        {
            if(value < 0) value = 2;
            _creatingTime = value;
        }
    }

    public long ProcessStartUtcTicks { get; set; }

    public bool IsCountLocked { get; set; }

    public virtual int RemainingSeconds => Math.Max(0, TimeForCreatingAll - ElapsedTime);

    public virtual void ApplyTimeReduction(int seconds)
    {
        if (seconds <= 0)
            return;

        int applied = Math.Min(seconds, RemainingSeconds);
        if (applied <= 0)
            return;

        ElapsedTime += applied;

        if (ProcessStartUtcTicks > 0)
            ProcessStartUtcTicks -= applied * TimeSpan.TicksPerSecond;
    }

    private int _resourceCount;
    private ResourceType _resultResourceType;
    private ResourceType _requestedResourceType;
    private int _elapsedTime;
    private int _requestedForResult;
    private int _creatingTime;
    private int _maxRequestCount = 0;
}
