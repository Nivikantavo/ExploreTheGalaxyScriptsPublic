using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "AllInstruments", menuName = "Scriptable Objects/AllInstruments")]
public class AllInstruments : ScriptableObject
{
    public IReadOnlyList<InstrumentConfig> Instruments => _axes.Concat(_pickaxes.Concat(_swords)).ToList();
    public IReadOnlyList<InstrumentConfig> Axes => _axes;
    public IReadOnlyList<InstrumentConfig> Pickaxes => _pickaxes;
    public IReadOnlyList<InstrumentConfig> Swords => _swords;
    [SerializeField] private List<InstrumentConfig> _axes;
    [SerializeField] private List<InstrumentConfig> _pickaxes;
    [SerializeField] private List<InstrumentConfig> _swords;

    private void OnValidate() 
    {
        InstrumentConfig instrument = _axes.Find(instrument => instrument.Type != InstrumentType.Axe);
        if(instrument != null)
        {
            Debug.LogError($"Instrument {instrument.Type} is not an axe");
            _axes.Remove(instrument);
        }
        instrument = _pickaxes.Find(instrument => instrument.Type != InstrumentType.Pickaxe);
        if(instrument != null)
        {
            Debug.LogError($"Instrument {instrument.Type} is not a pickaxe");
            _pickaxes.Remove(instrument);
        }
        instrument = _swords.Find(instrument => instrument.Type != InstrumentType.Sword);
        if(instrument != null)
        {
            Debug.LogError($"Instrument {instrument.Type} is not a sword");
            _swords.Remove(instrument);
        }
    }
}
