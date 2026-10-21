using System;
using System.Collections.Generic;
using UnityEngine;

public class InstrumentUpgraderData : CreatingData
{
    public Action<Dictionary<ResourceType,bool>> PlayerEnter;
    public event Action<int> OnUpgradeTimeChanged;
    public Action OnClaimButtonClicked;
    public Action OnComplited;
    public bool IsMaxLevel { get; private set; }
    
    public void Initialize(int currentLevel, int nextLevel, int upgradeTime, IReadOnlyDictionary<ResourceType, int> prices, Sprite nextLevelIcon)
    {
        IsMaxLevel = false;
        _elapsedTime = 0;
        _currentLevel = currentLevel;
        _nextLevel = nextLevel;
        _upgradeTime = upgradeTime;
        Prices = prices;
        NextLevelIcon = nextLevelIcon;
    }

    public void InitializeMaxLevel(int currentLevel, Sprite maxLevelIcon)
    {
        IsMaxLevel = true;
        _elapsedTime = 0;
        _currentLevel = currentLevel;
        _nextLevel = currentLevel;
        _upgradeTime = 0;
        Prices = new Dictionary<ResourceType, int>();
        NextLevelIcon = maxLevelIcon;
    }
    
    public Sprite NextLevelIcon {get; private set;}
    public IReadOnlyDictionary<ResourceType, int> Prices {get; private set;}
    public int ElapsedTime 
    {
        get => _elapsedTime; 
        set
        {
            if(value < 0) value = 0;
            _elapsedTime = value;
            OnUpgradeTimeChanged?.Invoke(_elapsedTime);
        }   
    }
    public int CurrentLevel 
    {
        get => _currentLevel; 
        set
        {
            if(value < 0) value = 1;
            _currentLevel = value;
        }
    }
    public int NextLevel 
    {
        get => _nextLevel;
        set
        {
            if(value < 0) value = 2;
            _nextLevel = value;
        }
    }
    public int UpgradeTime 
    {
        get => _upgradeTime; 
        set
        {
            if(value < 0) value = 0;
            _upgradeTime = value;
        }
    }

    public long ProcessStartUtcTicks { get; set; }

    public override int RemainingSeconds => Math.Max(0, UpgradeTime - ElapsedTime);

    public override void ApplyTimeReduction(int seconds)
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

    private int _elapsedTime;
    private int _currentLevel;
    private int _nextLevel;
    private int _upgradeTime;
}
