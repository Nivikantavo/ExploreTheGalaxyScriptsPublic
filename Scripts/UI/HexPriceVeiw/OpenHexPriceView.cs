using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using UnityEngine;

public class OpenHexPriceView : WorldSpaceUI
{
    [SerializeField] private ItemPriceView _itemPriceViewPrefab;
    [SerializeField] private Transform _itemPriceViewsContainer;
    [SerializeField] private ResourceTypeViews _resourceTypeIcons;
    [SerializeField] private bool _showOnStart = false;
    [SerializeField] private bool _ignorePaymentOpener;
    [SerializeField] private float _heightOffset = 1.5f;
    
    private List<ItemPriceView> _itemPriceViews = new List<ItemPriceView>();
    private ResourcePaymentOpener _openingHexagon;
    private HexTile _parentHexTile;
    private Sprite _requirementIcon;
    private bool _started;
    Vector3 _targetScale;
    

    private void Awake() 
    {
        _targetScale = transform.localScale;
        if (_ignorePaymentOpener)
            return;

        _openingHexagon = GetComponentInParent<ResourcePaymentOpener>(true);
        _parentHexTile = GetComponentInParent<HexTile>(true);
    }

    private void OnEnable() 
    {
        transform.localScale = Vector3.zero;
        if (_parentHexTile != null)
            _parentHexTile.OnUnlockAnimationEnded += UpdatePosition;
    }

    protected override void Start() 
    {
        base.Start();
        _started = true;

        if (_requirementIcon != null)
        {
            PresentRequirement();
            return;
        }

        Initialize(_openingHexagon);
        if (_showOnStart || _parentHexTile == null)
            UpdatePosition();
    }

    private void OnDisable() 
    {
        if (_parentHexTile != null)
            _parentHexTile.OnUnlockAnimationEnded -= UpdatePosition;
        if (_openingHexagon != null)
            _openingHexagon.ValueChanged -= OnValueChanged;
    }

    public void ShowRequirement(Sprite icon)
    {
        _requirementIcon = icon;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (_started)
            PresentRequirement();
    }

    public void Initialize(ResourcePaymentOpener openingHexagon)
    {
        if (openingHexagon == null)
            return;

        openingHexagon.ValueChanged += OnValueChanged;
        foreach(var price in openingHexagon.RemainsToPay)
        {
            ItemPriceView itemPriceView = Instantiate(_itemPriceViewPrefab, _itemPriceViewsContainer);
            itemPriceView.Initialize(price.Key, price.Value, _resourceTypeIcons.Icons[price.Key]);
            _itemPriceViews.Add(itemPriceView);
        }
    }

    private void OnValueChanged(ResourcePaymentOpener openingHexagon)
    {
        foreach(var price in openingHexagon.RemainsToPay)
        {
            ItemPriceView itemPriceView = _itemPriceViews.First(view => view.ResourseType == price.Key);
            itemPriceView.UpdateAmount(price.Value);
        }
    }

    public override void Show()
    {
        gameObject.SetActive(true);
        
        Tween.Scale(transform, _targetScale, 0.2f, Ease.InOutSine);
    }

    public override void Hide()
    {
        gameObject.SetActive(false);
    }

    private void PresentRequirement()
    {
        if (_itemPriceViewPrefab == null || _itemPriceViewsContainer == null)
            return;

        ClearItemViews();

        ItemPriceView itemPriceView = Instantiate(_itemPriceViewPrefab, _itemPriceViewsContainer);
        itemPriceView.ShowIcon(_requirementIcon);
        _itemPriceViews.Add(itemPriceView);
        Show();
    }

    private void ClearItemViews()
    {
        for (int i = 0; i < _itemPriceViews.Count; i++)
        {
            if (_itemPriceViews[i] != null)
                Destroy(_itemPriceViews[i].gameObject);
        }

        _itemPriceViews.Clear();
    }

    private void UpdatePosition()
    {
        if (_openingHexagon == null)
            return;

        Transform target = _openingHexagon.transform;
        if (_openingHexagon.OppeningObjectTransform != null &&
            _openingHexagon.OppeningObjectTransform.gameObject.activeInHierarchy)
        {
            target = _openingHexagon.OppeningObjectTransform;
        }

        transform.position = target.position + Vector3.up * _heightOffset;
        Show();
    }
}
