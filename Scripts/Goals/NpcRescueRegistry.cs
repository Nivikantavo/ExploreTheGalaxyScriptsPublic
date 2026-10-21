using System;
using System.Collections.Generic;

public class NpcRescueRegistry
{
    public event Action Changed;

    private readonly Dictionary<string, NpcRescueSite> _sitesById = new();

    public void Register(NpcRescueSite site)
    {
        if (site == null)
            return;

        RegisterId(site.GetSaveId(), site);
        RegisterId(site.gameObject.name, site);
    }

    public void Unregister(NpcRescueSite site)
    {
        if (site == null)
            return;

        RemoveIfSame(site.GetSaveId(), site);
        RemoveIfSame(site.gameObject.name, site);
    }

    public bool IsRescued(string id)
    {
        NpcRescueSite site = Find(id);
        return site != null && site.HasArrived;
    }

    public NpcRescueSite Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return _sitesById.GetValueOrDefault(id);
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private void RegisterId(string id, NpcRescueSite site)
    {
        if (string.IsNullOrEmpty(id))
            return;

        _sitesById[id] = site;
    }

    private void RemoveIfSame(string id, NpcRescueSite site)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (_sitesById.TryGetValue(id, out NpcRescueSite registered) && registered == site)
            _sitesById.Remove(id);
    }
}
