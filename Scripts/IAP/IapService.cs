using System;
using System.Collections.Generic;
using System.Linq;
using Playgama;
using UnityEngine;

public class IapService
{
    private const string PlaygamaPlatformId = "playgama";
    private const string PlaygamaCurrencySuffix = "GAM";
    private const string YandexPlatformId = "yandex";

    public event Action StateChanged;
    public event Action CatalogUpdated;
    public event Action SalesUpdated;
    public event Action<int> IslandOfferAvailable;
    public event Action PurchaseStarted;
    public event Action PurchaseFinished;

    public bool AdsRemoved => _adsRemoval.AdsRemoved;
    public bool StarterPackPurchased => _starterPackPurchased;
    public bool IsPurchaseInProgress => _isPurchaseInProgress;
    public bool HasStarted => _started;
    public bool IsCatalogReady => _catalogReady;

    private readonly IapCatalogSO _catalog;
    private readonly Inventory _inventory;
    private readonly AllInstruments _allInstruments;
    private readonly ISaveLoader _saveLoader;
    private readonly AdsRemovalState _adsRemoval;
    private readonly IslandProgressRegistry _islandRegistry;
    private readonly IapSaveBridge _iapSaveBridge;

    private readonly HashSet<int> _purchasedIslandOfferIndices = new();
    private readonly HashSet<int> _shownIslandOfferIndices = new();
    private readonly Dictionary<string, string> _catalogPrices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _catalogCurrencyImages = new(StringComparer.Ordinal);

    private bool _starterPackPurchased;
    private bool _isPurchaseInProgress;
    private bool _appliedSave;
    private bool _restoreRequested;
    private bool _started;
    private bool _catalogReady;
    private bool _salesRequested;
    private int _removeAdsSalePercent;
    private int _currencySalePercent;
    private int _starterPackSalePercent;

    public IapService(
        IapCatalogSO catalog,
        Inventory inventory,
        AllInstruments allInstruments,
        ISaveLoader saveLoader,
        AdsRemovalState adsRemoval,
        IslandProgressRegistry islandRegistry,
        IapSaveBridge iapSaveBridge)
    {
        _catalog = catalog;
        _inventory = inventory;
        _allInstruments = allInstruments;
        _saveLoader = saveLoader;
        _adsRemoval = adsRemoval;
        _islandRegistry = islandRegistry;
        _iapSaveBridge = iapSaveBridge;
        _iapSaveBridge?.Bind(this);
    }

    /// <summary>
    /// Call after save is loaded/applied (see SaveLoadBootstrap).
    /// </summary>
    public void StartAfterSaveApplied()
    {
        if (_started)
            return;

        _started = true;
        ApplyFromLoadedSave();
        RefreshCatalog();
        LoadSales();
        RestorePurchases();
    }

    public bool IsSupported
    {
        get
        {
#if UNITY_WEBGL
            return Bridge.instance != null
                   && Bridge.payments != null
                   && Bridge.payments.isSupported;
#else
            return false;
#endif
        }
    }

    public bool IsStarterPackAvailable =>
        IsSupported
        && _catalog != null
        && _catalog.StarterPack != null
        && !_starterPackPurchased;

    public bool IsRemoveAdsAvailable =>
        IsSupported
        && _catalog != null
        && !string.IsNullOrEmpty(_catalog.RemoveAdsProductId)
        && !_adsRemoval.AdsRemoved;

    public bool IsIslandOfferPurchased(int islandIndex) =>
        _purchasedIslandOfferIndices.Contains(islandIndex);

    public bool WasIslandOfferShown(int islandIndex) =>
        _shownIslandOfferIndices.Contains(islandIndex);

    public IslandOfferDefinition GetIslandOffer(int islandIndex) =>
        _catalog?.IslandOffers?.GetOfferForIsland(islandIndex);

