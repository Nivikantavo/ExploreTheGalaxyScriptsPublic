using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[Serializable]
public class IapGemPackDefinition
{
    public string ProductId => _productId;
    public int GemAmount => _gemAmount;
    public Sprite Icon => _icon;

    [SerializeField] private string _productId = "gems_100";
    [SerializeField] private int _gemAmount = 100;
    [SerializeField] private Sprite _icon;
}

[CreateAssetMenu(fileName = "IapCatalogSO", menuName = "IAP/IAP Catalog")]
public class IapCatalogSO : ScriptableObject
{
    public IReadOnlyList<IapGemPackDefinition> GemPacks => _gemPacks;
    public string RemoveAdsProductId => _removeAdsProductId;
    public LocalizedString RemoveAdsTitle => _removeAdsTitle;
    public StarterPackSO StarterPack => _starterPack;
    public IslandOfferCatalogSO IslandOffers => _islandOffers;

    [SerializeField] private List<IapGemPackDefinition> _gemPacks = new();
    [SerializeField] private string _removeAdsProductId = "remove_ads";
    [SerializeField] private LocalizedString _removeAdsTitle;
    [SerializeField] private StarterPackSO _starterPack;
    [SerializeField] private IslandOfferCatalogSO _islandOffers;

    public IapGemPackDefinition GetGemPack(string productId)
    {
        if (_gemPacks == null || string.IsNullOrEmpty(productId))
            return null;

        for (int i = 0; i < _gemPacks.Count; i++)
        {
            IapGemPackDefinition pack = _gemPacks[i];
            if (pack != null && pack.ProductId == productId)
                return pack;
        }

        return null;
    }
}
