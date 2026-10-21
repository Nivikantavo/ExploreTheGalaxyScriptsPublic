using UnityEngine;
using Zenject;

/// <summary>
/// Map object: approach shows a world-space canvas (via <see cref="ApproachUIShower"/>),
/// watch a rewarded ad or spend gems to receive a configured resource.
/// Prefab wiring: trigger + ApproachUIShower → RewardedResourceSpotView (WorldSpaceUI).
/// </summary>
public class RewardedResourceSpot : MonoBehaviour
{
    [SerializeField] private ResourceType _resourceType;
    [SerializeField, Min(1)] private int _resourceAmount = 10;
    [SerializeField] private string _rewardedPlacement = "resource_spot";
    [SerializeField] private Sprite _resourceIconOverride;
    [SerializeField] private bool _oneShot;
    [SerializeField] private ApproachUIShower _approachUIShower;
    [SerializeField] private RewardedResourceSpotView _view;

    private Inventory _inventory;
    private ResourceTypeViews _resourceTypeViews;
    private RewardedAdsController _rewardedAdsController;
    private ISaveLoader _saveLoader;
    private ShopPanelView _shopPanel;
    private bool _claimed;

    [Inject]
    private void Construct(
        Inventory inventory,
        ResourceTypeViews resourceTypeViews,
        RewardedAdsController rewardedAdsController,
        ISaveLoader saveLoader)
    {
        _inventory = inventory;
        _resourceTypeViews = resourceTypeViews;
        _rewardedAdsController = rewardedAdsController;
        _saveLoader = saveLoader;
    }

    private void Awake()
    {
        if (_approachUIShower == null)
            _approachUIShower = GetComponentInChildren<ApproachUIShower>(true);

        if (_view == null)
            _view = GetComponentInChildren<RewardedResourceSpotView>(true);

        if (_shopPanel == null)
            _shopPanel = FindFirstObjectByType<ShopPanelView>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (_view == null)
            return;

        _view.WatchClicked += OnWatchClicked;
        _view.PayClicked += OnPayClicked;
    }

    private void Start()
    {
        RefreshView();
    }

    private void OnDisable()
    {
        if (_view == null)
            return;

        _view.WatchClicked -= OnWatchClicked;
        _view.PayClicked -= OnPayClicked;
    }

    private void RefreshView()
    {
        if (_view == null)
            return;

        Sprite icon = _resourceIconOverride;
        if (icon == null &&
            _resourceTypeViews != null &&
            _resourceTypeViews.Icons != null &&
            _resourceTypeViews.Icons.TryGetValue(_resourceType, out Sprite typedIcon))
        {
            icon = typedIcon;
        }

        _view.Initialize(_resourceType, _resourceAmount, icon);
    }

    private void OnWatchClicked()
    {
        if (_claimed || _inventory == null || _rewardedAdsController == null)
            return;

        string placement = string.IsNullOrWhiteSpace(_rewardedPlacement)
            ? "resource_spot"
            : _rewardedPlacement;

        _rewardedAdsController.TryShowRewarded(placement, OnRewardedCompleted);
    }

    private void OnRewardedCompleted()
    {
        if (_claimed || _inventory == null)
            return;

        _inventory.AddRewardedAdResource(_resourceType, _resourceAmount);
        FinishClaim();
    }

    private void OnPayClicked()
    {
        if (_claimed || _inventory == null)
            return;

        int cost = RewardedResourceSpotView.GetGemCost(_resourceAmount);
        if (_inventory.GetResourceAmount(ResourceType.Gem) < cost)
        {
            OpenGemShop();
            return;
        }

        _inventory.RemoveResource(ResourceType.Gem, cost);
        _inventory.AddResourceWithoutLifetime(_resourceType, _resourceAmount);
        _saveLoader?.Save();
        FinishClaim();
    }

    private void FinishClaim()
    {
        if (!_oneShot)
            return;

        _claimed = true;
        _view?.Hide();

        if (_approachUIShower != null)
            _approachUIShower.enabled = false;

        gameObject.SetActive(false);
    }

    private void OpenGemShop()
    {
        if (_shopPanel == null)
            _shopPanel = FindFirstObjectByType<ShopPanelView>(FindObjectsInactive.Include);

        _shopPanel?.ShowCurrency();
    }
}