    public IslandOfferDefinition GetActiveIslandOffer()
    {
        if (!IsSupported || _catalog?.IslandOffers == null)
            return null;

        int bestIndex = -1;
        IslandOfferDefinition bestOffer = null;

        foreach (IslandProgressHandler island in _islandRegistry.Islands)
        {
            if (island == null || island.IslandIndex < IslandOfferCatalogSO.MinIslandIndex)
                continue;

            if (!HasIslandProgress(island))
                continue;

            if (_purchasedIslandOfferIndices.Contains(island.IslandIndex))
                continue;

            IslandOfferDefinition offer = _catalog.IslandOffers.GetOfferForIsland(island.IslandIndex);
            if (offer == null)
                continue;

            if (island.IslandIndex >= bestIndex)
            {
                bestIndex = island.IslandIndex;
                bestOffer = offer;
            }
        }

        return bestOffer;
    }

    public string GetPriceLabel(string productId)
    {
        if (string.IsNullOrEmpty(productId))
            return string.Empty;

        return _catalogPrices.TryGetValue(productId, out string price) ? price : string.Empty;
    }

    /// <summary>
    /// Discount percent for a Yandex sale flag, or 0 when this product is not on sale.
    /// Island offers, resource trades, and gem upgrades stay at 0.
    /// </summary>
    public int GetSalePercent(string productId)
    {
        if (string.IsNullOrEmpty(productId) || _catalog == null)
            return 0;

        if (productId == _catalog.RemoveAdsProductId)
            return _removeAdsSalePercent;

        if (_catalog.GetGemPack(productId) != null)
            return _currencySalePercent;

        if (_catalog.StarterPack != null && productId == _catalog.StarterPack.ProductId)
            return _starterPackSalePercent;

        return 0;
    }

    public string GetPriceCurrencyImageUrl(string productId)
    {
        if (string.IsNullOrEmpty(productId))
            return string.Empty;

        return _catalogCurrencyImages.TryGetValue(productId, out string url) ? url : string.Empty;
    }

    /// <summary>
    /// True when Bridge catalog returned a non-empty SDK <c>price</c> for this product
    /// (includes portal currency name; do not append currency codes in UI).
    /// </summary>
    public bool HasCatalogProduct(string productId)
    {
        if (string.IsNullOrEmpty(productId) || !_catalogReady)
            return false;

        return _catalogPrices.TryGetValue(productId, out string price) && !string.IsNullOrEmpty(price);
    }

    public bool CanPurchaseProduct(string productId) =>
        IsSupported
        && HasCatalogProduct(productId)
        && !_isPurchaseInProgress;

    public void MarkIslandOfferShown(int islandIndex)
    {
        if (islandIndex < IslandOfferCatalogSO.MinIslandIndex)
            return;

        if (!_shownIslandOfferIndices.Add(islandIndex))
            return;

        Persist();
        StateChanged?.Invoke();
    }

    public void NotifyIslandBecameAvailable(int islandIndex)
    {
        if (!IsSupported || islandIndex < IslandOfferCatalogSO.MinIslandIndex)
            return;

        if (_purchasedIslandOfferIndices.Contains(islandIndex))
            return;

        if (_catalog?.IslandOffers?.GetOfferForIsland(islandIndex) == null)
            return;

        IslandOfferAvailable?.Invoke(islandIndex);
    }

    public void RefreshCatalog()
    {
#if UNITY_WEBGL
        if (!IsSupported)
            return;

        Bridge.payments.GetCatalog(OnGetCatalogCompleted);
#endif
    }

    private void LoadSales()
    {
#if UNITY_WEBGL
        if (_salesRequested)
            return;

        if (Bridge.instance == null
            || Bridge.platform == null
            || Bridge.remoteConfig == null
            || !string.Equals(Bridge.platform.id, YandexPlatformId, StringComparison.OrdinalIgnoreCase)
            || !Bridge.remoteConfig.isSupported)
        {
            return;
        }

        _salesRequested = true;
        Bridge.remoteConfig.Get(OnSalesLoaded);
#endif
    }

#if UNITY_WEBGL
    private void OnSalesLoaded(bool success, Dictionary<string, string> data)
    {
        if (success && data != null)
        {
            IapSalePercents sales = IapSalePercents.FromFlags(data);
            _removeAdsSalePercent = sales.RemoveAdsPercent;
            _currencySalePercent = sales.CurrencyPercent;
            _starterPackSalePercent = sales.StarterPackPercent;
        }

        Debug.Log(
            $"[IAP] Sales flags: success={success}, removeAds={_removeAdsSalePercent}, currency={_currencySalePercent}, starter={_starterPackSalePercent}");
        SalesUpdated?.Invoke();
    }
#endif

