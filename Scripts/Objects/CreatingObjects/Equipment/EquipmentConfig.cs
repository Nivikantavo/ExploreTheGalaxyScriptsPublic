using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentConfig", menuName = "Scriptable Objects/EquipmentConfig")]
public class EquipmentConfig : ScriptableObject
{
    public EquipmentType Type => _type;
    public int Level => _level;
    public int BonusHealth => _bonusHealth;
    public int UpgradeTime => _upgradeTime;
    public Sprite Icon => _icon;
    public IReadOnlyDictionary<ResourceType, int> Price => _price;

    [SerializeField] private EquipmentType _type;
    [SerializeField] private int _level = 1;
    [SerializeField, Min(0), Tooltip("Total max HP added while this level is equipped, not the delta from the previous level.")]
    private int _bonusHealth;
    [SerializeField, Min(0)] private int _upgradeTime;
    [SerializeField] private Sprite _icon;
    [SerializeField] private SerializedDictionary<ResourceType, int> _price;
}
