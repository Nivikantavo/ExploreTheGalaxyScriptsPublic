using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[Serializable]
public class IslandOfferDefinition
{
    public int IslandIndex => _islandIndex;
    public string ProductId => _productId;
    public LocalizedString Title => _title;
    public int GemAmount => _gemAmount;
    public IReadOnlyList<IapResourceReward> ResourceRewards => _resourceRewards;

    [SerializeField] private int _islandIndex = 3;
    [SerializeField] private string _productId = "island_offer_3";
    [SerializeField] private LocalizedString _title;
    [SerializeField] private int _gemAmount = 50;
    [SerializeField] private List<IapResourceReward> _resourceRewards = new();
}

[CreateAssetMenu(fileName = "IslandOfferCatalogSO", menuName = "IAP/Island Offer Catalog")]
public class IslandOfferCatalogSO : ScriptableObject
{
    public const int MinIslandIndex = 3;

    public IReadOnlyList<IslandOfferDefinition> Offers => _offers;

    [SerializeField] private List<IslandOfferDefinition> _offers = new();

    public IslandOfferDefinition GetOfferForIsland(int islandIndex)
    {
        if (_offers == null || islandIndex < MinIslandIndex)
            return null;

        for (int i = 0; i < _offers.Count; i++)
        {
            IslandOfferDefinition offer = _offers[i];
            if (offer != null && offer.IslandIndex == islandIndex)
                return offer;
        }

        return null;
    }

    public IslandOfferDefinition GetOfferByProductId(string productId)
    {
        if (_offers == null || string.IsNullOrEmpty(productId))
            return null;

        for (int i = 0; i < _offers.Count; i++)
        {
            IslandOfferDefinition offer = _offers[i];
            if (offer != null && offer.ProductId == productId)
                return offer;
        }

        return null;
    }
}