    public void RestorePurchases()
    {
#if UNITY_WEBGL
        if (!IsSupported || _restoreRequested)
            return;

        _restoreRequested = true;
        Bridge.payments.GetPurchases(OnGetPurchasesCompleted);
#endif
    }

    public bool TryPurchaseGemPack(string productId)
    {
        IapGemPackDefinition pack = _catalog?.GetGemPack(productId);
        if (pack == null)
            return false;

        return BeginPurchase(productId);
    }

    public bool TryPurchaseRemoveAds()
    {
        if (!IsRemoveAdsAvailable)
            return false;

        return BeginPurchase(_catalog.RemoveAdsProductId);
    }

    public bool TryPurchaseStarterPack()
    {
        if (!IsStarterPackAvailable)
            return false;

        return BeginPurchase(_catalog.StarterPack.ProductId);
    }

    public bool TryPurchaseIslandOffer(int islandIndex)
    {
        if (!IsSupported || _purchasedIslandOfferIndices.Contains(islandIndex))
            return false;

        IslandOfferDefinition offer = _catalog?.IslandOffers?.GetOfferForIsland(islandIndex);
        if (offer == null || string.IsNullOrEmpty(offer.ProductId))
            return false;

        return BeginPurchase(offer.ProductId);
    }

    public void ApplyFromLoadedSave()
    {
        if (_appliedSave)
            return;

        _appliedSave = true;
        GameSaveData save = _saveLoader.HasLoadedSave ? _saveLoader.LoadedSave : null;
        if (save == null)
            return;

        _adsRemoval.SetAdsRemoved(save.AdsRemoved);
        _starterPackPurchased = save.StarterPackPurchased;
        ReplaceSet(_purchasedIslandOfferIndices, save.PurchasedIslandOfferIndices);
        ReplaceSet(_shownIslandOfferIndices, save.ShownIslandOfferIndices);
        StateChanged?.Invoke();
    }

    public void WriteToSaveData(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        saveData.AdsRemoved = _adsRemoval.AdsRemoved;
        saveData.StarterPackPurchased = _starterPackPurchased;
        saveData.PurchasedIslandOfferIndices = _purchasedIslandOfferIndices.OrderBy(i => i).ToArray();
        saveData.ShownIslandOfferIndices = _shownIslandOfferIndices.OrderBy(i => i).ToArray();
    }

    private bool BeginPurchase(string productId)
    {
#if !UNITY_WEBGL
        return false;
#else
        if (!CanPurchaseProduct(productId))
            return false;

        _isPurchaseInProgress = true;
        PurchaseStarted?.Invoke();
        Bridge.payments.Purchase(productId, OnPurchaseCompleted);
        return true;
#endif
    }

#if UNITY_WEBGL
    private void OnGetCatalogCompleted(bool success, List<Dictionary<string, string>> items)
    {
        _catalogPrices.Clear();
        _catalogCurrencyImages.Clear();
        if (success && items != null)
        {
            foreach (Dictionary<string, string> item in items)
            {
                if (item == null || !item.TryGetValue("id", out string id) || string.IsNullOrEmpty(id))
                    continue;

                // Use only SDK "price" (figure + portal currency name). Do not fall back to
                // priceValue alone or append priceCurrencyCode — that breaks Yandex 1.13.2/1.13.4.
                if (!item.TryGetValue("price", out string priceText) || string.IsNullOrEmpty(priceText))
                {
                    Debug.LogWarning($"[IAP] Catalog item '{id}' has no SDK price string; offer will stay disabled.");
                    continue;
                }

                _catalogPrices[id] = FormatPriceForPlatform(priceText);

                if (item.TryGetValue("priceCurrencyImage", out string currencyImage)
                    && !string.IsNullOrEmpty(currencyImage)
                    && !string.Equals(currencyImage, "null", StringComparison.OrdinalIgnoreCase))
                {
                    _catalogCurrencyImages[id] = currencyImage;
                }
                else
                {
                    Debug.LogWarning($"[IAP] Catalog item '{id}' has no priceCurrencyImage (check Bridge version / platform).");
                }
            }
        }
        else
        {
            Debug.LogWarning("[IAP] GetCatalog failed or returned null.");
        }

        Debug.Log($"[IAP] Catalog ready: success={success}, prices={_catalogPrices.Count}, currencyIcons={_catalogCurrencyImages.Count}");
        _catalogReady = true;
        CatalogUpdated?.Invoke();
    }

