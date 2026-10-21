using System.Linq;
using UnityEngine;
using Zenject;

public class InstrumentViewer : MonoBehaviour
{
    [SerializeField] private InstrumentObject _axeView;
    [SerializeField] private InstrumentObject _pickaxeView;
    [SerializeField] private InstrumentObject _swordView;

    private Inventory _inventory;

    [Inject]
    private void Construct(Inventory inventory)
    {
        _inventory = inventory;

        var axe = _inventory.Instruments.First(inst => inst.Type == InstrumentType.Axe);
        _axeView.Initialize(_inventory, axe);

        var pickaxe = _inventory.Instruments.First(inst => inst.Type == InstrumentType.Pickaxe);
        _pickaxeView.Initialize(_inventory, pickaxe);

        var sword = _inventory.Instruments.First(inst => inst.Type == InstrumentType.Sword);
        _swordView.Initialize(_inventory, sword);
    }

    public void ShowInstrument(InstrumentType type)
    {
        var instrument = GetInstrumentBy(type);
        instrument.gameObject.SetActive(true);
    }

    public void HideInstrument(InstrumentType type)
    {
        var instrument = GetInstrumentBy(type);
        instrument.gameObject.SetActive(false);
    }

    public void ResetInstrument(InstrumentType type)
    {
        var instrument = GetInstrumentBy(type);
        instrument.Clear();
    }

    private InstrumentObject GetInstrumentBy(InstrumentType type)
    {
        switch(type)
        {
            case InstrumentType.Axe:
                return _axeView;
            case InstrumentType.Pickaxe:
                return _pickaxeView;
            case InstrumentType.Sword:
                return _swordView;
            case InstrumentType.None:
                return null;
        }
        throw new System.ArgumentException(nameof(type));
    }
}
