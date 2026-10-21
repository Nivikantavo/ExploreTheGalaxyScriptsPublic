using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopRewardedResourceOffer
{
    public ResourceType ResourceType;
    [Min(1)] public int Amount;
    public string Placement;
}

[CreateAssetMenu(fileName = "ShopRewardedResourceCatalog", menuName = "Shop/Rewarded Resource Catalog")]
public class ShopRewardedResourceCatalogSO : ScriptableObject
{
    public const string DefaultPlacement = "shop_resource";

    [SerializeField] private List<ShopRewardedResourceOffer> _offers = new()
    {
        new ShopRewardedResourceOffer { ResourceType = ResourceType.Wood, Amount = 200, Placement = DefaultPlacement },
        new ShopRewardedResourceOffer { ResourceType = ResourceType.Rock, Amount = 200, Placement = DefaultPlacement },
        new ShopRewardedResourceOffer { ResourceType = ResourceType.Plank, Amount = 100, Placement = DefaultPlacement },
        new ShopRewardedResourceOffer { ResourceType = ResourceType.Meat, Amount = 100, Placement = DefaultPlacement },
        new ShopRewardedResourceOffer { ResourceType = ResourceType.Iron, Amount = 50, Placement = DefaultPlacement },
        new ShopRewardedResourceOffer { ResourceType = ResourceType.Gold, Amount = 50, Placement = DefaultPlacement },
    };

    public IReadOnlyList<ShopRewardedResourceOffer> Offers => _offers;

    public static string ResolvePlacement(ShopRewardedResourceOffer offer)
    {
        return string.IsNullOrWhiteSpace(offer.Placement) ? DefaultPlacement : offer.Placement;
    }
}
