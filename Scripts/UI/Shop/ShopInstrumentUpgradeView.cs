using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class ShopInstrumentUpgradeView : MonoBehaviour
{
    private static readonly InstrumentType[] UpgradeTypes =
    {
        InstrumentType.Axe,
        InstrumentType.Pickaxe,
        InstrumentType.Sword,
    };

    [SerializeField] private Transform _offersRoot;
    [SerializeField] private ShopOfferView _offerPrefab;
    [SerializeField] private ShopInstrumentUpgradeCatalogSO _catalog;

    private Inventory _inventory;
    private AllInstruments _allInstruments;
    private ISaveLoader _saveLoader;
    private readonly List<ShopOfferView> _spawnedOffers = new();

    [Inject]
    private void Construct(Inventory inventory, AllInstruments allInstruments, ISaveLoader saveLoader)
    {
        _inventory = inventory;
        _allInstruments = allInstruments;
        _saveLoader = saveLoader;
    }

    private void OnEnable()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged += OnInventoryCellChanged;
            _inventory.InstrumentUpgraded += OnInstrumentUpgraded;
        }

        Refresh();
    }

    private void Start() => Refresh();

    private void OnDisable()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged -= OnInventoryCellChanged;
            _inventory.InstrumentUpgraded -= OnInstrumentUpgraded;
        }
    }

    public void Refresh()
    {
        if (_offersRoot == null || _offerPrefab == null || _inventory == null || _allInstruments == null)
            return;

        EnsureOfferViews(UpgradeTypes.Length);
        int gemBalance = _inventory.GetResourceAmount(ResourceType.Gem);

        for (int i = 0; i < _spawnedOffers.Count; i++)
        {
            ShopOfferView view = _spawnedOffers[i];
            if (view == null)
                continue;

            if (i >= UpgradeTypes.Length)
            {
                view.gameObject.SetActive(false);
                continue;
            }

            InstrumentType type = UpgradeTypes[i];
            Instrument instrument = _inventory.Instruments.FirstOrDefault(item => item.Type == type);
            if (instrument == null)
            {
                view.gameObject.SetActive(false);
                continue;
            }

            InstrumentConfig nextConfig = GetNextConfig(type, instrument.Level);
            view.gameObject.SetActive(true);

            if (nextConfig == null)
            {
                view.BindAction(
                    $"{type} MAX",
                    string.Empty,
                    $"Lv {instrument.Level}",
                    null,
                    false,
                    null);
                continue;
            }

            int cost = _catalog != null ? _catalog.GetGemCost(nextConfig.Level) : nextConfig.Level * 5;
            bool canBuy = gemBalance >= cost;
            InstrumentType capturedType = type;
            view.BindAction(
                $"{type} → {nextConfig.Level}",
                cost.ToString(),
                $"Lv {instrument.Level}→{nextConfig.Level}",
                nextConfig.Icon,
                canBuy,
                () => OnUpgradeClicked(capturedType));
        }
    }

    private void EnsureOfferViews(int count)
    {
        while (_spawnedOffers.Count < count)
        {
            ShopOfferView view = Instantiate(_offerPrefab, _offersRoot);
            view.gameObject.SetActive(true);
            _spawnedOffers.Add(view);
        }
    }

    private void OnUpgradeClicked(InstrumentType type)
    {
        if (_inventory == null || _allInstruments == null)
            return;

        Instrument instrument = _inventory.Instruments.FirstOrDefault(item => item.Type == type);
        if (instrument == null)
            return;

        InstrumentConfig nextConfig = GetNextConfig(type, instrument.Level);
        if (nextConfig == null)
        {
            Refresh();
            return;
        }

        int cost = _catalog != null ? _catalog.GetGemCost(nextConfig.Level) : nextConfig.Level * 5;
        if (cost <= 0 || _inventory.GetResourceAmount(ResourceType.Gem) < cost)
        {
            Refresh();
            return;
        }

        _inventory.RemoveResource(ResourceType.Gem, cost);
        _inventory.UpgradeInstrument(nextConfig);
        RefreshInstrumentUpgrader(type);
        _saveLoader?.Save();
        Refresh();
    }

    private InstrumentConfig GetNextConfig(InstrumentType type, int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        return _allInstruments.Instruments.FirstOrDefault(
            config => config != null && config.Type == type && config.Level == nextLevel);
    }

    private static void RefreshInstrumentUpgrader(InstrumentType type)
    {
        InstrumentUpgrader[] upgraders = FindObjectsByType<InstrumentUpgrader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < upgraders.Length; i++)
        {
            InstrumentUpgrader upgrader = upgraders[i];
            if (upgrader != null && upgrader.InstrumentType == type)
                upgrader.RefreshFromInventory();
        }
    }

    private void OnInventoryCellChanged(ResourceCell cell)
    {
        if (cell != null && cell.ResourseType == ResourceType.Gem)
            Refresh();
    }

    private void OnInstrumentUpgraded(Instrument _) => Refresh();
}
