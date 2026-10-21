using UnityEngine;

public readonly struct LowLevelHitData
{
    public int MinLevel { get; }
    public InstrumentType InstrumentType { get; }
    public Vector3 StartPosition { get; }

    public LowLevelHitData(int minLevel, InstrumentType instrument, Vector3 startPosition)
    {
        MinLevel = minLevel;
        InstrumentType = instrument;
        StartPosition = startPosition;
    }
}
