using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class IslandOfferUiBuilder
{
    private const string InventoryResourceCellPrefabPath = "Assets/Prefabs/UI/Inventory/ResourseCellView.prefab";

    public static IslandOfferPopupView BuildUnder(Transform parent, ResourceCellView resourceCellPrefab = null)
    {
        if (resourceCellPrefab == null)
            resourceCellPrefab = LoadResourceCellPrefab();

        GameObject root = new GameObject("IslandOfferView", typeof(RectTransform), typeof(IslandOfferPopupView));
        if (parent != null)
            root.transform.SetParent(parent, false);
        StretchFull(root.GetComponent<RectTransform>());

        GameObject openButtonGo = CreateUiChild(root.transform, "OpenButton", typeof(Image), typeof(Button));
        RectTransform openRect = openButtonGo.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(0f, 0f);
        openRect.anchorMax = new Vector2(0f, 0f);
        openRect.pivot = new Vector2(0.5f, 0.5f);
        openRect.anchoredPosition = new Vector2(113f, 730f);
        openRect.sizeDelta = new Vector2(118f, 112f);
        Image openImage = openButtonGo.GetComponent<Image>();
        openImage.color = new Color(1f, 0.78f, 0.2f, 1f);
        openButtonGo.AddComponent<LockedUntilTutorialCompleted>();

        TextMeshProUGUI openLabel = CreateLabel(openButtonGo.transform, "Label", "Offer", 28f, TextAlignmentOptions.Center);
        StretchFull(openLabel.rectTransform);
        openButtonGo.SetActive(false);

        GameObject panel = CreateUiChild(root.transform, "Panel", typeof(Image));
        StretchFull(panel.GetComponent<RectTransform>());
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = true;
        panel.SetActive(false);

        GameObject card = CreateUiChild(panel.transform, "Card", typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(920f, 1180f);
        card.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);

        TextMeshProUGUI titleText = CreateAnchoredLabel(
            card.transform,
            "TitleText",
            "Special Offer",
            new Vector2(0.06f, 0.84f),
            new Vector2(0.94f, 0.96f),
            46f,
            TextAlignmentOptions.Center);

        TextMeshProUGUI gemsText = CreateAnchoredLabel(
            card.transform,
            "GemsText",
            "+50",
            new Vector2(0.06f, 0.72f),
            new Vector2(0.94f, 0.82f),
            40f,
            TextAlignmentOptions.Center);

        GameObject resourcesRootGo = CreateUiChild(card.transform, "ResourcesRoot", typeof(HorizontalLayoutGroup));
        RectTransform resourcesRect = resourcesRootGo.GetComponent<RectTransform>();
        resourcesRect.anchorMin = new Vector2(0.06f, 0.46f);
        resourcesRect.anchorMax = new Vector2(0.94f, 0.7f);
        resourcesRect.offsetMin = Vector2.zero;
        resourcesRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup resourcesLayout = resourcesRootGo.GetComponent<HorizontalLayoutGroup>();
        resourcesLayout.childAlignment = TextAnchor.MiddleCenter;
        resourcesLayout.spacing = 16f;
        resourcesLayout.childControlWidth = true;
        resourcesLayout.childControlHeight = true;
        resourcesLayout.childForceExpandWidth = false;
        resourcesLayout.childForceExpandHeight = false;
        resourcesLayout.padding = new RectOffset(8, 8, 8, 8);

        GameObject buyButtonGo = CreateUiChild(card.transform, "BuyButton", typeof(Image), typeof(Button));
        RectTransform buyRect = buyButtonGo.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0.5f, 0f);
        buyRect.anchorMax = new Vector2(0.5f, 0f);
        buyRect.pivot = new Vector2(0.5f, 0f);
        buyRect.anchoredPosition = new Vector2(0f, 120f);
        buyRect.sizeDelta = new Vector2(520f, 96f);
        buyButtonGo.GetComponent<Image>().color = new Color(0.25f, 0.78f, 0.35f, 1f);

        TextMeshProUGUI priceText = CreateLabel(buyButtonGo.transform, "PriceText", string.Empty, 34f, TextAlignmentOptions.Center);
        RectTransform priceRect = priceText.rectTransform;
        priceRect.anchorMin = new Vector2(0.18f, 0f);
        priceRect.anchorMax = new Vector2(0.95f, 1f);
        priceRect.offsetMin = Vector2.zero;
        priceRect.offsetMax = Vector2.zero;

        GameObject currencyImageGo = CreateUiChild(buyButtonGo.transform, "CurrencyImage", typeof(Image));
        RectTransform currencyRect = currencyImageGo.GetComponent<RectTransform>();
        currencyRect.anchorMin = new Vector2(0.04f, 0.2f);
        currencyRect.anchorMax = new Vector2(0.16f, 0.8f);
        currencyRect.offsetMin = Vector2.zero;
        currencyRect.offsetMax = Vector2.zero;
        Image currencyImage = currencyImageGo.GetComponent<Image>();
        currencyImage.preserveAspect = true;
        currencyImage.raycastTarget = false;
        currencyImageGo.SetActive(false);

        GameObject closeButtonGo = CreateUiChild(card.transform, "CloseButton", typeof(Image), typeof(Button));
        RectTransform closeRect = closeButtonGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 24f);
        closeRect.sizeDelta = new Vector2(260f, 80f);
        closeButtonGo.GetComponent<Image>().color = new Color(0.45f, 0.48f, 0.55f, 1f);

        TextMeshProUGUI closeLabel = CreateLabel(closeButtonGo.transform, "Label", "Close", 32f, TextAlignmentOptions.Center);
        StretchFull(closeLabel.rectTransform);

        IslandOfferPopupView view = root.GetComponent<IslandOfferPopupView>();
        view.Configure(
            panel,
            openButtonGo.GetComponent<Button>(),
            closeButtonGo.GetComponent<Button>(),
            buyButtonGo.GetComponent<Button>(),
            titleText,
            priceText,
            currencyImage,
            gemsText,
            resourcesRootGo.transform,
            resourceCellPrefab,
            autoShowOnIslandUnlock: true);

        SetLayerRecursive(root, 5);
        return view;
    }

    public static ResourceCellView LoadResourceCellPrefab()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceCellView>(InventoryResourceCellPrefabPath);
#else
        return null;
#endif
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiChild(parent, name, typeof(TextMeshProUGUI));
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        return label;
    }

    private static TextMeshProUGUI CreateAnchoredLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiChild(parent, name, typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        return label;
    }

    private static GameObject CreateUiChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
            SetLayerRecursive(transform.GetChild(i).gameObject, layer);
    }
}
