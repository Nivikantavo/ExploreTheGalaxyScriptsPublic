#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AchievementsHudSetup
{
    private const string PrefabFolder = "Assets/Prefabs/UI/Achievements";
    private const string EntryPrefabPath = PrefabFolder + "/AchievementEntry.prefab";
    private const string PanelPrefabPath = PrefabFolder + "/AchievementsPanel.prefab";

    [MenuItem("Tools/Celebrations/Create Achievements UI Prefabs")]
    public static void CreateAchievementsUiPrefabs()
    {
        EnsureFolder(PrefabFolder);

        AchievementEntryView entry = CreateEntryPrefab();
        CreatePanelPrefab(entry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Achievements UI prefabs created at {PrefabFolder}. Drag AchievementsPanel into the gameplay HUD Canvas and wire CelebrationSystem if needed.");
    }

    private static AchievementEntryView CreateEntryPrefab()
    {
        GameObject root = new GameObject("AchievementEntry", typeof(RectTransform), typeof(Image), typeof(AchievementEntryView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(900f, 140f);
        root.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.24f, 0.95f);

        GameObject iconGo = CreateUiChild(root.transform, "Icon", typeof(Image));
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(24f, 0f);
        iconRect.sizeDelta = new Vector2(96f, 96f);
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;

        GameObject titleGo = CreateUiChild(root.transform, "Title", typeof(TextMeshProUGUI));
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(140f, 0f);
        titleRect.offsetMax = new Vector2(-120f, -16f);
        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 34f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject bodyGo = CreateUiChild(root.transform, "Body", typeof(TextMeshProUGUI));
        RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.offsetMin = new Vector2(140f, 16f);
        bodyRect.offsetMax = new Vector2(-120f, 0f);
        TextMeshProUGUI bodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize = 26f;
        bodyText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        bodyText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject checkGo = CreateUiChild(root.transform, "Checkmark", typeof(Image));
        RectTransform checkRect = checkGo.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(1f, 0.5f);
        checkRect.anchorMax = new Vector2(1f, 0.5f);
        checkRect.pivot = new Vector2(1f, 0.5f);
        checkRect.anchoredPosition = new Vector2(-28f, 0f);
        checkRect.sizeDelta = new Vector2(64f, 64f);
        checkGo.GetComponent<Image>().color = new Color(0.35f, 0.85f, 0.45f, 1f);

        AchievementEntryView view = root.GetComponent<AchievementEntryView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_icon").objectReferenceValue = iconImage;
        so.FindProperty("_titleText").objectReferenceValue = titleText;
        so.FindProperty("_bodyText").objectReferenceValue = bodyText;
        so.FindProperty("_checkmark").objectReferenceValue = checkGo;
        so.ApplyModifiedPropertiesWithoutUndo();

        AchievementEntryView prefab = PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath).GetComponent<AchievementEntryView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreatePanelPrefab(AchievementEntryView entryPrefab)
    {
        GameObject root = new GameObject("AchievementsPanel", typeof(RectTransform), typeof(AchievementsPanelView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);

        GameObject openButtonGo = CreateUiChild(root.transform, "OpenButton", typeof(Image), typeof(Button));
        RectTransform openRect = openButtonGo.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(1f, 1f);
        openRect.anchorMax = new Vector2(1f, 1f);
        openRect.pivot = new Vector2(1f, 1f);
        openRect.anchoredPosition = new Vector2(-24f, -140f);
        openRect.sizeDelta = new Vector2(120f, 120f);
        openButtonGo.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.7f, 1f);

        GameObject panel = CreateUiChild(root.transform, "Panel", typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StretchFull(panelRect);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        panel.SetActive(false);

        GameObject card = CreateUiChild(panel.transform, "Card", typeof(Image));
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(980f, 1200f);
        card.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.2f, 1f);

        GameObject titleGo = CreateUiChild(card.transform, "Title", typeof(TextMeshProUGUI));
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.9f);
        titleRect.anchorMax = new Vector2(0.95f, 0.98f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.text = "Achievements";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 48f;
        titleText.color = Color.white;

        GameObject scrollGo = CreateUiChild(card.transform, "Scroll", typeof(Image), typeof(ScrollRect));
        RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.05f, 0.12f);
        scrollRect.anchorMax = new Vector2(0.95f, 0.88f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.15f, 1f);

        GameObject viewport = CreateUiChild(scrollGo.transform, "Viewport", typeof(RectMask2D), typeof(Image));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        StretchFull(viewportRect);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        GameObject content = CreateUiChild(viewport.transform, "Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 12f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        GameObject closeGo = CreateUiChild(card.transform, "CloseButton", typeof(Image), typeof(Button));
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 28f);
        closeRect.sizeDelta = new Vector2(260f, 80f);
        closeGo.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f, 1f);

        GameObject closeLabelGo = CreateUiChild(closeGo.transform, "Label", typeof(TextMeshProUGUI));
        RectTransform closeLabelRect = closeLabelGo.GetComponent<RectTransform>();
        StretchFull(closeLabelRect);
        TextMeshProUGUI closeLabel = closeLabelGo.GetComponent<TextMeshProUGUI>();
        closeLabel.text = "Close";
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.fontSize = 34f;
        closeLabel.color = Color.white;

        AchievementsPanelView panelView = root.GetComponent<AchievementsPanelView>();
        SerializedObject so = new SerializedObject(panelView);
        so.FindProperty("_panelRoot").objectReferenceValue = panel;
        so.FindProperty("_openButton").objectReferenceValue = openButtonGo.GetComponent<Button>();
        so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
        so.FindProperty("_contentRoot").objectReferenceValue = content.transform;
        so.FindProperty("_entryPrefab").objectReferenceValue = entryPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
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

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
