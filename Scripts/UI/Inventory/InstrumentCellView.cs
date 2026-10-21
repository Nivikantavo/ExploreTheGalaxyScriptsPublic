using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InstrumentCellView : MonoBehaviour
{
    public const string LevelText = "Lvl ";
    public InstrumentType InstrumentType { get; private set; }
    public int Level { get; private set; }

    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _level;

    private Sprite _emptyIcon;
    private bool _emptyIconCached;

    public void Initialize(InstrumentConfig config)
    {
        InstrumentType = config.Type;
        _icon.sprite = config.Icon;
        UpdateLevel(config.Level);
    }

    public void ShowEquipment(EquipmentConfig config)
    {
        if (_icon == null || _level == null)
            return;

        CacheEmptyIcon();

        bool equipped = config != null && config.Level > 0;
        _icon.sprite = equipped && config.Icon != null ? config.Icon : _emptyIcon;
        _level.gameObject.SetActive(equipped);
        if (equipped)
            UpdateLevel(config.Level);
    }

    private void CacheEmptyIcon()
    {
        if (_emptyIconCached)
            return;

        _emptyIcon = _icon.sprite;
        _emptyIconCached = true;
    }

    private void UpdateLevel(int level)
    {
        Level = level;
        _level.text = LevelText + Level.ToString();
    }
}
