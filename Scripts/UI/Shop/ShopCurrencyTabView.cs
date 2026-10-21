using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ShopCurrencyTabView : MonoBehaviour
{
    [SerializeField] private Transform _gemPacksRoot;
    [SerializeField] private ShopOfferView _gemPackOfferPrefab;
    [SerializeField] private ShopStarterPackOfferView _starterPackOffer;
    [SerializeField] private ShopInstrumentUpgradeView _instrumentUpgradeView;
    [SerializeField] private ResourceTypeViews _resourceTypeViews;

    private IapService _iapService;
    private IapCatalogSO _catalog;
    private AllInstruments _allInstruments;
    private Inventory _inventory;
    private readonly List<ShopOfferView> _spawnedGemOffers = new();

    [Inject]
    private void Construct(
        IapService iapService,
        IapCatalogSO catalog,
        AllInstruments allInstruments,
        Inventory inventory)
    {
        _iapService = iapService;
        _catalog = catalog;
        _allInstruments = allInstruments;
        _inventory = inventory;
    }

    public void Refresh()
    {
        RefreshGemPacks();
        RefreshStarterPack();
        _instrumentUpgradeView?.Refresh();
    }

    private void RefreshGemPacks()
    {
        if (_catalog == null)
            return;

        EnsureGemOfferViews();

        IReadOnlyList<IapGemPackDefinition> packs = _catalog.GemPacks;
        for (int i = 0; i < _spawnedGemOffers.Count; i++)
        {
            ShopOfferView view = _spawnedGemOffers[i];
            if (view == null)
                continue;

            if (packs == null || i >= packs.Count || packs[i] == null)
            {
                view.gameObject.SetActive(false);
                continue;
            }

            IapGemPackDefinition pack = packs[i];
            view.gameObject.SetActive(true);
            Sprite icon = pack.Icon != null ? pack.Icon : ResolveIcon(ResourceType.Gem);
            string title = $"{pack.GemAmount}";

            string price = _iapService != null ? _iapService.GetPriceLabel(pack.ProductId) : string.Empty;
            string currencyImageUrl = _iapService != null
                ? _iapService.GetPriceCurrencyImageUrl(pack.ProductId)
                : string.Empty;
            bool canBuy = _iapService != null
                && _iapService.CanPurchaseProduct(pack.ProductId);
            view.Bind(
                pack.ProductId,
                title,
                price,
                $"x{pack.GemAmount}",
                icon,
                canBuy,
                OnGemPackBuyClicked,
                currencyImageUrl);
            view.SetSale(_iapService != null ? _iapService.GetSalePercent(pack.ProductId) : 0);
        }
    }

    private void EnsureGemOfferViews()
    {
        if (_gemPacksRoot == null || _gemPackOfferPrefab == null || _catalog?.GemPacks == null)
            return;

        while (_spawnedGemOffers.Count < _catalog.GemPacks.Count)
        {
            ShopOfferView view = Instantiate(_gemPackOfferPrefab, _gemPacksRoot);
            view.gameObject.SetActive(true);
            _spawnedGemOffers.Add(view);
        }
    }

    private void RefreshStarterPack()
    {
        if (_starterPackOffer == null || _catalog?.StarterPack == null)
            return;

        StarterPackSO starter = _catalog.StarterPack;
        string price = _iapService != null ? _iapService.GetPriceLabel(starter.ProductId) : string.Empty;
        string currencyImageUrl = _iapService != null
            ? _iapService.GetPriceCurrencyImageUrl(starter.ProductId)
            : string.Empty;
        bool available = _iapService != null
            && _iapService.IsStarterPackAvailable
            && _iapService.CanPurchaseProduct(starter.ProductId);
        bool owned = _iapService != null && _iapService.StarterPackPurchased;
        _starterPackOffer.Bind(
            starter,
            _allInstruments,
            _inventory,
            price,
            available,
            OnStarterBuyClicked,
            currencyImageUrl,
            owned);
        _starterPackOffer.SetSale(_iapService != null ? _iapService.GetSalePercent(starter.ProductId) : 0);
    }

    private void OnGemPackBuyClicked(string productId)
    {
        _iapService?.TryPurchaseGemPack(productId);
    }

    private void OnStarterBuyClicked()
    {
        _iapService?.TryPurchaseStarterPack();
    }

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

    private static string SafeLocalized(UnityEngine.Localization.LocalizedString localized)
    {
        if (localized == null)
            return string.Empty;

        try
        {
            string value = localized.GetLocalizedString();
            return value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
