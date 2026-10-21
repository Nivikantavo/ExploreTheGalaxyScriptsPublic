using UnityEngine;
using Zenject;

public class ShopRemoveAdsTabView : MonoBehaviour
{
    [SerializeField] private ShopOfferView _removeAdsOffer;

    private IapService _iapService;
    private IapCatalogSO _catalog;

    [Inject]
    private void Construct(IapService iapService, IapCatalogSO catalog)
    {
        _iapService = iapService;
        _catalog = catalog;
    }

    public void Refresh()
    {
        if (_removeAdsOffer == null || _catalog == null)
            return;

        string title = SafeLocalized(_catalog.RemoveAdsTitle);
        if (string.IsNullOrEmpty(title))
            title = "Remove Ads";

        string price = _iapService != null ? _iapService.GetPriceLabel(_catalog.RemoveAdsProductId) : string.Empty;
        string currencyImageUrl = _iapService != null
            ? _iapService.GetPriceCurrencyImageUrl(_catalog.RemoveAdsProductId)
            : string.Empty;
        bool available = _iapService != null
            && _iapService.IsRemoveAdsAvailable
            && _iapService.CanPurchaseProduct(_catalog.RemoveAdsProductId);
        bool owned = _iapService != null && _iapService.AdsRemoved;
        _removeAdsOffer.Bind(
            _catalog.RemoveAdsProductId,
            title,
            price,
            string.Empty,
            null,
            available,
            OnRemoveAdsBuyClicked,
            currencyImageUrl,
            owned);
        _removeAdsOffer.SetSale(
            _iapService != null ? _iapService.GetSalePercent(_catalog.RemoveAdsProductId) : 0);
    }

    private void OnRemoveAdsBuyClicked(string productId)
    {
        _iapService?.TryPurchaseRemoveAds();
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
