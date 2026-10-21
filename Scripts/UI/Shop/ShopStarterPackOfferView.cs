using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopStarterPackOfferView : MonoBehaviour
{
    [Header("Purchase")]
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private Image _currencyImage;
    [SerializeField] private GameObject _purchasedMark;

    [Header("Instruments")]
    [SerializeField] private Image _axeIcon;
    [SerializeField] private TMP_Text _axeLevelText;
    [SerializeField] private Image _pickaxeIcon;
    [SerializeField] private TMP_Text _pickaxeLevelText;
    [SerializeField] private Image _swordIcon;
    [SerializeField] private TMP_Text _swordLevelText;

    [Header("Resource Rewards")]
    [SerializeField] private Transform _resourceRewardsRoot;
    [SerializeField] private ShopStarterPackResourceSlotView _resourceSlotPrefab;
    [SerializeField] private ResourceTypeViews _resourceTypeViews;

    private readonly List<ShopStarterPackResourceSlotView> _spawnedResourceSlots = new();
    private Action _onBuyClicked;
    private Coroutine _currencyLoadRoutine;
    private string _pendingCurrencyImageUrl;

    private void OnEnable()
    {
        if (_buyButton != null)
            _buyButton.onClick.AddListener(OnBuyClicked);
    }

    private void OnDisable()
    {
        if (_buyButton != null)
            _buyButton.onClick.RemoveListener(OnBuyClicked);

        StopCurrencyLoad();
    }

    public void Bind(
        StarterPackSO starter,
        AllInstruments allInstruments,
        Inventory inventory,
        string price,
        bool available,
        Action onBuyClicked,
        string currencyImageUrl = null,
        bool owned = false)
    {
        _onBuyClicked = onBuyClicked;

        if (_priceText != null)
            _priceText.text = price ?? string.Empty;

        BindInstruments(starter, allInstruments, inventory);
        BindResourceRewards(starter);
        SetAvailable(available);
        SetOwned(owned);
        SetSale(0);
        SetCurrencyImageUrl(currencyImageUrl);
    }

    public void SetSale(int percent)
    {
        ShopSaleMark.Apply(transform, percent);
    }

    public void SetAvailable(bool available)
    {
        if (_buyButton != null)
            _buyButton.interactable = available;
    }

    public void SetOwned(bool owned)
    {
        if (_purchasedMark != null)
            _purchasedMark.SetActive(owned);
    }

    public void SetCurrencyImageUrl(string url)
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

    private void BindInstruments(StarterPackSO starter, AllInstruments allInstruments, Inventory inventory)
    {
        BindInstrumentSlot(
            InstrumentType.Axe,
            _axeIcon,
            _axeLevelText,
            starter,
            allInstruments,
            inventory);

        BindInstrumentSlot(
            InstrumentType.Pickaxe,
            _pickaxeIcon,
            _pickaxeLevelText,
            starter,
            allInstruments,
            inventory);

        BindInstrumentSlot(
            InstrumentType.Sword,
            _swordIcon,
            _swordLevelText,
            starter,
            allInstruments,
            inventory);
    }

    private static void BindInstrumentSlot(
        InstrumentType type,
        Image iconImage,
        TMP_Text levelText,
        StarterPackSO starter,
        AllInstruments allInstruments,
        Inventory inventory)
    {
        int currentLevel = GetCurrentInstrumentLevel(inventory, type);
        int maxLevel = GetMaxInstrumentLevel(allInstruments, type);
        int offeredLevel = starter != null
            ? starter.GetOfferedInstrumentLevel(currentLevel, maxLevel)
            : Mathf.Max(1, currentLevel);

        if (levelText != null)
            levelText.text = offeredLevel.ToString();

        SetInstrumentIcon(iconImage, type, offeredLevel, allInstruments);
    }

    private static int GetCurrentInstrumentLevel(Inventory inventory, InstrumentType type)
    {
        if (inventory?.Instruments == null)
            return 1;

        Instrument instrument = inventory.Instruments.FirstOrDefault(item => item.Type == type);
        return instrument != null ? Mathf.Max(1, instrument.Level) : 1;
    }

    private static int GetMaxInstrumentLevel(AllInstruments allInstruments, InstrumentType type)
    {
        if (allInstruments?.Instruments == null)
            return 0;

        int max = 0;
        foreach (InstrumentConfig config in allInstruments.Instruments)
        {
            if (config != null && config.Type == type)
                max = Mathf.Max(max, config.Level);
        }

        return max;
    }

    private static void SetInstrumentIcon(
        Image image,
        InstrumentType type,
        int level,
        AllInstruments allInstruments)
    {
        if (image == null)
            return;

        Sprite icon = ResolveInstrumentIcon(type, level, allInstruments);
        if (icon == null)
        {
            image.enabled = false;
            return;
        }

        image.enabled = true;
        image.sprite = icon;
    }

    private static Sprite ResolveInstrumentIcon(InstrumentType type, int level, AllInstruments allInstruments)
    {
        if (allInstruments?.Instruments == null)
            return null;

        InstrumentConfig exact = allInstruments.Instruments.FirstOrDefault(
            config => config != null && config.Type == type && config.Level == level);
        if (exact != null && exact.Icon != null)
            return exact.Icon;

        InstrumentConfig fallback = allInstruments.Instruments
            .Where(config => config != null && config.Type == type && config.Icon != null)
            .OrderByDescending(config => config.Level)
            .FirstOrDefault(config => config.Level <= level);

        return fallback != null ? fallback.Icon : null;
    }

    private void BindResourceRewards(StarterPackSO starter)
    {
        IReadOnlyList<IapResourceReward> rewards = starter?.ResourceRewards;
        int rewardCount = rewards != null ? rewards.Count : 0;

        EnsureResourceSlots(rewardCount);

        for (int i = 0; i < _spawnedResourceSlots.Count; i++)
        {
            ShopStarterPackResourceSlotView slot = _spawnedResourceSlots[i];
            if (slot == null)
                continue;

            if (rewards == null || i >= rewards.Count || rewards[i].Amount <= 0)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            IapResourceReward reward = rewards[i];
            slot.gameObject.SetActive(true);
            slot.Bind(ResolveResourceIcon(reward.Type), $"x{reward.Amount}");
        }
    }

    private void EnsureResourceSlots(int count)
    {
        if (_resourceRewardsRoot == null || _resourceSlotPrefab == null || count <= 0)
            return;

        while (_spawnedResourceSlots.Count < count)
        {
            ShopStarterPackResourceSlotView slot = Instantiate(_resourceSlotPrefab, _resourceRewardsRoot);
            slot.gameObject.SetActive(true);
            _spawnedResourceSlots.Add(slot);
        }
    }

    private Sprite ResolveResourceIcon(ResourceType type)
    {
        if (_resourceTypeViews != null
            && _resourceTypeViews.Icons != null
            && _resourceTypeViews.Icons.TryGetValue(type, out Sprite sprite))
        {
            return sprite;
        }

        return null;
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

    private void OnBuyClicked()
    {
        _onBuyClicked?.Invoke();
    }
}
