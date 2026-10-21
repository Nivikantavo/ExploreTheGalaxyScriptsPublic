using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "AllEquipment", menuName = "Scriptable Objects/AllEquipment")]
public class AllEquipment : ScriptableObject
{
    public IReadOnlyList<EquipmentConfig> Equipment => _armors.Concat(_helmets).ToList();
    public IReadOnlyList<EquipmentConfig> Armors => _armors;
    public IReadOnlyList<EquipmentConfig> Helmets => _helmets;

    [SerializeField] private List<EquipmentConfig> _armors = new();
    [SerializeField] private List<EquipmentConfig> _helmets = new();

    public EquipmentConfig Get(EquipmentType type, int level)
    {
        IReadOnlyList<EquipmentConfig> source = type == EquipmentType.Armor ? _armors : _helmets;
        return source.FirstOrDefault(config => config != null && config.Type == type && config.Level == level);
    }

    private void OnValidate()
    {
        RemoveMismatched(_armors, EquipmentType.Armor);
        RemoveMismatched(_helmets, EquipmentType.Helmet);
    }

    private static void RemoveMismatched(List<EquipmentConfig> configs, EquipmentType expectedType)
    {
        if (configs == null)
            return;

        EquipmentConfig mismatched = configs.Find(config => config != null && config.Type != expectedType);
        if (mismatched == null)
            return;

        Debug.LogError($"Equipment {mismatched.name} is not {expectedType}");
        configs.Remove(mismatched);
    }
}
