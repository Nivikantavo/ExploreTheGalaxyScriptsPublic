#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class PlanetTravelHudSetup
{
    private const string PrefabFolder = "Assets/Prefabs/UI/PlanetTravel";
    private const string CardPrefabPath = PrefabFolder + "/PlanetCard.prefab";
    private const string PanelPrefabPath = PrefabFolder + "/PlanetTravelPanel.prefab";
    private const string WorldCatalogPath = "Assets/Configs/Save/WorldCatalog.asset";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string LocalizationTableGuid = "GUID:463fb33737ab6364088020068eda3d31";
    private const long TitleKeyId = 2920000000000001;
    private const long GoKeyId = 2920000000000002;

    [MenuItem("Tools/MyLittleUniverse/Setup Planet Travel UI")]
    public static void Setup()
    {
        EnsureFolder(PrefabFolder);

        PlanetCardView card = CreateCardPrefab();
        CreatePanelPrefab(card);
        WireGameplayScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Planet travel UI prefabs created and GameplayScene portal wired.");
    }

    private static PlanetCardView CreateCardPrefab()
    {
        GameObject root = new GameObject("PlanetCard", typeof(RectTransform), typeof(Image), typeof(PlanetCardView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(280f, 380f);
        root.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.96f);

        LayoutElement layoutElement = root.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 280f;
        layoutElement.preferredHeight = 380f;
        layoutElement.minWidth = 280f;
        layoutElement.minHeight = 380f;

        GameObject iconGo = CreateUiChild(root.transform, "Icon", typeof(Image));
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -24f);
        iconRect.sizeDelta = new Vector2(200f, 200f);
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;

        GameObject nameGo = CreateUiChild(root.transform, "Name", typeof(TextMeshProUGUI));
        RectTransform nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.08f, 0.28f);
        nameRect.anchorMax = new Vector2(0.92f, 0.42f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        TextMeshProUGUI nameText = nameGo.GetComponent<TextMeshProUGUI>();
        nameText.text = "Planet";
        nameText.fontSize = 28f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.enableWordWrapping = true;

        GameObject goButtonGo = CreateUiChild(root.transform, "GoButton", typeof(Image), typeof(Button));
        RectTransform goRect = goButtonGo.GetComponent<RectTransform>();
        goRect.anchorMin = new Vector2(0.5f, 0f);
        goRect.anchorMax = new Vector2(0.5f, 0f);
        goRect.pivot = new Vector2(0.5f, 0f);
        goRect.anchoredPosition = new Vector2(0f, 20f);
        goRect.sizeDelta = new Vector2(200f, 64f);
        goButtonGo.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f, 1f);
        Button goButton = goButtonGo.GetComponent<Button>();

        GameObject goLabelGo = CreateUiChild(goButtonGo.transform, "Label", typeof(TextMeshProUGUI));
        RectTransform goLabelRect = goLabelGo.GetComponent<RectTransform>();
        StretchFull(goLabelRect);
        TextMeshProUGUI goLabel = goLabelGo.GetComponent<TextMeshProUGUI>();
        goLabel.text = "Go";
        goLabel.fontSize = 28f;
        goLabel.color = Color.white;
        goLabel.alignment = TextAlignmentOptions.Center;

        GameObject lockGo = CreateUiChild(root.transform, "LockOverlay", typeof(Image));
        RectTransform lockRect = lockGo.GetComponent<RectTransform>();
        StretchFull(lockRect);
        lockGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.62f);
        lockGo.SetActive(false);

        GameObject lockLabelGo = CreateUiChild(lockGo.transform, "LockLabel", typeof(TextMeshProUGUI));
        RectTransform lockLabelRect = lockLabelGo.GetComponent<RectTransform>();
        StretchFull(lockLabelRect);
        TextMeshProUGUI lockLabel = lockLabelGo.GetComponent<TextMeshProUGUI>();
        lockLabel.text = "Locked";
        lockLabel.fontSize = 32f;
        lockLabel.color = Color.white;
        lockLabel.alignment = TextAlignmentOptions.Center;

        PlanetCardView view = root.GetComponent<PlanetCardView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_icon").objectReferenceValue = iconImage;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_goButton").objectReferenceValue = goButton;
        so.FindProperty("_goButtonText").objectReferenceValue = goLabel;
        so.FindProperty("_lockOverlay").objectReferenceValue = lockGo;
        so.ApplyModifiedPropertiesWithoutUndo();

        PlanetCardView prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath).GetComponent<PlanetCardView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreatePanelPrefab(PlanetCardView cardPrefab)
    {
        GameObject root = new GameObject("PlanetTravelPanel", typeof(RectTransform), typeof(PlanetTravelPanelView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);

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
        titleRect.anchorMin = new Vector2(0.08f, 0.9f);
        titleRect.anchorMax = new Vector2(0.92f, 0.98f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.text = "Choose a planet";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 48f;
        titleText.color = Color.white;

        GameObject scrollGo = CreateUiChild(card.transform, "Scroll", typeof(Image), typeof(ScrollRect));
        RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.06f, 0.14f);
        scrollRect.anchorMax = new Vector2(0.94f, 0.88f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.15f, 1f);

        GameObject viewport = CreateUiChild(scrollGo.transform, "Viewport", typeof(RectMask2D), typeof(Image));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        StretchFull(viewportRect);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        GameObject content = CreateUiChild(viewport.transform, "Content", typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(280f, 380f);
        grid.spacing = new Vector2(24f, 24f);
        grid.padding = new RectOffset(16, 16, 16, 16);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject closeGo = CreateUiChild(card.transform, "CloseButton", typeof(Image), typeof(Button));
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 28f);
        closeRect.sizeDelta = new Vector2(260f, 80f);
        closeGo.GetComponent<Image>().color = new Color(0.35f, 0.38f, 0.45f, 1f);

        GameObject closeLabelGo = CreateUiChild(closeGo.transform, "Label", typeof(TextMeshProUGUI));
        RectTransform closeLabelRect = closeLabelGo.GetComponent<RectTransform>();
        StretchFull(closeLabelRect);
        TextMeshProUGUI closeLabel = closeLabelGo.GetComponent<TextMeshProUGUI>();
        closeLabel.text = "Close";
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.fontSize = 34f;
        closeLabel.color = Color.white;

        WorldCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WorldCatalogSO>(WorldCatalogPath);
        PlanetTravelPanelView view = root.GetComponent<PlanetTravelPanelView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_panelRoot").objectReferenceValue = panel;
        so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
        so.FindProperty("_contentRoot").objectReferenceValue = content.transform;
        so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
        so.FindProperty("_worldCatalog").objectReferenceValue = catalog;
        so.FindProperty("_titleText").objectReferenceValue = titleText;
        AssignLocalizedString(so.FindProperty("_title"), TitleKeyId);
        AssignLocalizedString(so.FindProperty("_goLabel"), GoKeyId);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void WireGameplayScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Transform portalPopap = GameObject.Find("Canvas/PortalPopap")?.transform;
        if (portalPopap == null)
        {
            Canvas hudCanvas = FindHudCanvas();
            if (hudCanvas == null)
            {
                Debug.LogError("PlanetTravelHudSetup: HUD Canvas not found in GameplayScene.");
                return;
            }

            GameObject created = new GameObject("PortalPopap", typeof(RectTransform));
            portalPopap = created.transform;
            portalPopap.SetParent(hudCanvas.transform, false);
            StretchFull(portalPopap.GetComponent<RectTransform>());
        }

        DestroyMisplacedTravelUi(portalPopap);

        Transform oldAnticlick = FindChildRecursive(portalPopap, "Anticlick");
        if (oldAnticlick != null)
            oldAnticlick.gameObject.SetActive(false);

        PlanetTravelPanelView[] existingPanels = portalPopap.GetComponentsInChildren<PlanetTravelPanelView>(true);
        for (int i = 0; i < existingPanels.Length; i++)
        {
            if (existingPanels[i] != null)
                Object.DestroyImmediate(existingPanels[i].gameObject);
        }

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, portalPopap);
        instance.name = "PlanetTravelPanel";
        StretchFull(instance.GetComponent<RectTransform>());

        PlanetTravelPanelView panelView = instance.GetComponent<PlanetTravelPanelView>();
        LoadingScreen loadingScreen = Object.FindFirstObjectByType<LoadingScreen>(FindObjectsInactive.Include);
        SerializedObject panelSo = new SerializedObject(panelView);
        panelSo.FindProperty("_loadingScreen").objectReferenceValue = loadingScreen;
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        Portal portal = Object.FindFirstObjectByType<Portal>(FindObjectsInactive.Include);
        if (portal == null)
        {
            Debug.LogError("PlanetTravelHudSetup: Portal not found in GameplayScene.");
        }
        else
        {
            SerializedObject portalSo = new SerializedObject(portal);
            portalSo.FindProperty("_planetTravelPanel").objectReferenceValue = panelView;
            portalSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AssignLocalizedString(SerializedProperty property, long keyId)
    {
        if (property == null)
            return;

        SerializedProperty table = property.FindPropertyRelative("m_TableReference.m_TableCollectionName");
        SerializedProperty keyIdProperty = property.FindPropertyRelative("m_TableEntryReference.m_KeyId");
        SerializedProperty keyProperty = property.FindPropertyRelative("m_TableEntryReference.m_Key");
        if (table != null)
            table.stringValue = LocalizationTableGuid;
        if (keyIdProperty != null)
            keyIdProperty.longValue = keyId;
        if (keyProperty != null)
            keyProperty.stringValue = string.Empty;
    }

    private static Canvas FindHudCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.transform.parent != null)
                continue;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.name == "Canvas")
                return canvas;
        }

        return null;
    }

    private static void DestroyMisplacedTravelUi(Transform hudPortalPopap)
    {
        PlanetTravelPanelView[] panels = Object.FindObjectsByType<PlanetTravelPanelView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < panels.Length; i++)
        {
            PlanetTravelPanelView panel = panels[i];
            if (panel == null)
                continue;

            if (hudPortalPopap != null && panel.transform.IsChildOf(hudPortalPopap))
                continue;

            Transform misplacedRoot = panel.transform;
            Transform parent = misplacedRoot.parent;
            Object.DestroyImmediate(misplacedRoot.gameObject);
            if (parent != null && parent.name == "PortalPopap" && parent != hudPortalPopap)
                Object.DestroyImmediate(parent.gameObject);
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null)
            return null;

        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
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
