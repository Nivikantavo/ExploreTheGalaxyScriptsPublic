using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Zenject;

public class ShopRewardedResourcesTabView : MonoBehaviour
{
    [SerializeField] private Transform _offersRoot;
    [SerializeField] private ShopOfferView _offerPrefab;
    [SerializeField] private ShopRewardedResourceCatalogSO _catalog;
    [SerializeField] private ResourceTypeViews _resourceTypeViews;
    [SerializeField] private AllResoursesConfig _allResoursesConfig;
    [SerializeField] private LocalizedString _watchLabel;

    private Inventory _inventory;
    private ISaveLoader _saveLoader;
    private RewardedAdsController _rewardedAdsController;
    private readonly List<ShopOfferView> _spawnedOffers = new();
    private readonly List<ShopRewardedResourceOffer> _visibleOffers = new();

    [Inject]
    private void Construct(
        Inventory inventory,
        ISaveLoader saveLoader,
        RewardedAdsController rewardedAdsController)
    {
        _inventory = inventory;
        _saveLoader = saveLoader;
        _rewardedAdsController = rewardedAdsController;
    }

    private void OnEnable()
    {
        if (_inventory != null)
            _inventory.LifetimeResourceChanged += OnLifetimeResourceChanged;

        Refresh();
    }

    private void Start() => Refresh();

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.LifetimeResourceChanged -= OnLifetimeResourceChanged;
    }

    public void Refresh()
    {
        if (_offersRoot == null || _offerPrefab == null || _catalog?.Offers == null)
            return;

        BuildVisibleOffers();
        EnsureOfferViews(_visibleOffers.Count);

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

            ShopRewardedResourceOffer offer = _visibleOffers[i];
            ResourceType type = offer.ResourceType;
            int offerIndex = i;
            view.gameObject.SetActive(true);
            view.BindResource(
                type,
                ResolveResourceName(type),
                LocalizationSafe.Get(_watchLabel),
                $"x{offer.Amount}",
                ResolveIcon(type),
                true,
                _ => OnWatchClicked(offerIndex));
        }
    }

    private void BuildVisibleOffers()
    {
        _visibleOffers.Clear();
        IReadOnlyList<ShopRewardedResourceOffer> offers = _catalog.Offers;
        for (int i = 0; i < offers.Count; i++)
        {
            ShopRewardedResourceOffer offer = offers[i];
            if (offer.Amount <= 0)
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
            ShopOfferView view = Instantiate(_offerPrefab, _offersRoot);
            view.gameObject.SetActive(true);
            _spawnedOffers.Add(view);
        }
    }

    private void OnWatchClicked(int offerIndex)
    {
        if (_inventory == null || _rewardedAdsController == null)
            return;

        if (offerIndex < 0 || offerIndex >= _visibleOffers.Count)
            return;

        ShopRewardedResourceOffer offer = _visibleOffers[offerIndex];
        if (offer.Amount <= 0 || !ShopResourceOfferRules.IsUnlockedForShop(_inventory, offer.ResourceType))
            return;

        string placement = ShopRewardedResourceCatalogSO.ResolvePlacement(offer);
        _rewardedAdsController.TryShowRewarded(placement, () =>
        {
            _inventory.AddRewardedAdResource(offer.ResourceType, offer.Amount);
            _saveLoader?.Save();
            Refresh();
        });
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
