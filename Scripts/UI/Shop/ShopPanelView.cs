using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;

public class ShopPanelView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _gemBalanceText;

    [Header("Tabs")]
    [SerializeField] private ShopTabBarView _tabBar;
    [SerializeField] private Button _currencyTabButton;
    [SerializeField] private Button _removeAdsTabButton;
    [SerializeField] private Button _resourcesTabButton;
    [SerializeField] private Button _adsResourcesTabButton;
    [SerializeField] private GameObject _currencyTabRoot;
    [SerializeField] private GameObject _removeAdsTabRoot;
    [SerializeField] private GameObject _resourcesTabRoot;
    [SerializeField] private GameObject _adsResourcesTabRoot;

    [Header("Tab Content")]
    [SerializeField] private ShopCurrencyTabView _currencyTab;
    [SerializeField] private ShopRemoveAdsTabView _removeAdsTab;
    [SerializeField] private ShopResourceExchangeView _resourcesTab;
    [SerializeField] private ShopRewardedResourcesTabView _adsResourcesTab;

    private IapService _iapService;
    private Inventory _inventory;
    private ISaveLoader _saveLoader;
    private TutorialHandler _tutorialHandler;
    private int _activeTabIndex;
    private bool _isVisible;
    private Coroutine _layoutRebuildRoutine;
    private ScrollRect _shopScroll;

    [Inject]
    private void Construct(IapService iapService, Inventory inventory, ISaveLoader saveLoader)
    {
        _iapService = iapService;
        _inventory = inventory;
        _saveLoader = saveLoader;
    }

    private void Awake()
    {
        HideImmediate();
        ShowTab(0);
        _tutorialHandler = TutorialHudAccess.FindTutorialHandler();
    }

    private void OnEnable()
    {
        if (_tutorialHandler == null)
            _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved += RefreshAvailability;

        if (_openButton != null)
            _openButton.onClick.AddListener(Show);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_currencyTabButton != null)
            _currencyTabButton.onClick.AddListener(ShowCurrencyTab);

        if (_removeAdsTabButton != null)
            _removeAdsTabButton.onClick.AddListener(ShowRemoveAdsTab);

        if (_resourcesTabButton != null)
            _resourcesTabButton.onClick.AddListener(ShowResourcesTab);

        if (_adsResourcesTabButton != null)
            _adsResourcesTabButton.onClick.AddListener(ShowAdsResourcesTab);

        if (_iapService != null)
        {
            _iapService.StateChanged += Refresh;
            _iapService.CatalogUpdated += Refresh;
            _iapService.SalesUpdated += Refresh;
            _iapService.PurchaseStarted += OnPurchaseStarted;
            _iapService.PurchaseFinished += OnPurchaseFinished;
        }

        if (_inventory != null)
            _inventory.CellChanged += OnInventoryCellChanged;

        RefreshAvailability();
        Refresh();
    }

    private void Start()
    {
        RefreshAvailability();
        Refresh();
    }

    private void OnDisable()
    {
        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved -= RefreshAvailability;

        if (_openButton != null)
            _openButton.onClick.RemoveListener(Show);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);

        if (_currencyTabButton != null)
            _currencyTabButton.onClick.RemoveListener(ShowCurrencyTab);

        if (_removeAdsTabButton != null)
            _removeAdsTabButton.onClick.RemoveListener(ShowRemoveAdsTab);

        if (_resourcesTabButton != null)
            _resourcesTabButton.onClick.RemoveListener(ShowResourcesTab);

        if (_adsResourcesTabButton != null)
            _adsResourcesTabButton.onClick.RemoveListener(ShowAdsResourcesTab);

        if (_iapService != null)
        {
            _iapService.StateChanged -= Refresh;
            _iapService.CatalogUpdated -= Refresh;
            _iapService.SalesUpdated -= Refresh;
            _iapService.PurchaseStarted -= OnPurchaseStarted;
            _iapService.PurchaseFinished -= OnPurchaseFinished;
        }

        if (_inventory != null)
            _inventory.CellChanged -= OnInventoryCellChanged;

        StopLayoutRebuild();
        if (_isVisible)
        {
            GameplayPause.Pop();
            _isVisible = false;
        }
    }

    public void Show()
    {
        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
            return;

        if (_iapService == null || !_iapService.IsSupported)
            return;

        // Re-fetch platform prices/currency icons (covers first open + catalog failure recovery).
        _iapService.RefreshCatalog();
        Refresh();

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        bool wasVisible = _isVisible;
        _isVisible = true;
        ScheduleShopLayoutRebuild(resetScroll: true);
        if (!wasVisible)
            GameplayPause.Push();
    }

    public void ShowCurrency()
    {
        ShowTab(0);
        Show();
    }

    public void Hide()
    {
        HideImmediate();
        if (_isVisible)
            GameplayPause.Pop();
        _isVisible = false;
    }

    private void HideImmediate()
    {
        StopLayoutRebuild();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void ShowCurrencyTab() => ShowTab(0);

    private void ShowRemoveAdsTab() => ShowTab(1);

    private void ShowResourcesTab() => ShowTab(2);

    private void ShowAdsResourcesTab() => ShowTab(3);

    private void ShowTab(int tabIndex)
    {
        _activeTabIndex = Mathf.Clamp(tabIndex, 0, 3);

        SetTabActive(_currencyTabRoot, _activeTabIndex == 0);
        SetTabActive(_removeAdsTabRoot, _activeTabIndex == 1);
        SetTabActive(_resourcesTabRoot, _activeTabIndex == 2);
        SetTabActive(_adsResourcesTabRoot, _activeTabIndex == 3);

        RefreshTabButtonStates();
        Refresh();
        ScheduleShopLayoutRebuild(resetScroll: true);
    }

    private static void SetTabActive(GameObject root, bool active)
    {
        if (root != null)
            root.SetActive(active);
    }

    private void RefreshTabButtonStates()
    {
        _tabBar?.SetSelected(_activeTabIndex);
    }

    private void RefreshAvailability()
    {
        bool supported = _iapService != null && _iapService.IsSupported;
        bool tutorialUnlocked = TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler);
        bool available = supported && tutorialUnlocked;

        if (_openButton != null)
            _openButton.gameObject.SetActive(available);

        if (!available && _isVisible)
            Hide();
    }

    private void Refresh()
    {
        RefreshAvailability();
        RefreshGemBalance();

        switch (_activeTabIndex)
        {
            case 0:
                _currencyTab?.Refresh();
                break;
            case 1:
                _removeAdsTab?.Refresh();
                break;
            case 2:
                _resourcesTab?.Refresh();
                break;
            case 3:
                _adsResourcesTab?.Refresh();
                break;
        }

        if (_isVisible)
            ScheduleShopLayoutRebuild(resetScroll: false);
    }

    private void ScheduleShopLayoutRebuild(bool resetScroll)
    {
        if (!_isVisible)
            return;

        RebuildShopLayout(resetScroll);

        if (!isActiveAndEnabled)
            return;

        StopLayoutRebuild();
        _layoutRebuildRoutine = StartCoroutine(RebuildShopLayoutNextFrame(resetScroll));
    }

    private IEnumerator RebuildShopLayoutNextFrame(bool resetScroll)
    {
        yield return null;
        RebuildShopLayout(resetScroll);
        yield return new WaitForEndOfFrame();
        RebuildShopLayout(resetScroll);
        _layoutRebuildRoutine = null;
    }

    private void RebuildShopLayout(bool resetScroll)
    {
        RectTransform content = ResolveScrollContent();
        if (content == null || !content.gameObject.activeInHierarchy)
            return;

        Canvas.ForceUpdateCanvases();
        RebuildNestedLayout(content);
        FitContentToActiveTab(content);

        if (resetScroll)
            ResetScrollToTop();
    }

    private static void RebuildNestedLayout(RectTransform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            RebuildNestedLayout(child);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    private void FitContentToActiveTab(RectTransform content)
    {
        RectTransform tab = ResolveActiveTabRect();
        if (tab == null)
            return;

        VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
        float padding = 0f;
        if (group != null)
            padding = group.padding.top + group.padding.bottom;

        float tabHeight = tab.rect.height;
        if (tabHeight <= 1f)
            return;

        float targetHeight = tabHeight + padding;
        LayoutElement layoutElement = content.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = content.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = -1f;
        layoutElement.preferredHeight = targetHeight;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
    }

    private void ResetScrollToTop()
    {
        if (_shopScroll == null)
        {
            RectTransform content = ResolveScrollContent();
            if (content != null)
                _shopScroll = content.GetComponentInParent<ScrollRect>();
        }

        if (_shopScroll != null)
            _shopScroll.verticalNormalizedPosition = 1f;
    }

    private RectTransform ResolveScrollContent()
    {
        Transform tabRoot = ResolveActiveTabRoot();
        if (tabRoot == null)
            tabRoot = _currencyTabRoot != null ? _currencyTabRoot.transform : null;

        return tabRoot != null ? tabRoot.parent as RectTransform : null;
    }

    private Transform ResolveActiveTabRoot()
    {
        return _activeTabIndex switch
        {
            1 => _removeAdsTabRoot != null ? _removeAdsTabRoot.transform : null,
            2 => _resourcesTabRoot != null ? _resourcesTabRoot.transform : null,
            3 => _adsResourcesTabRoot != null ? _adsResourcesTabRoot.transform : null,
            _ => _currencyTabRoot != null ? _currencyTabRoot.transform : null,
        };
    }

    private RectTransform ResolveActiveTabRect()
    {
        return ResolveActiveTabRoot() as RectTransform;
    }

    private void StopLayoutRebuild()
    {
        if (_layoutRebuildRoutine == null)
            return;

        StopCoroutine(_layoutRebuildRoutine);
        _layoutRebuildRoutine = null;
    }

    private void RefreshGemBalance()
    {
        if (_gemBalanceText == null || _inventory == null)
            return;

        _gemBalanceText.text = _inventory.GetResourceAmount(ResourceType.Gem).ToString();
    }

    private void OnPurchaseStarted() => Refresh();

    private void OnPurchaseFinished() => Refresh();

    private void OnInventoryCellChanged(ResourceCell cell)
    {
        if (cell != null && cell.ResourseType == ResourceType.Gem)
            RefreshGemBalance();
    }
}
