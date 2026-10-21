using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DefaultInstruments", menuName = "Scriptable Objects/DefaultInstruments")]
public class DefaultInstruments : ScriptableObject
{
    public IReadOnlyList<InstrumentConfig> Instruments => _instruments;
    [SerializeField] private List<InstrumentConfig> _instruments;
}
