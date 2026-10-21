using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ShopResourceExchangeView : MonoBehaviour
{
    [SerializeField] private Transform _resourceOffersRoot;
    [SerializeField] private ShopOfferView _resourceOfferPrefab;
    [SerializeField] private ShopResourceExchangeCatalogSO _catalog;
    [SerializeField] private ResourceTypeViews _resourceTypeViews;
    [SerializeField] private AllResoursesConfig _allResoursesConfig;

    private Inventory _inventory;
    private ISaveLoader _saveLoader;
    private readonly List<ShopOfferView> _spawnedOffers = new();
    private readonly List<ShopResourceExchangeOffer> _visibleOffers = new();

    [Inject]
    private void Construct(Inventory inventory, ISaveLoader saveLoader)
    {
        _inventory = inventory;
        _saveLoader = saveLoader;
    }

    private void OnEnable()
    {
        if (_inventory != null)
        {
            _inventory.CellChanged += OnInventoryCellChanged;
            _inventory.LifetimeResourceChanged += OnLifetimeResourceChanged;
        }

        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory == null)
            return;

        _inventory.CellChanged -= OnInventoryCellChanged;
        _inventory.LifetimeResourceChanged -= OnLifetimeResourceChanged;
    }

    public void Refresh()
    {
        if (_resourceOffersRoot == null || _resourceOfferPrefab == null || _inventory == null || _catalog?.Offers == null)
            return;

        BuildVisibleOffers();
        EnsureOfferViews(_visibleOffers.Count);

        int gemBalance = _inventory.GetResourceAmount(ResourceType.Gem);

        for (int i = 0; i < _spawnedOffers.Count; i++)
        {
            ShopOfferView view = _spawnedOffers[i];
            if (view == null)
                continue;

            if (i >= _visibleOffers.Count)
            {
                view.gameObject.SetActive(false);
                continue;
            }

            ShopResourceExchangeOffer offer = _visibleOffers[i];
            ResourceType type = offer.ResourceType;
            int amount = Mathf.Max(1, offer.Amount);
            int gemCost = Mathf.Max(1, offer.GemCost);
            int offerIndex = i;

            view.gameObject.SetActive(true);
            view.BindResource(
                type,
                ResolveResourceName(type),
                gemCost.ToString(),
                $"x{amount}",
                ResolveIcon(type),
                gemBalance >= gemCost,
                _ => OnResourceBuyClicked(offerIndex));
        }
    }

    private void BuildVisibleOffers()
    {
        _visibleOffers.Clear();
        IReadOnlyList<ShopResourceExchangeOffer> offers = _catalog.Offers;
        for (int i = 0; i < offers.Count; i++)
        {
            ShopResourceExchangeOffer offer = offers[i];
            if (offer.Amount <= 0 || offer.GemCost <= 0)
                continue;

            if (!ShopResourceOfferRules.IsUnlockedForShop(_inventory, offer.ResourceType))
                continue;

            _visibleOffers.Add(offer);
        }
    }

    private void EnsureOfferViews(int count)
    {
        while (_spawnedOffers.Count < count)
        {
            ShopOfferView view = Instantiate(_resourceOfferPrefab, _resourceOffersRoot);
            view.gameObject.SetActive(true);
            _spawnedOffers.Add(view);
        }
    }

    private void OnResourceBuyClicked(int offerIndex)
    {
        if (_inventory == null || offerIndex < 0 || offerIndex >= _visibleOffers.Count)
            return;

        ShopResourceExchangeOffer offer = _visibleOffers[offerIndex];
        if (!ShopResourceOfferRules.IsUnlockedForShop(_inventory, offer.ResourceType))
            return;

        int amount = Mathf.Max(1, offer.Amount);
        int gemCost = Mathf.Max(1, offer.GemCost);

        if (_inventory.GetResourceAmount(ResourceType.Gem) < gemCost)
        {
            Refresh();
            return;
        }

        _inventory.RemoveResource(ResourceType.Gem, gemCost);
        _inventory.AddResourceWithoutLifetime(offer.ResourceType, amount);
        _saveLoader?.Save();
        Refresh();
    }

    private void OnInventoryCellChanged(ResourceCell cell)
    {
        if (cell != null && cell.ResourseType == ResourceType.Gem)
            Refresh();
    }

    private void OnLifetimeResourceChanged(ResourceType _, int __) => Refresh();

    private Sprite ResolveIcon(ResourceType type)
    {
        if (_resourceTypeViews != null
            && _resourceTypeViews.Icons != null
            && _resourceTypeViews.Icons.TryGetValue(type, out Sprite sprite))
        {
            return sprite;
        }

        return null;
    }

    private string ResolveResourceName(ResourceType type)
    {
        if (_allResoursesConfig?.Resourses != null)
        {
            foreach (ResourseConfig config in _allResoursesConfig.Resourses)
            {
                if (config == null || config.ResourseType != type)
                    continue;

                try
                {
                    string name = config.Name;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
                catch
                {
                }

                break;
            }
        }

        return type.ToString();
    }
}
