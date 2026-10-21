using System;
using UnityEngine;

public class Instrument
{
    public event Action<InstrumentConfig> InstrumentUpgraded;
    public int Level {get; private set;}
    public int DamagePerHit {get; private set;}
    public InstrumentType Type {get; private set;}
    public GameObject InstrumentView {get; private set;}

    public Instrument(InstrumentType type, int level, int damagePerHit, GameObject view)
    {
        Type = type;
        Level = level;
        DamagePerHit = damagePerHit;
        InstrumentView = view;
    }

    public void Upgrade(InstrumentConfig instrumentConfig)
    {
        Level = instrumentConfig.Level;
        DamagePerHit = instrumentConfig.DamagePerHit;
        InstrumentView = instrumentConfig.InstrumentObj;
        InstrumentUpgraded?.Invoke(instrumentConfig);
    }

    public void Restore(InstrumentConfig instrumentConfig) => Upgrade(instrumentConfig);
}
