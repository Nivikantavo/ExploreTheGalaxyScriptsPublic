using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopOfferView : MonoBehaviour
{
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private GameObject _purchasedMark;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _currencyImage;
    [SerializeField] private bool _currencyImageEnabled = true;

    private string _productId;
    private ResourceType _resourceType;
    private bool _isResourceOffer;
    private bool _isCustomAction;
    private System.Action<string> _onProductBuyClicked;
    private System.Action<ResourceType> _onResourceBuyClicked;
    private System.Action _onCustomBuyClicked;
    private Coroutine _currencyLoadRoutine;
    private string _pendingCurrencyImageUrl;

    public string ProductId => _productId;
    public ResourceType ResourceType => _resourceType;

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
        string productId,
        string title,
        string price,
        string amountLabel,
        Sprite icon,
        bool available,
        System.Action<string> onBuyClicked,
        string currencyImageUrl = null,
        bool owned = false)
    {
        _isResourceOffer = false;
        _isCustomAction = false;
        _productId = productId;
        _onProductBuyClicked = onBuyClicked;
        _onResourceBuyClicked = null;
        _onCustomBuyClicked = null;
        ApplyVisuals(title, price, amountLabel, icon, available);

        if (_purchasedMark != null)
            _purchasedMark.SetActive(owned);

        if (_currencyImageEnabled)
            _currencyImage.gameObject.SetActive(true);
        else
            SetCurrencyImageUrl(currencyImageUrl);
    }

    public void BindResource(
        ResourceType resourceType,
        string title,
        string price,
        string amountLabel,
        Sprite icon,
        bool available,
        System.Action<ResourceType> onBuyClicked)
    {
        _isResourceOffer = true;
        _isCustomAction = false;
        _resourceType = resourceType;
        _productId = null;
        _onProductBuyClicked = null;
        _onResourceBuyClicked = onBuyClicked;
        _onCustomBuyClicked = null;
        ApplyVisuals(title, price, amountLabel, icon, available);

        if (_currencyImageEnabled)
            _currencyImage.gameObject.SetActive(true);
        else
            ClearCurrencyImage();
    }

    public void BindAction(
        string title,
        string price,
        string amountLabel,
        Sprite icon,
        bool available,
        System.Action onBuyClicked)
    {
        _isResourceOffer = false;
        _isCustomAction = true;
        _productId = null;
        _onProductBuyClicked = null;
        _onResourceBuyClicked = null;
        _onCustomBuyClicked = onBuyClicked;
        ApplyVisuals(title, price, amountLabel, icon, available);
        
        if (_currencyImageEnabled)
            _currencyImage.gameObject.SetActive(true);
        else
            ClearCurrencyImage();
    }

    public void SetAvailable(bool available)
    {
        if (_buyButton != null)
            _buyButton.interactable = available;
    }

    public void SetOwned(bool owned)
    {
        if (_purchasedMark != null)
            _purchasedMark.SetActive(owned && !_isResourceOffer && !_isCustomAction);
    }

    public void SetPrice(string price)
    {
        if (_priceText != null)
            _priceText.text = price ?? string.Empty;
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

    private void ApplyVisuals(string title, string price, string amountLabel, Sprite icon, bool available)
    {
        if (_priceText != null)
            _priceText.text = price ?? string.Empty;

        if (_amountText != null)
            _amountText.text = amountLabel ?? string.Empty;

        if (_iconImage != null && icon != null)
            _iconImage.sprite = icon;

        if (_purchasedMark != null && (_isResourceOffer || _isCustomAction))
            _purchasedMark.SetActive(false);

        SetSale(0);
        SetAvailable(available);
    }

    public void SetSale(int percent)
    {
        ShopSaleMark.Apply(transform, percent);
    }

    private void ClearCurrencyImage()
    {
        StopCurrencyLoad();
        _pendingCurrencyImageUrl = null;

        if (_currencyImage != null)
            _currencyImage.gameObject.SetActive(false);
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
        if (_isCustomAction)
        {
            _onCustomBuyClicked?.Invoke();
            return;
        }

        if (_isResourceOffer)
        {
            _onResourceBuyClicked?.Invoke(_resourceType);
            return;
        }

        if (!string.IsNullOrEmpty(_productId))
            _onProductBuyClicked?.Invoke(_productId);
    }
}