    private void OnGetPurchasesCompleted(bool success, List<Dictionary<string, string>> purchases)
    {
        if (!success || purchases == null)
            return;

        bool changed = false;
        foreach (Dictionary<string, string> purchase in purchases)
        {
            if (purchase == null || !purchase.TryGetValue("id", out string id) || string.IsNullOrEmpty(id))
                continue;

            if (FulfillOwnedProduct(id, consumeIfNeeded: true, persist: false))
                changed = true;
        }

        if (changed)
        {
            Persist();
            StateChanged?.Invoke();
        }
    }

    private void OnPurchaseCompleted(bool success, Dictionary<string, string> purchase)
    {
        _isPurchaseInProgress = false;
        PurchaseFinished?.Invoke();

        if (!success)
            return;

        string id = null;
        if (purchase != null)
            purchase.TryGetValue("id", out id);

        if (string.IsNullOrEmpty(id))
            return;

        if (IsConsumableGemPack(id))
        {
            Bridge.payments.ConsumePurchase(id, (consumeSuccess, consumed) =>
            {
                if (!consumeSuccess)
                    return;

                string consumedId = id;
                if (consumed != null && consumed.TryGetValue("id", out string resolved) && !string.IsNullOrEmpty(resolved))
                    consumedId = resolved;

                GrantGemPack(consumedId);
                Persist();
                StateChanged?.Invoke();
            });
            return;
        }

        FulfillOwnedProduct(id, consumeIfNeeded: false, persist: true);
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Playgama.com charges in Gam. The catalog string can still end with USD
    /// (or "Gam"); the shop label on that platform must use the GAM suffix.
    /// </summary>
    private static string FormatPriceForPlatform(string priceText)
    {
        if (string.IsNullOrEmpty(priceText) || !IsPlaygamaPlatform())
            return priceText;

        string trimmed = priceText.TrimEnd();
        if (TryStripCurrencySuffix(trimmed, "USD", out string amount)
            || TryStripCurrencySuffix(trimmed, "GAM", out amount))
        {
            amount = amount.TrimEnd();
            return string.IsNullOrEmpty(amount) ? PlaygamaCurrencySuffix : amount + " " + PlaygamaCurrencySuffix;
        }

        return priceText;
    }

    private static bool IsPlaygamaPlatform()
    {
        if (Bridge.instance == null || Bridge.platform == null)
            return false;

        return string.Equals(Bridge.platform.id, PlaygamaPlatformId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryStripCurrencySuffix(string priceText, string currency, out string amount)
    {
        amount = priceText;
        if (priceText.Length < currency.Length
            || !priceText.EndsWith(currency, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int suffixStart = priceText.Length - currency.Length;
        if (suffixStart > 0 && !char.IsWhiteSpace(priceText[suffixStart - 1]))
            return false;

        amount = priceText.Substring(0, suffixStart);
        return true;
    }
#endif

    private bool FulfillOwnedProduct(string productId, bool consumeIfNeeded, bool persist)
    {
        if (string.IsNullOrEmpty(productId) || _catalog == null)
            return false;

        if (productId == _catalog.RemoveAdsProductId)
        {
            if (_adsRemoval.AdsRemoved)
                return false;

            _adsRemoval.SetAdsRemoved(true);
            if (persist)
                Persist();
            return true;
        }

        if (_catalog.StarterPack != null && productId == _catalog.StarterPack.ProductId)
        {
            if (_starterPackPurchased)
                return false;

            GrantStarterPack();
            _starterPackPurchased = true;
            if (persist)
                Persist();
            return true;
        }

        IslandOfferDefinition islandOffer = _catalog.IslandOffers?.GetOfferByProductId(productId);
        if (islandOffer != null)
        {
            if (_purchasedIslandOfferIndices.Contains(islandOffer.IslandIndex))
                return false;

            GrantIslandOffer(islandOffer);
            _purchasedIslandOfferIndices.Add(islandOffer.IslandIndex);
            _shownIslandOfferIndices.Add(islandOffer.IslandIndex);
            if (persist)
                Persist();
            return true;
        }

        if (consumeIfNeeded && IsConsumableGemPack(productId))
        {
#if UNITY_WEBGL
            // Unconsumed consumable left over from a previous session.
            Bridge.payments.ConsumePurchase(productId, (ok, _) =>
            {
                if (!ok)
                    return;

                GrantGemPack(productId);
                Persist();
                StateChanged?.Invoke();
            });
#endif
            return true;
        }

        return false;
    }

    private bool IsConsumableGemPack(string productId) =>
        _catalog?.GetGemPack(productId) != null;

    private void GrantGemPack(string productId)
    {
        IapGemPackDefinition pack = _catalog?.GetGemPack(productId);
        if (pack == null || pack.GemAmount <= 0)
            return;

        _inventory.AddResourceWithoutLifetime(ResourceType.Gem, pack.GemAmount);
    }

    private void GrantStarterPack()
    {
        StarterPackSO starter = _catalog?.StarterPack;
        if (starter == null)
            return;

        GrantInstrumentLevels(starter);
        GrantResourceRewards(starter.ResourceRewards);
    }

    private void GrantIslandOffer(IslandOfferDefinition offer)
    {
        if (offer == null)
            return;

        if (offer.GemAmount > 0)
            _inventory.AddResourceWithoutLifetime(ResourceType.Gem, offer.GemAmount);

        GrantResourceRewards(offer.ResourceRewards);
    }

    private void GrantResourceRewards(IReadOnlyList<IapResourceReward> rewards)
    {
        if (rewards == null)
            return;

        for (int i = 0; i < rewards.Count; i++)
        {
            IapResourceReward reward = rewards[i];
            if (reward.Amount <= 0)
                continue;

            _inventory.AddResourceWithoutLifetime(reward.Type, reward.Amount);
        }
    }

    private void GrantInstrumentLevels(StarterPackSO starter)
    {
        if (_allInstruments == null || _inventory == null || starter == null)
            return;

        InstrumentType[] types = { InstrumentType.Axe, InstrumentType.Pickaxe, InstrumentType.Sword };

        foreach (InstrumentType type in types)
        {
            Instrument instrument = _inventory.Instruments.FirstOrDefault(item => item.Type == type);
            if (instrument == null)
                continue;

            int currentLevel = instrument.Level;
            int maxLevel = GetMaxInstrumentLevel(type);
            int targetLevel = starter.GetOfferedInstrumentLevel(currentLevel, maxLevel);

            if (targetLevel <= currentLevel)
                continue;

            InstrumentConfig config = _allInstruments.Instruments
                .FirstOrDefault(item => item.Type == type && item.Level == targetLevel);
            if (config == null)
                continue;

            _inventory.UpgradeInstrument(config);
            RefreshInstrumentUpgrader(type);
        }
    }

    private int GetMaxInstrumentLevel(InstrumentType type)
    {
        int max = 0;
        foreach (InstrumentConfig config in _allInstruments.Instruments)
        {
            if (config != null && config.Type == type)
                max = Mathf.Max(max, config.Level);
        }

        return max;
    }

    private static void RefreshInstrumentUpgrader(InstrumentType type)
    {
        InstrumentUpgrader[] upgraders = UnityEngine.Object.FindObjectsByType<InstrumentUpgrader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (InstrumentUpgrader upgrader in upgraders)
        {
            if (upgrader != null && upgrader.InstrumentType == type)
                upgrader.RefreshFromInventory();
        }
    }

    private static bool HasIslandProgress(IslandProgressHandler island)
    {
        if (island == null)
            return false;

        return island.IsFullyOpened
               || island.IsMainPathReached
               || island.GetOpenProgress01() > 0f;
    }

    private void Persist()
    {
        _saveLoader?.Save();
    }

    private static void ReplaceSet(HashSet<int> target, int[] source)
    {
        target.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
            target.Add(source[i]);
    }
}
