using System;
using UnityEngine;

public class ResourseHolder
{
    public event Action OnResourceChanged;

    public Health Health => _health;
    public int MaxResourseCount {get; private set;}
    public int MinInstrumentLevel {get; private set;}
    public ResourceType ResourseType {get; private set;}
    public InstrumentType InstrumentType {get; private set;}

    private int _currentResourseCount;
    private Health _health;

    public ResourseHolder(int maxResourseCount, int maxHealth, ResourceType resourseType, InstrumentType instrumentType, int minInstrumentLevel)
    {
        MaxResourseCount = maxResourseCount;
        _currentResourseCount = MaxResourseCount;
        _health = new Health(maxHealth);
        ResourseType = resourseType;
        InstrumentType = instrumentType;
        MinInstrumentLevel = minInstrumentLevel;
    }

    public int GetResource(int damage)
    {
        if (_health.CurrentHealth <= 0 || damage <= 0)
            return 0;

        float actualDamage = Mathf.Min(damage, _health.CurrentHealth);
        _health.TakeDamage(actualDamage);

        int resource;
        if (_health.CurrentHealth <= 0)
        {
            // Last hit: give only what remains so total never exceeds MaxResourseCount.
            resource = _currentResourseCount;
        }
        else
        {
            resource = (int)(actualDamage / _health.MaxHealth * MaxResourseCount);
            resource = Mathf.Clamp(resource, 0, _currentResourseCount);
        }

        _currentResourseCount -= resource;
        OnResourceChanged?.Invoke();

        return resource;
    }

    public void Repair()
    {
        _health.RestoreHealth();
        _currentResourseCount = MaxResourseCount;
        OnResourceChanged?.Invoke();
    }
}
