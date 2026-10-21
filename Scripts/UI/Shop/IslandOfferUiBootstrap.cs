using System.Reflection;
using UnityEngine;
using Zenject;

public class IslandOfferUiBootstrap : IInitializable
{
    private readonly DiContainer _container;
    private readonly ResourceCellView _resourceCellPrefab;

    public IslandOfferUiBootstrap(DiContainer container, [InjectOptional] ResourceCellView resourceCellPrefab)
    {
        _container = container;
        _resourceCellPrefab = resourceCellPrefab;
    }

    public void Initialize()
    {
        if (Object.FindFirstObjectByType<IslandOfferPopupView>(FindObjectsInactive.Include) != null)
            return;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("IslandOfferUiBootstrap: Canvas not found; Island Offer UI was not created.");
            return;
        }

        ResourceCellView prefab = ResolveResourceCellPrefab();
        if (prefab == null)
            Debug.LogWarning("IslandOfferUiBootstrap: ResourceCellView prefab is missing; resource rewards will not render.");

        IslandOfferPopupView view = IslandOfferUiBuilder.BuildUnder(canvas.transform, prefab);
        view.gameObject.SetActive(false);
        _container.InjectGameObject(view.gameObject);
        view.gameObject.SetActive(true);
    }

    private ResourceCellView ResolveResourceCellPrefab()
    {
        if (_resourceCellPrefab != null)
            return _resourceCellPrefab;

        ResourceCellView loaded = IslandOfferUiBuilder.LoadResourceCellPrefab();
        if (loaded != null)
            return loaded;

        return TryGetPrefabFromInventoryView();
    }

    private static ResourceCellView TryGetPrefabFromInventoryView()
    {
        InventoryView inventoryView = Object.FindFirstObjectByType<InventoryView>(FindObjectsInactive.Include);
        if (inventoryView == null)
            return null;

        FieldInfo field = typeof(InventoryView).GetField(
            "_resourceCellViewPrefab",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(inventoryView) as ResourceCellView;
    }
}
