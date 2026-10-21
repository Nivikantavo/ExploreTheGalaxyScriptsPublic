using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class IslandOfferPopupView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private Image _currencyImage;
    [SerializeField] private TMP_Text _gemsText;
    [SerializeField] private Transform _resourcesRoot;
    [SerializeField] private ResourceCellView _resourceCellViewPrefab;
    [SerializeField] private bool _autoShowOnIslandUnlock = true;

    private readonly List<ResourceCellView> _spawnedResourceCells = new();

    private IapService _iapService;
    private AllResoursesConfig _allResourses;
    private ISaveLoader _saveLoader;
    private TutorialHandler _tutorialHandler;
    private IslandOfferDefinition _currentOffer;
    private bool _isVisible;
    private Coroutine _currencyLoadRoutine;
    private string _pendingCurrencyImageUrl;

    public void Configure(
        GameObject panelRoot,
        Button openButton,
        Button closeButton,
        Button buyButton,
        TMP_Text titleText,
        TMP_Text priceText,
        Image currencyImage,
        TMP_Text gemsText,
        Transform resourcesRoot,
        ResourceCellView resourceCellViewPrefab,
        bool autoShowOnIslandUnlock)
    {
        _panelRoot = panelRoot;
        _openButton = openButton;
        _closeButton = closeButton;
        _buyButton = buyButton;
        _titleText = titleText;
        _priceText = priceText;
        _currencyImage = currencyImage;
        _gemsText = gemsText;
        _resourcesRoot = resourcesRoot;
        _resourceCellViewPrefab = resourceCellViewPrefab;
        _autoShowOnIslandUnlock = autoShowOnIslandUnlock;
    }

    [Inject]
    private void Construct(IapService iapService, AllResoursesConfig allResourses, ISaveLoader saveLoader)
    {
        _iapService = iapService;
        _allResourses = allResourses;
        _saveLoader = saveLoader;
    }

    private void Awake()
    {
        HideImmediate();
        _tutorialHandler = TutorialHudAccess.FindTutorialHandler();
    }

    private void OnEnable()
    {
        if (_tutorialHandler == null)
            _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved += Refresh;

        if (_openButton != null)
            _openButton.onClick.AddListener(ShowActiveOffer);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_buyButton != null)
            _buyButton.onClick.AddListener(OnBuyClicked);

        if (_iapService != null)
        {
            _iapService.StateChanged += Refresh;
            _iapService.CatalogUpdated += Refresh;
            _iapService.IslandOfferAvailable += OnIslandOfferAvailable;
            _iapService.PurchaseStarted += Refresh;
            _iapService.PurchaseFinished += Refresh;
        }

        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved -= Refresh;

        if (_openButton != null)
            _openButton.onClick.RemoveListener(ShowActiveOffer);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);

        if (_buyButton != null)
            _buyButton.onClick.RemoveListener(OnBuyClicked);

        if (_iapService != null)
        {
            _iapService.StateChanged -= Refresh;
            _iapService.CatalogUpdated -= Refresh;
            _iapService.IslandOfferAvailable -= OnIslandOfferAvailable;
            _iapService.PurchaseStarted -= Refresh;
            _iapService.PurchaseFinished -= Refresh;
        }

        StopCurrencyLoad();
    }

    public void ShowActiveOffer()
    {
        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
            return;

        if (_iapService == null || !_iapService.IsSupported)
            return;

        IslandOfferDefinition offer = _iapService.GetActiveIslandOffer();
        if (offer == null)
            return;

        ShowOffer(offer, markShown: true);
    }

    public void ShowOffer(IslandOfferDefinition offer, bool markShown)
    {
        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
            return;

        if (offer == null || _iapService == null || !_iapService.IsSupported)
            return;

        _currentOffer = offer;
        if (markShown)
            _iapService.MarkIslandOfferShown(offer.IslandIndex);

        BindOffer(offer);

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        if (!_isVisible)
            GameplayPause.Push();
        _isVisible = true;
        RefreshOpenButton();
    }

    public void Hide()
    {
        HideImmediate();
        if (_isVisible)
            GameplayPause.Pop();
        _isVisible = false;
        RefreshOpenButton();
    }

    private void HideImmediate()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void OnIslandOfferAvailable(int islandIndex)
    {
        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
        {
            RefreshOpenButton();
            return;
        }

        TryAutoShowIslandOffer(islandIndex);
    }

    private void Refresh()
    {
        RefreshOpenButton();

        if (_isVisible && _currentOffer != null)
        {
            if (_iapService != null && _iapService.IsIslandOfferPurchased(_currentOffer.IslandIndex))
            {
                Hide();
                return;
            }

            BindOffer(_currentOffer);
        }
    }

    private void RefreshOpenButton()
    {
        bool supported = _iapService != null && _iapService.IsSupported;
        bool tutorialUnlocked = TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler);
        IslandOfferDefinition active = supported ? _iapService.GetActiveIslandOffer() : null;
        bool showButton = supported && tutorialUnlocked && active != null;

        if (_openButton != null)
            _openButton.gameObject.SetActive(showButton);
    }

    private void TryAutoShowIslandOffer(int islandIndex)
    {
        if (!_autoShowOnIslandUnlock || _iapService == null)
        {
            RefreshOpenButton();
            return;
        }

        if (_iapService.WasIslandOfferShown(islandIndex))
        {
            RefreshOpenButton();
            return;
        }

        IslandOfferDefinition offer = _iapService.GetActiveIslandOffer();
        if (offer == null || offer.IslandIndex != islandIndex)
            offer = _iapService.GetIslandOffer(islandIndex);

        if (offer == null)
        {
            RefreshOpenButton();
            return;
        }

        ShowOffer(offer, markShown: true);
    }

    private void BindOffer(IslandOfferDefinition offer)
    {
        if (offer == null)
            return;

        if (_titleText != null)
            _titleText.text = SafeLocalized(offer.Title);

        string price = _iapService != null ? _iapService.GetPriceLabel(offer.ProductId) : string.Empty;
        if (_priceText != null)
            _priceText.text = price;

        string currencyImageUrl = _iapService != null
            ? _iapService.GetPriceCurrencyImageUrl(offer.ProductId)
            : string.Empty;
        SetCurrencyImageUrl(currencyImageUrl);

        if (_gemsText != null)
            _gemsText.text = offer.GemAmount > 0 ? $"+{offer.GemAmount}" : string.Empty;

        BindResourceRewards(offer.ResourceRewards);

        if (_buyButton != null)
        {
            bool canBuy = _iapService != null
                && !_iapService.IsIslandOfferPurchased(offer.IslandIndex)
                && _iapService.CanPurchaseProduct(offer.ProductId);
            _buyButton.interactable = canBuy;
        }
    }

    private void SetCurrencyImageUrl(string url)
    {
        StopCurrencyLoad();
        _pendingCurrencyImageUrl = url;

        if (_currencyImage == null)
            return;

        _currencyImage.gameObject.SetActive(false);

        if (string.IsNullOrEmpty(url))
            return;

        if (PlatformCurrencyIconCache.TryGet(url, out Sprite cached) && cached != null)
        {
            ApplyCurrencySprite(cached);
            return;
        }

        _currencyLoadRoutine = StartCoroutine(LoadCurrencyIconRoutine(url));
    }

    private IEnumerator LoadCurrencyIconRoutine(string url)
    {
        Sprite loaded = null;
        yield return PlatformCurrencyIconCache.Load(url, sprite => loaded = sprite);

        _currencyLoadRoutine = null;

        if (_pendingCurrencyImageUrl != url || loaded == null)
            yield break;

        ApplyCurrencySprite(loaded);
    }

    private void ApplyCurrencySprite(Sprite sprite)
    {
        if (_currencyImage == null || sprite == null)
            return;

        _currencyImage.sprite = sprite;
        _currencyImage.gameObject.SetActive(true);
    }

    private void StopCurrencyLoad()
    {
        if (_currencyLoadRoutine == null)
            return;

        StopCoroutine(_currencyLoadRoutine);
        _currencyLoadRoutine = null;
    }

    private void BindResourceRewards(IReadOnlyList<IapResourceReward> rewards)
    {
        int rewardCount = CountValidRewards(rewards);
        EnsureResourceCells(rewardCount);

        int cellIndex = 0;
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                IapResourceReward reward = rewards[i];
                if (reward.Amount <= 0)
                    continue;

                ResourseConfig config = FindResourceConfig(reward.Type);
                if (config == null)
                    continue;

                if (cellIndex >= _spawnedResourceCells.Count)
                    break;

                ResourceCellView cell = _spawnedResourceCells[cellIndex];
                cell.gameObject.SetActive(true);
                cell.Initialize(config, reward.Amount);
                cellIndex++;
            }
        }

        for (int i = cellIndex; i < _spawnedResourceCells.Count; i++)
        {
            ResourceCellView cell = _spawnedResourceCells[i];
            if (cell != null)
                cell.gameObject.SetActive(false);
        }
    }

    private void EnsureResourceCells(int count)
    {
        if (_resourcesRoot == null || _resourceCellViewPrefab == null || count <= 0)
            return;

        while (_spawnedResourceCells.Count < count)
        {
            ResourceCellView cell = Instantiate(_resourceCellViewPrefab, _resourcesRoot);
            LayoutElement layoutElement = cell.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = cell.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredWidth = 140f;
            layoutElement.preferredHeight = 160f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            cell.gameObject.SetActive(true);
            _spawnedResourceCells.Add(cell);
        }
    }

    private static int CountValidRewards(IReadOnlyList<IapResourceReward> rewards)
    {
        if (rewards == null)
            return 0;

        int count = 0;
        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i].Amount > 0)
                count++;
        }

        return count;
    }

    private ResourseConfig FindResourceConfig(ResourceType type)
    {
        if (_allResourses?.Resourses == null)
            return null;

        return _allResourses.Resourses.FirstOrDefault(config => config != null && config.ResourseType == type);
    }

    private void OnBuyClicked()
    {
        if (_currentOffer == null || _iapService == null)
            return;

        _iapService.TryPurchaseIslandOffer(_currentOffer.IslandIndex);
    }

    private static string SafeLocalized(UnityEngine.Localization.LocalizedString localized)
    {
        if (localized == null)
            return string.Empty;

        try
        {
            string value = localized.GetLocalizedString();
            return value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
