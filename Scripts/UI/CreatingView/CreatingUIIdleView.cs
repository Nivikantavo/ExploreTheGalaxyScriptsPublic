using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Zenject;

public class CreatingUIIdleView : MonoBehaviour
{
    public event Action OnUpgradeButtonClicked;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private LocalizedString _titleString;
    [SerializeField] private TextMeshProUGUI _itemText;
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private ItemPriceView _itemPriceView;
    [SerializeField] private Transform _itemPriceViewsContainer;
    [SerializeField] private TextMeshProUGUI _processTimeText;
    [SerializeField] private Slider _countSlider;

    private ResourceTypeViews _resourceTypeIcons;
    private AllResoursesConfig _allResoursesConfig;
    private List<ItemPriceView> _itemPriceViews = new List<ItemPriceView>();
    private CreatingData _data;

    [Inject]
    protected virtual void Construct(ResourceTypeViews resourceTypeViews, AllResoursesConfig allResoursesConfig)
    {
        _resourceTypeIcons = resourceTypeViews;
        _allResoursesConfig = allResoursesConfig;
    }

    public virtual void Initialize(CreatingData data)
    {
        UnsubscribeDataEvents();

        _upgradeButton.onClick.RemoveListener(UpgradeButtonClicked);

        _data = data;
        var resultConfig = _allResoursesConfig.Resourses.FirstOrDefault(res => res.ResourseType == data.ResultResourceType);
        string name = resultConfig != null ? resultConfig.Name : string.Empty;
        _titleText.text = $"{LocalizationSafe.Get(_titleString)} {name}";
        _itemIcon.sprite = data.ResourceIcon;

        if (_itemPriceViews.Count > 0)
        {
            _itemPriceViews[0].Initialize(data.RequestedResourceType, data.RequestedResourceCount, _resourceTypeIcons.Icons[data.RequestedResourceType]);
        }
        else
        {
            ItemPriceView itemPriceView = Instantiate(_itemPriceView, _itemPriceViewsContainer);
            itemPriceView.Initialize(data.RequestedResourceType, data.RequestedResourceCount, _resourceTypeIcons.Icons[data.RequestedResourceType]);
            _itemPriceViews.Add(itemPriceView);
        }

        _upgradeButton.onClick.AddListener(UpgradeButtonClicked);
        SubscribeDataEvents();

        SetMaxValue();
    }

    private void OnEnable()
    {
        _countSlider.onValueChanged.AddListener(OnSliderValueChange);
        SubscribeDataEvents();
    }

    private void OnDisable()
    {
        _countSlider.onValueChanged.RemoveListener(OnSliderValueChange);
        UnsubscribeDataEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeDataEvents();
    }

    private void SubscribeDataEvents()
    {
        if (_data == null)
            return;

        _data.OnRequestedResourceCountChanged -= SetMaxValue;
        _data.OnRequestedResourceCountChanged += SetMaxValue;
    }

    private void UnsubscribeDataEvents()
    {
        if (_data == null)
            return;

        _data.OnRequestedResourceCountChanged -= SetMaxValue;
    }

    private void UpdateView()
    {
        _itemText.text = CompactNumberFormatter.Format(_data.ResourceCount);
        _itemPriceViews[0].UpdateAmount(_data.RequestedResourceCount);
        _itemPriceViews[0].SetAvailable(_data.MaxRequestedCount >= _data.RequestedResourceCount);
        _processTimeText.text = $"{_data.TimeForCreatingAll / 60}:{_data.TimeForCreatingAll % 60}";
    }

    private void SetMaxValue()
    {
        if (!isActiveAndEnabled || _data == null || _data.IsCountLocked)
            return;

        int maxCraftable = _data.RequestedForResult > 0
            ? _data.MaxRequestedCount / _data.RequestedForResult
            : 0;

        _countSlider.maxValue = Mathf.Max(0, maxCraftable);

        int targetCount = _data.ResourceCount;
        if (targetCount <= 0 || targetCount > maxCraftable)
            targetCount = maxCraftable / 2;

        _countSlider.SetValueWithoutNotify(targetCount);
        _data.ResourceCount = targetCount;
        UpdateView();
    }

    private void OnSliderValueChange(float newValue)
    {
        if (_data == null || _data.IsCountLocked)
            return;

        _data.ResourceCount = (int)newValue;
        UpdateView();
    }

    protected virtual void UpgradeButtonClicked()
    {
        OnUpgradeButtonClicked?.Invoke();
    }
}
