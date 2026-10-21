using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "InstrumentConfig", menuName = "Scriptable Objects/InstrumentConfig")]
public class InstrumentConfig : ScriptableObject
{
    public InstrumentType Type => _type;
    public int Level => _level;
    public int DamagePerHit => _damagePerHit;
    public int UpgradeTime => _upgradeTime;
    public Sprite Icon => _icon;
    public GameObject InstrumentObj => _instrumentObj;
    public Quaternion RotationOffset => _rotationOffset;
    public IReadOnlyDictionary<ResourceType, int> Price => _price;
    
    [SerializeField] private InstrumentType _type;
    [SerializeField] private int _level;
    [SerializeField] private int _damagePerHit;
    [SerializeField] private int _upgradeTime;
    [SerializeField] private Sprite _icon;
    [SerializeField] private GameObject _instrumentObj;
    [SerializeField] private Quaternion _rotationOffset = Quaternion.identity;
    [SerializeField] private SerializedDictionary<ResourceType, int> _price;
}
