using System.Collections.Generic;
using UnityEngine;

public class InstrumentObject : MonoBehaviour
{
    private List<IResourceKeeper> _resourseHolderViews = new();
    private Instrument _instrument;
    private Inventory _inventory;
    private GameObject _instrumentView;

    public void Initialize(Inventory inventory, Instrument instrument)
    {
        _inventory = inventory;
        _instrument = instrument;
        _instrument.InstrumentUpgraded += OnInstrumentUpgraded;

        SetInstrumentView(_instrument.InstrumentView);
    }

    public void Clear() 
    {
        _resourseHolderViews.Clear();
    }

    private void HitResourceHolder(IResourceKeeper resourseHolderView)
    {
        _resourseHolderViews.Add(resourseHolderView);
        int resource = resourseHolderView.KnockOutResources(_instrument, transform);
        if(resource > 0)
        {
            _inventory.AddResource(resourseHolderView.ResourseType, resource);
        }
    }

    private void OnDestroy() 
    {
        _instrument.InstrumentUpgraded -= OnInstrumentUpgraded;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetResourceKeeper(other, out IResourceKeeper resourseHolderView))
            return;

        if (ContainsKeeper(resourseHolderView))
            return;

        HitResourceHolder(resourseHolderView);
    }

    private static bool TryGetResourceKeeper(Collider other, out IResourceKeeper keeper)
    {
        if (other.TryGetComponent(out ResourseHolderView holder))
        {
            keeper = holder;
            return true;
        }

        if (other.TryGetComponent(out Enemy enemy))
        {
            keeper = enemy;
            return true;
        }

        keeper = null;
        return false;
    }

    private bool ContainsKeeper(IResourceKeeper keeper)
    {
        for (int i = 0; i < _resourseHolderViews.Count; i++)
        {
            if (ReferenceEquals(_resourseHolderViews[i], keeper))
                return true;
        }

        return false;
    }

    private void OnInstrumentUpgraded(InstrumentConfig config)
    {
        if(_instrumentView != null)
        {
            Destroy(_instrumentView);
        }
        
        SetInstrumentView(config.InstrumentObj);
    }

    private void SetInstrumentView(GameObject instrumentObj)
    {
        _instrumentView = Instantiate(instrumentObj, transform);
        _instrumentView.transform.localPosition = Vector3.zero;
        _instrumentView.transform.localRotation = Quaternion.identity;
    }
}
