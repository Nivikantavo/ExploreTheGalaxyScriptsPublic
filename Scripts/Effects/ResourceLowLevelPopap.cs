using System.Linq;
using UnityEngine;
using Zenject;

public class ResourceLowLevelPopap : MonoBehaviour
{
    [SerializeField] private GameObject _pickupPopupPrefab;
    [SerializeField] private Vector3 _pickupPopupOffset = new Vector3(0f, 1.5f, 0f);

    private IHitAnimated _hited;
    private Sprite _icon;
    private AllInstruments _instruments;

    [Inject]
    private void Construct(AllInstruments instruments)
    {
        _instruments = instruments;
    }

    private void Awake()
    {
        _hited = GetComponentInParent<IHitAnimated>();
    }

    private void OnEnable()
    {
        _hited.OnHitLevelLow += LowLevelHit;
    }

    private void OnDisable()
    {
        _hited.OnHitLevelLow -= LowLevelHit;
    }

    private void LowLevelHit(LowLevelHitData data)
    {
        SpawnPopup(data);
    }

    private void SpawnPopup(LowLevelHitData data)
    {
        if(_icon == null)
        {
            _icon = _instruments.Instruments.FirstOrDefault(instr => instr.Type == data.InstrumentType && instr.Level == data.MinLevel).Icon;
        }

        ResoursePickupPopupPool.SpawnAndPlay(
            prefab: _pickupPopupPrefab,
            spawnPosition: data.StartPosition + _pickupPopupOffset,
            icon: _icon,
            info: data.MinLevel.ToString()
        );
    }
}
