using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "ResourseConfig", menuName = "Resourses/ResourseConfig")]
public class ResourseConfig : ScriptableObject
{
    public string Name => LocalizationSafe.Get(_name);
    public ResourceType ResourseType => _resourseType;
    public InstrumentType InstrumentType => _instrumentType;
    public int MinInstrumentLevel => _minInstrumentLevel;
    public int MaxResourseCount => _maxResourseCount;
    public int MaxHealth => _maxHealth;
    public float RepairDuration => _repairDuration;
    public GameObject ResourseObj => _resourseObj;
    public Sprite Icon => _icon;

    [SerializeField] private LocalizedString _name;
    [SerializeField] private ResourceType _resourseType;
    [SerializeField] private InstrumentType _instrumentType;
    [SerializeField] private int _minInstrumentLevel;
    [SerializeField] private int _maxResourseCount;
    [SerializeField] private int _maxHealth;
    [SerializeField] private float _repairDuration = 5f;
    [SerializeField] private GameObject _resourseObj;
    [SerializeField] private Sprite _icon;
    
}
