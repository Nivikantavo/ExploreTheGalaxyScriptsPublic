using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zenject;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private InstrumentCellView _swordCell;
    [SerializeField] private InstrumentCellView _pickaxeCell;
    [SerializeField] private InstrumentCellView _axeCell;
    [SerializeField] private InstrumentCellView _helmetCell;
    [SerializeField] private InstrumentCellView _armorCell;
    [SerializeField] private ResourceCellView _resourceCellViewPrefab;
    [SerializeField] private Transform _resourceCellViewsContainer;
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private TextMeshProUGUI _healthText;

    private Player _player;
    private Inventory _inventory;
    private List<ResourceCellView> _resourceCellViews = new List<ResourceCellView>();
    private AllResoursesConfig _allResoursesConfig;
    private AllInstruments _allInstruments;
    private AllEquipment _allEquipment;
    private ISaveLoader _saveLoader;
    private TutorialHandler _tutorialHandler;
    private bool _holdsPause;

    [Inject]
    private void Construct(
        AllResoursesConfig allResoursesConfig,
        AllInstruments allInstruments,
        AllEquipment allEquipment,
        Player player,
        ISaveLoader saveLoader)
    {
        _player = player;
        _inventory = player.Inventory;
        _allResoursesConfig = allResoursesConfig;
        _allInstruments = allInstruments;
        _allEquipment = allEquipment;
        _saveLoader = saveLoader;
    }

    private void Start()
    {
        _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        foreach (var instrument in _inventory.Instruments)
        {
            var view = GetInstrumentCellBy(instrument.Type);
            InstrumentConfig config = _allInstruments.Instruments.FirstOrDefault(instrConf => instrConf.Type == instrument.Type && instrConf.Level == instrument.Level);
            view.Initialize(config);
            instrument.InstrumentUpgraded += OnInstrumetnUpgraded;
        }

        _inventory.EquipmentChanged += OnEquipmentChanged;
        RefreshEquipmentCells();

        _player.OnHealthInitialized += OnPlayerMaxHealthChanged;
        _healthText.text = _player.MaxHealth.ToString();
    }

    private void OnDestroy()
    {
        EndPause();

        if (_player != null)
            _player.OnHealthInitialized -= OnPlayerMaxHealthChanged;

        if (_inventory == null)
            return;

        _inventory.EquipmentChanged -= OnEquipmentChanged;

        foreach (var instrument in _inventory.Instruments)
        {
            instrument.InstrumentUpgraded -= OnInstrumetnUpgraded;
        }
    }

    public void Show()
    {
        if (_tutorialHandler == null)
            _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
            return;

        _inventoryPanel.SetActive(true);
        UpdateVeiw();
        RefreshEquipmentCells();
        BeginPause();
    }

    public void Hide()
    {
        _inventoryPanel.SetActive(false);
        EndPause();
    }

    private void BeginPause()
    {
        if (_holdsPause)
            return;

        GameplayPause.Push();
        _holdsPause = true;
    }

    private void EndPause()
    {
        if (!_holdsPause)
            return;

        GameplayPause.Pop();
        _holdsPause = false;
    }

    private void UpdateVeiw()
    {
        foreach(ResourceCell cell in _inventory.Cells)
        {
            if(_resourceCellViews.Any(r => r.ResourseType == cell.ResourseType))
            {
                ResourceCellView existingResourceCellView = _resourceCellViews.FirstOrDefault(r => r.ResourseType == cell.ResourseType);
                if(existingResourceCellView.Amount != cell.Amount)
                {
                    existingResourceCellView.UpdateAmount(cell.Amount);
                }
                continue;
            }
            ResourceCellView resourceCellView = Instantiate(_resourceCellViewPrefab, _resourceCellViewsContainer);
            resourceCellView.Initialize(_allResoursesConfig.Resourses.FirstOrDefault(r => r.ResourseType == cell.ResourseType), cell.Amount);
            _resourceCellViews.Add(resourceCellView);
        }
    }

    private InstrumentCellView GetInstrumentCellBy(InstrumentType type)
    {
        switch (type)
        {
            case InstrumentType.Axe:
                return _axeCell;
            case InstrumentType.Pickaxe:
                return _pickaxeCell;
            case InstrumentType.Sword:
                return _swordCell;
            case InstrumentType.None:
                break;
        }
        return null;
    }

    private void OnInstrumetnUpgraded(InstrumentConfig config)
    {
        var view = GetInstrumentCellBy(config.Type);
        view.Initialize(config);
    }

    private void OnEquipmentChanged(Equipment equipment)
    {
        if (equipment == null)
            return;

        RefreshEquipmentCell(equipment.Type);
    }

    private void RefreshEquipmentCells()
    {
        RefreshEquipmentCell(EquipmentType.Helmet);
        RefreshEquipmentCell(EquipmentType.Armor);
    }

    private void RefreshEquipmentCell(EquipmentType type)
    {
        InstrumentCellView view = type == EquipmentType.Helmet ? _helmetCell : _armorCell;
        if (view == null || _inventory == null)
            return;

        int level = _inventory.GetEquipmentLevel(type);
        EquipmentConfig config = level > 0 && _allEquipment != null
            ? _allEquipment.Get(type, level)
            : null;
        view.ShowEquipment(config);
    }

    private void OnPlayerMaxHealthChanged(float maxHealth)
    {
        _healthText.text = maxHealth.ToString();
    }
}
