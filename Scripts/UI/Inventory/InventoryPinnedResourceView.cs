using UnityEngine;
using Zenject;

public class InventoryPinnedResourceView : MonoBehaviour
{
    [SerializeField] private ResourceCellView _cellView;
    [SerializeField] private ResourceType _resourceType = ResourceType.Gem;

    private Inventory _inventory;
    private ResourseConfig _config;
    private bool _initialized;

    [Inject]
    private void Construct(Inventory inventory, AllResoursesConfig allResoursesConfig)
    {
        _inventory = inventory;
        _config = FindConfig(allResoursesConfig, _resourceType);
        _inventory.CellChanged += OnCellChanged;
    }

    private void Awake()
    {
        if (_cellView == null)
            _cellView = GetComponent<ResourceCellView>();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (_inventory != null)
            _inventory.CellChanged -= OnCellChanged;
    }

    private void OnCellChanged(ResourceCell cell)
    {
        if (cell != null && cell.ResourseType == _resourceType)
            Refresh();
    }

    private void Refresh()
    {
        if (_cellView == null)
            _cellView = GetComponent<ResourceCellView>();

        if (_cellView == null || _inventory == null)
            return;

        int amount = _inventory.GetResourceAmount(_resourceType);

        if (!_initialized)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[InventoryPinnedResourceView] Missing config for {_resourceType}");
                return;
            }

            _cellView.Initialize(_config, amount);
            _initialized = true;
            return;
        }

        if (_cellView.Amount != amount)
            _cellView.UpdateAmount(amount);
    }

    private static ResourseConfig FindConfig(AllResoursesConfig allResoursesConfig, ResourceType type)
    {
        if (allResoursesConfig?.Resourses == null)
            return null;

        for (int i = 0; i < allResoursesConfig.Resourses.Count; i++)
        {
            ResourseConfig config = allResoursesConfig.Resourses[i];
            if (config != null && config.ResourseType == type)
                return config;
        }

        return null;
    }
}
