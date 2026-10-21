using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Zenject;

public class InstrumentUpgraderIdleView : MonoBehaviour
{
    public event Action OnUpgradeButtonClicked;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private LocalizedString _titleString;
    [SerializeField] private TextMeshProUGUI _nextLevelText;
    [SerializeField] private LocalizedString _nextLevelString;
    [SerializeField] private Button _upgradeButton;
    [SerializeField] private Image _nextLevelIcon;
    [SerializeField] private ItemPriceView _itemPriceView;
    [SerializeField] private Transform _itemPriceViewsContainer;
    [SerializeField] private TextMeshProUGUI _upgradeTimeText;
    private ResourceTypeViews _resourceTypeIcons;
    private InstrumentUpgraderData _data;
    private List<ItemPriceView> _itemPriceViews = new List<ItemPriceView>();

    [Inject]
    private void Construct(ResourceTypeViews resourceTypeViews)
    {
        _resourceTypeIcons = resourceTypeViews;
    }

    private void OnDestroy() 
    {
        if (_data != null)
            _data.PlayerEnter -= UpdateResourceAvailavle;
    }

    public void Initialize(InstrumentUpgraderData data)
    {
        if (_data != null)
            _data.PlayerEnter -= UpdateResourceAvailavle;

        _data = data;
        _titleText.text = $"{LocalizationSafe.Get(_titleString)} {data.NextLevel}";
        _nextLevelText.text = $"{LocalizationSafe.Get(_nextLevelString)} {data.NextLevel}";
        _nextLevelIcon.sprite = data.NextLevelIcon;

        int index = 0;
        if (data.Prices != null)
        {
            foreach(var price in data.Prices)
            {
                if(index < _itemPriceViews.Count)
                {
                    _itemPriceViews[index].gameObject.SetActive(true);
                    _itemPriceViews[index].Initialize(price.Key, price.Value, _resourceTypeIcons.Icons[price.Key]);
                }
                else
                {
                    ItemPriceView itemPriceView = Instantiate(_itemPriceView, _itemPriceViewsContainer);
                    itemPriceView.Initialize(price.Key, price.Value, _resourceTypeIcons.Icons[price.Key]);
                    _itemPriceViews.Add(itemPriceView);
                }
                index++;
            }
        }

        for (int i = index; i < _itemPriceViews.Count; i++)
            _itemPriceViews[i].gameObject.SetActive(false);

        _upgradeTimeText.text = $"{data.UpgradeTime/60}:{data.UpgradeTime%60}";
        _upgradeButton.onClick.RemoveListener(UpgradeButtonClicked);
        _upgradeButton.onClick.AddListener(UpgradeButtonClicked);
        _data.PlayerEnter += UpdateResourceAvailavle;
    }

    private void UpgradeButtonClicked()
    {
        OnUpgradeButtonClicked?.Invoke();
    }

    private void UpdateResourceAvailavle(Dictionary<ResourceType, bool> availables)
    {
        foreach(var price in _itemPriceViews)
        {
            if (availables != null && availables.TryGetValue(price.ResourseType, out bool available))
                price.SetAvailable(available);
        }
    }
}
