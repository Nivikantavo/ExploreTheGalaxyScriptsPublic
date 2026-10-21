using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class InventoryLastAddedView : MonoBehaviour
{
    [SerializeField] private Transform _itemsContainer;
    [SerializeField] private ResourceCellView _cellViewPrefab;
    [SerializeField] private int _containerLimit;

    private Inventory _inventory;
    private Dictionary<ResourceType, ResourseConfig> _configsByType;
    private Dictionary<ResourceType, ResourceCellView> _viewsByType;

    [Inject]
    private void Construct(Inventory inventory, AllResoursesConfig allResoursesConfig)
    {
        _inventory = inventory;
        _configsByType = new Dictionary<ResourceType, ResourseConfig>(allResoursesConfig.Resourses.Count);
        foreach (ResourseConfig config in allResoursesConfig.Resourses)
        {
            _configsByType[config.ResourseType] = config;
        }

        _viewsByType = new Dictionary<ResourceType, ResourceCellView>(_containerLimit);
        _inventory.CellChanged += OnInventoryCellChanged;
    }

    private void OnDestroy()
    {
        _inventory.CellChanged -= OnInventoryCellChanged;
    }

    public void Initialize()
    {
        int limit = Mathf.Min(_containerLimit, _inventory.Cells.Count);

        for (int i = 0; i < limit; i++)
        {
            ResourceCell cell = _inventory.Cells[i];
            ResourceCellView cellView = Instantiate(_cellViewPrefab, _itemsContainer);
            cellView.Initialize(_configsByType[cell.ResourseType], cell.Amount);
            _viewsByType[cell.ResourseType] = cellView;
        }
    }

    private void OnInventoryCellChanged(ResourceCell cell)
    {
        if (_viewsByType.TryGetValue(cell.ResourseType, out ResourceCellView view))
        {
            if (view.Amount != cell.Amount)
            {
                view.UpdateAmount(cell.Amount);
            }

            view.transform.SetAsFirstSibling();
            return;
        }

        if (_viewsByType.Count >= _containerLimit)
        {
            view = _itemsContainer.GetChild(_itemsContainer.childCount - 1).GetComponent<ResourceCellView>();
            _viewsByType.Remove(view.ResourseType);
        }
        else
        {
            view = Instantiate(_cellViewPrefab, _itemsContainer);
        }

        view.Initialize(_configsByType[cell.ResourseType], cell.Amount);
        _viewsByType[cell.ResourseType] = view;
        view.transform.SetAsFirstSibling();
    }
}
