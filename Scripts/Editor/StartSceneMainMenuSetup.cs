#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StartSceneMainMenuSetup
{
    private const string StartScenePath = "Assets/Scenes/StartScene.unity";
    private const string AudioClipsConfigPath = "Assets/Configs/Audio/AudioClipsConfig.asset";

    [MenuItem("Tools/MyLittleUniverse/Setup Start Scene Main Menu")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("StartSceneMainMenuSetup: Canvas not found in StartScene.");
            return;
        }

        EnsureEventSystem();

        Transform canvasTransform = canvas.transform;
        GameObject menuRoot = FindOrCreateChild(canvasTransform, "Menu");
        Button playButton = FindButton(menuRoot.transform, "PlayButton");
        Button settingsButton = FindButton(menuRoot.transform, "SettingsButton");

        LoadingScreen loadingScreen = EnsureLoadingScreen(canvasTransform);
        SettingsPanelView settingsPanel = EnsureSettingsPanel(canvasTransform);

        MainMenu mainMenu = canvas.GetComponent<MainMenu>();
        if (mainMenu == null)
            mainMenu = canvas.gameObject.AddComponent<MainMenu>();

        SerializedObject so = new SerializedObject(mainMenu);
        so.FindProperty("_loadingScreen").objectReferenceValue = loadingScreen;
        so.FindProperty("_menuRoot").objectReferenceValue = menuRoot;
        so.FindProperty("_playButton").objectReferenceValue = playButton;
        so.FindProperty("_settingsButton").objectReferenceValue = settingsButton;
        so.FindProperty("_settingsPanel").objectReferenceValue = settingsPanel;
        so.FindProperty("_gameplaySceneName").stringValue = "GameplayScene";
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("StartSceneMainMenuSetup: MainMenu wired. StartScene saved and added to Build Settings.");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static LoadingScreen EnsureLoadingScreen(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("LoadingScreen");
        GameObject root = existing != null ? existing.gameObject : CreateUiPanel(canvasTransform, "LoadingScreen", new Color(0.08f, 0.08f, 0.1f, 1f));
        root.transform.SetAsLastSibling();

        StretchFull(root.GetComponent<RectTransform>());

        TextMeshProUGUI label = root.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null)
        {
            GameObject textGo = CreateTmpText(root.transform, "Text", "Loading...", 48, TextAlignmentOptions.Center);
            StretchFull(textGo.GetComponent<RectTransform>());
            label = textGo.GetComponent<TextMeshProUGUI>();
        }

        label.text = "Loading...";
        label.alignment = TextAlignmentOptions.Center;

        LoadingScreen loadingScreen = root.GetComponent<LoadingScreen>();
        if (loadingScreen == null)
            loadingScreen = root.AddComponent<LoadingScreen>();

        SerializedObject so = new SerializedObject(loadingScreen);
        so.FindProperty("_root").objectReferenceValue = root;
        so.FindProperty("_hideOnAwake").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        return loadingScreen;
    }

    private static SettingsPanelView EnsureSettingsPanel(Transform canvasTransform)
    {
        Transform hostTransform = canvasTransform.Find("Settings");
        GameObject host = hostTransform != null
            ? hostTransform.gameObject
            : new GameObject("Settings", typeof(RectTransform));
        if (hostTransform == null)
            host.transform.SetParent(canvasTransform, false);
        StretchFull(host.GetComponent<RectTransform>());
        host.transform.SetAsLastSibling();

        Transform existing = host.transform.Find("SettingsPanel");
        GameObject panelRoot = existing != null
            ? existing.gameObject
            : CreateUiPanel(host.transform, "SettingsPanel", new Color(0f, 0f, 0f, 0.72f));
        StretchFull(panelRoot.GetComponent<RectTransform>());

        Transform contentTransform = panelRoot.transform.Find("Content");
        GameObject content = contentTransform != null
            ? contentTransform.gameObject
            : CreateUiPanel(panelRoot.transform, "Content", new Color(0.16f, 0.16f, 0.2f, 0.98f));

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(700f, 720f);
        contentRect.anchoredPosition = Vector2.zero;

        EnsureLabel(content.transform, "Title", "Settings", 44, new Vector2(0f, 280f));
        Slider musicSlider = EnsureSlider(content.transform, "MusicSlider", "Music", new Vector2(0f, 140f));
        Slider sfxSlider = EnsureSlider(content.transform, "SfxSlider", "SFX", new Vector2(0f, 20f));
        TMP_Dropdown languageDropdown = EnsureLanguageDropdown(content.transform, new Vector2(0f, -120f));
        Button closeButton = EnsureButton(content.transform, "CloseButton", "Close", new Vector2(0f, -260f));

        SettingsPanelView view = host.GetComponent<SettingsPanelView>();
        if (view == null)
            view = host.AddComponent<SettingsPanelView>();

        // Prefer host-level component; remove mistaken panel-root copies from older setup runs.
        SettingsPanelView onPanel = panelRoot.GetComponent<SettingsPanelView>();
        if (onPanel != null && onPanel != view)
            Object.DestroyImmediate(onPanel);

        AudioClipsConfig audioConfig = AssetDatabase.LoadAssetAtPath<AudioClipsConfig>(AudioClipsConfigPath);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
        so.FindProperty("_musicVolumeSlider").objectReferenceValue = musicSlider;
        so.FindProperty("_sfxVolumeSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("_languageDropdown").objectReferenceValue = languageDropdown;
        so.FindProperty("_openButton").objectReferenceValue = null;
        so.FindProperty("_closeButton").objectReferenceValue = closeButton;
        so.FindProperty("_menuAudioClipsConfig").objectReferenceValue = audioConfig;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (audioConfig == null)
            Debug.LogWarning($"StartSceneMainMenuSetup: AudioClipsConfig not found at {AudioClipsConfigPath}. Volume sliders will not apply mixer changes until assigned.");

        panelRoot.SetActive(false);
        return view;
    }

    private static Slider EnsureSlider(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        EnsureLabel(parent, name + "Label", label, 28, anchoredPosition + new Vector2(0f, 50f));

        Transform existing = parent.Find(name);
        GameObject sliderGo;
        if (existing != null)
        {
            sliderGo = existing.gameObject;
        }
        else
        {
            sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.name = name;
            sliderGo.transform.SetParent(parent, false);
        }

        RectTransform rect = sliderGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 36f);
        rect.anchoredPosition = anchoredPosition;

        Slider slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        return slider;
    }

    private static TMP_Dropdown EnsureLanguageDropdown(Transform parent, Vector2 anchoredPosition)
    {
        EnsureLabel(parent, "LanguageLabel", "Language", 28, anchoredPosition + new Vector2(0f, 50f));

        Transform existing = parent.Find("LanguageDropdown");
        GameObject dropdownGo;
        if (existing != null)
        {
            dropdownGo = existing.gameObject;
        }
        else
        {
            dropdownGo = new GameObject("LanguageDropdown", typeof(RectTransform));
            dropdownGo.transform.SetParent(parent, false);
            Image image = dropdownGo.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 1f);
            TMP_Dropdown dropdown = dropdownGo.AddComponent<TMP_Dropdown>();

            GameObject labelGo = CreateTmpText(dropdownGo.transform, "Label", "Language", 26, TextAlignmentOptions.Left);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            StretchWithPadding(labelRect, 16f, 16f, 8f, 8f);

            GameObject template = CreateUiPanel(dropdownGo.transform, "Template", new Color(0.2f, 0.2f, 0.25f, 1f));
            template.SetActive(false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 180f);

            ScrollRect scroll = template.AddComponent<ScrollRect>();
            GameObject viewport = CreateUiPanel(template.transform, "Viewport", new Color(1f, 1f, 1f, 1f));
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = Color.white;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 40f);

            GameObject item = CreateUiPanel(content.transform, "Item", new Color(1f, 1f, 1f, 0f));
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 40f);

            Toggle toggle = item.AddComponent<Toggle>();
            GameObject itemLabel = CreateTmpText(item.transform, "Item Label", "Option", 24, TextAlignmentOptions.Left);
            StretchWithPadding(itemLabel.GetComponent<RectTransform>(), 16f, 16f, 4f, 4f);

            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            dropdown.template = templateRect;
            dropdown.captionText = labelGo.GetComponent<TextMeshProUGUI>();
            dropdown.itemText = itemLabel.GetComponent<TextMeshProUGUI>();
            toggle.targetGraphic = item.GetComponent<Image>();
        }

        RectTransform dropdownRect = dropdownGo.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        dropdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        dropdownRect.pivot = new Vector2(0.5f, 0.5f);
        dropdownRect.sizeDelta = new Vector2(520f, 48f);
        dropdownRect.anchoredPosition = anchoredPosition;
        return dropdownGo.GetComponent<TMP_Dropdown>();
    }

    private static Button EnsureButton(Transform parent, string name, string text, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(name);
        GameObject buttonGo;
        if (existing != null)
        {
            buttonGo = existing.gameObject;
        }
        else
        {
            buttonGo = DefaultControls.CreateButton(new DefaultControls.Resources());
            buttonGo.name = name;
            buttonGo.transform.SetParent(parent, false);
            Transform legacyText = buttonGo.transform.Find("Text");
            if (legacyText != null)
                Object.DestroyImmediate(legacyText.gameObject);

            CreateTmpText(buttonGo.transform, "Text (TMP)", text, 30, TextAlignmentOptions.Center);
        }

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 90f);
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI label = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = text;

        return buttonGo.GetComponent<Button>();
    }

    private static void EnsureLabel(Transform parent, string name, string text, float fontSize, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(name);
        GameObject labelGo = existing != null
            ? existing.gameObject
            : CreateTmpText(parent, name, text, fontSize, TextAlignmentOptions.Center);

        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 50f);
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject CreateUiPanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateTmpText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
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

    private static void StretchWithPadding(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        StretchFull(go.GetComponent<RectTransform>());
        return go;
    }

    private static Button FindButton(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static void EnsureBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(StartScenePath, true));

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == StartScenePath)
                continue;
            scenes.Add(scene);
        }

        if (!scenes.Exists(scene => scene.path == "Assets/Scenes/GameplayScene.unity"))
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/GameplayScene.unity", true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
