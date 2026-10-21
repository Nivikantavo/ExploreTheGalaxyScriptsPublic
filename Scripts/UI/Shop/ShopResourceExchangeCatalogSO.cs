using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopResourceExchangeOffer
{
    public ResourceType ResourceType;
    [Min(1)] public int Amount;
    [Min(1)] public int GemCost;
}

[CreateAssetMenu(fileName = "ShopResourceExchangeCatalog", menuName = "Shop/Resource Exchange Catalog")]
public class ShopResourceExchangeCatalogSO : ScriptableObject
{
    [SerializeField] private List<ShopResourceExchangeOffer> _offers = new()
    {
        new ShopResourceExchangeOffer { ResourceType = ResourceType.Wood, Amount = 250, GemCost = 1 },
        new ShopResourceExchangeOffer { ResourceType = ResourceType.Rock, Amount = 250, GemCost = 1 },
        new ShopResourceExchangeOffer { ResourceType = ResourceType.Plank, Amount = 100, GemCost = 1 },
        new ShopResourceExchangeOffer { ResourceType = ResourceType.Meat, Amount = 100, GemCost = 1 },
        new ShopResourceExchangeOffer { ResourceType = ResourceType.Iron, Amount = 100, GemCost = 1 },
        new ShopResourceExchangeOffer { ResourceType = ResourceType.Gold, Amount = 100, GemCost = 1 },
    };

    public IReadOnlyList<ShopResourceExchangeOffer> Offers => _offers;
}
