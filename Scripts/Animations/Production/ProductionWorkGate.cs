using UnityEngine;

public static class ProductionWorkGate
{
    public static bool IsInProgress(Component host)
    {
        if (host == null)
            return false;

        var instrument = host.GetComponent<InstrumentUpgrader>();
        if (instrument != null)
            return instrument.ProcessState == TimedProcessState.InProgress;

        var equipment = host.GetComponent<EquipmentUpgrader>();
        if (equipment != null)
            return equipment.ProcessState == TimedProcessState.InProgress;

        var resource = host.GetComponent<CreatingResourceObject>();
        if (resource != null)
            return resource.ProcessState == TimedProcessState.InProgress;

        return false;
    }

    public static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
