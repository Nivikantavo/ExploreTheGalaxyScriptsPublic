/// <summary>
/// Shared rules for which resources may appear in shop resource offer tabs.
/// Key and SourceCore are mine-only and never sold via shop.
/// Other resources appear only after the player has obtained them at least once.
/// </summary>
public static class ShopResourceOfferRules
{
    public static bool IsExcludedFromShopOffers(ResourceType type)
    {
        return type == ResourceType.Gem
               || type == ResourceType.Key
               || type == ResourceType.SourceCore;
    }

    public static bool IsUnlockedForShop(Inventory inventory, ResourceType type)
    {
        if (inventory == null || IsExcludedFromShopOffers(type))
            return false;

        return inventory.GetLifetimeAmount(type) > 0;
    }
}
