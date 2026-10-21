#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Zenject;

/// <summary>
/// Runtime debug panel. Editor only; stripped from player builds.
/// Toggle with F1 (desktop) or triple-tap top-left corner (mobile).
/// Auto-created after scene load; injects via SceneContext.
/// </summary>
public class RuntimeDebugCheats : MonoBehaviour
{
    private const Key ToggleKey = Key.F1;
    private const int ResourceStep = 100;
    private const int MobileToggleTapCount = 3;
    private const float MobileToggleTapWindow = 0.8f;
    private const float MobileToggleCornerSize = 72f;

    [SerializeField] private bool _startVisible;

    private bool _visible;
    private Vector2 _scroll;
    private ISaveLoader _saveLoader;
    private IslandProgressRegistry _islandRegistry;
    private Inventory _inventory;
    private AllInstruments _allInstruments;
    private AllEquipment _allEquipment;
    private TutorialHandler _tutorialHandler;

    private string[] _resourceNames;
    private int _selectedResourceIndex;
    private string _exactAmountText = "100";

    private int _cornerTapCount;
    private float _cornerTapWindowEnd;

    [Inject]
    private void Construct(
        ISaveLoader saveLoader,
        IslandProgressRegistry islandRegistry,
        Inventory inventory,
        AllInstruments allInstruments,
        AllEquipment allEquipment)
    {
        _saveLoader = saveLoader;
        _islandRegistry = islandRegistry;
        _inventory = inventory;
        _allInstruments = allInstruments;
        _allEquipment = allEquipment;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<RuntimeDebugCheats>(FindObjectsInactive.Include) != null)
            return;

        var go = new GameObject(nameof(RuntimeDebugCheats));
        go.AddComponent<RuntimeDebugCheats>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        _resourceNames = Enum.GetNames(typeof(ResourceType));
        TryInjectFromSceneContext();
    }

    private void Start()
    {
        _visible = _startVisible;
        CacheTutorialHandler();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
            _visible = !_visible;

        if (Application.isMobilePlatform)
            TryHandleMobileCornerToggle();
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        const float width = 340f;
        const float height = 680f;
        Rect area = new Rect(12f, 12f, width, height);

        GUI.Box(area, "Debug Cheats (F1 / 3× corner)");
        GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 28f, area.width - 20f, area.height - 38f));

        if (GUILayout.Button("Close", GUILayout.Height(32f)))
            _visible = false;

        GUILayout.Space(4f);
        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawTutorialSection();
        GUILayout.Space(8f);
        DrawSaveSection();
        GUILayout.Space(8f);
        DrawIslandsSection();
        GUILayout.Space(8f);
        DrawInstrumentsSection();
        GUILayout.Space(8f);
        DrawEquipmentSection();
        GUILayout.Space(8f);
        DrawResourcesSection();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void TryHandleMobileCornerToggle()
    {
        if (!TryGetPressScreenPosition(out Vector2 pressPos))
            return;

        Rect corner = new Rect(
            0f,
            Screen.height - MobileToggleCornerSize,
            MobileToggleCornerSize,
            MobileToggleCornerSize);

        if (!corner.Contains(pressPos))
        {
            _cornerTapCount = 0;
            return;
        }

        float now = Time.unscaledTime;
        if (now > _cornerTapWindowEnd)
            _cornerTapCount = 0;

        _cornerTapCount++;
        _cornerTapWindowEnd = now + MobileToggleTapWindow;

        if (_cornerTapCount < MobileToggleTapCount)
            return;

        _cornerTapCount = 0;
        _visible = !_visible;
    }

    private static bool TryGetPressScreenPosition(out Vector2 position)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            position = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }

        // WebGL mobile often maps touches to the mouse device.
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private void DrawTutorialSection()
    {
        if (GUILayout.Button("Skip Tutorial", GUILayout.Height(32f)))
            SkipTutorial();
    }

    private void DrawSaveSection()
    {
        GUILayout.Label("Save");
        if (GUILayout.Button("Reset World 2 Progress", GUILayout.Height(32f)))
            ResetWorld2Progress();
    }

    private void DrawIslandsSection()
    {
        if (GUILayout.Button("Open All Islands", GUILayout.Height(32f)))
            OpenAllIslands();

        GUILayout.Space(4f);
        GUILayout.Label("Open island by index:");

        if (_islandRegistry != null)
        {
            foreach (IslandProgressHandler island in _islandRegistry.Islands.OrderBy(i => i.IslandIndex))
            {
                if (GUILayout.Button($"Island {island.IslandIndex}", GUILayout.Height(28f)))
                    OpenIsland(island);
            }
        }
        else
        {
            GUILayout.Label("(island registry not ready)");
        }
    }

    private void DrawInstrumentsSection()
    {
        GUILayout.Label("Instruments");

        if (!EnsureInventory() || !EnsureAllInstruments())
        {
            GUILayout.Label("(instruments not ready)");
            return;
        }

        DrawUpgradeInstrumentButton(InstrumentType.Axe);
        DrawUpgradeInstrumentButton(InstrumentType.Pickaxe);
        DrawUpgradeInstrumentButton(InstrumentType.Sword);
    }

    private void DrawEquipmentSection()
    {
        GUILayout.Label("Equipment");

        if (!EnsureInventory() || !EnsureAllEquipment())
        {
            GUILayout.Label("(equipment not ready)");
            return;
        }

        DrawApplyEquipmentButton(EquipmentType.Armor);
        DrawApplyEquipmentButton(EquipmentType.Helmet);
    }

    private void DrawApplyEquipmentButton(EquipmentType type)
    {
        int currentLevel = _inventory.GetEquipmentLevel(type);
        EquipmentConfig nextConfig = GetNextEquipmentConfig(type, currentLevel);
        string label = currentLevel <= 0
            ? (nextConfig == null ? $"Equip {type} (missing Lv 1)" : $"Equip {type} (Lv {nextConfig.Level})")
            : (nextConfig == null
                ? $"Upgrade {type} (max Lv {currentLevel})"
                : $"Upgrade {type} (Lv {currentLevel} → {nextConfig.Level})");

        GUI.enabled = nextConfig != null;
        if (GUILayout.Button(label, GUILayout.Height(28f)))
            ApplyEquipment(type);
        GUI.enabled = true;
    }

    private void DrawUpgradeInstrumentButton(InstrumentType type)
    {
        Instrument instrument = _inventory.Instruments.FirstOrDefault(item => item.Type == type);
        if (instrument == null)
        {
            GUILayout.Label($"{type}: missing");
            return;
        }

        InstrumentConfig nextConfig = GetNextInstrumentConfig(type, instrument.Level);
        string label = nextConfig == null
            ? $"Upgrade {type} (max Lv {instrument.Level})"
            : $"Upgrade {type} (Lv {instrument.Level} → {nextConfig.Level})";

        GUI.enabled = nextConfig != null;
        if (GUILayout.Button(label, GUILayout.Height(28f)))
            UpgradeInstrument(type);
        GUI.enabled = true;
    }

    private void DrawResourcesSection()
    {
        GUILayout.Label("Resources");

        if (!EnsureInventory())
        {
            GUILayout.Label("(inventory not ready)");
            return;
        }

        ResourceType selectedType = (ResourceType)_selectedResourceIndex;
        int currentAmount = _inventory.GetResourceAmount(selectedType);
        GUILayout.Label($"Current: {selectedType} = {currentAmount}");

        _selectedResourceIndex = GUILayout.SelectionGrid(
            _selectedResourceIndex,
            _resourceNames,
            2);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"+{ResourceStep}", GUILayout.Height(28f)))
            AddSelectedResource(ResourceStep);

        if (GUILayout.Button($"-{ResourceStep}", GUILayout.Height(28f)))
            RemoveSelectedResource(ResourceStep);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Exact:", GUILayout.Width(50f));
        _exactAmountText = GUILayout.TextField(_exactAmountText, GUILayout.Width(80f));
        if (GUILayout.Button("Set", GUILayout.Height(28f)))
            SetSelectedResourceExact();
        if (GUILayout.Button("Add", GUILayout.Height(28f)))
            AddSelectedResourceExact();
        GUILayout.EndHorizontal();
    }

    private void SkipTutorial()
    {
        CacheTutorialHandler();
        if (_tutorialHandler == null)
        {
            Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: TutorialHandler not found.");
            return;
        }

        _tutorialHandler.SkipTutorial();
        Debug.Log($"{nameof(RuntimeDebugCheats)}: tutorial skipped.");
    }

    private void ResetWorld2Progress()
    {
        TryInjectFromSceneContext();
        bool reset = _saveLoader != null
            ? _saveLoader.TryResetWorldProgress(SaveWorldIds.World2)
            : SaveWorldAccess.TryResetInStorage(new PlayerPrefsSaveLoader(), SaveWorldIds.World2);

        if (!reset)
        {
            Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: World 2 progress was not reset.");
            return;
        }

        Debug.Log($"{nameof(RuntimeDebugCheats)}: World 2 progress reset. Reloading scene.");
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

    private void OpenAllIslands()
    {
        if (!EnsureIslandRegistry())
            return;

        foreach (IslandProgressHandler island in _islandRegistry.Islands)
            island.ForceFullyOpen();

        _saveLoader?.DisableCompletedOpeningHexagons();
        _saveLoader?.Save();
        Debug.Log($"{nameof(RuntimeDebugCheats)}: opened all islands.");
    }

    private void OpenIsland(IslandProgressHandler island)
    {
        if (island == null)
            return;

        island.ForceFullyOpen();
        _saveLoader?.DisableCompletedOpeningHexagons();
        _saveLoader?.Save();
        Debug.Log($"{nameof(RuntimeDebugCheats)}: opened island {island.IslandIndex}.");
    }

    private void UpgradeInstrument(InstrumentType type)
    {
        if (!EnsureInventory() || !EnsureAllInstruments())
            return;

        Instrument instrument = _inventory.Instruments.FirstOrDefault(item => item.Type == type);
        if (instrument == null)
        {
            Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: instrument {type} not found.");
            return;
        }

        InstrumentConfig nextConfig = GetNextInstrumentConfig(type, instrument.Level);
        if (nextConfig == null)
        {
            Debug.Log($"{nameof(RuntimeDebugCheats)}: {type} already at max level {instrument.Level}.");
            return;
        }

        _inventory.UpgradeInstrument(nextConfig);
        RefreshInstrumentUpgrader(type);
        _saveLoader?.Save();
        Debug.Log($"{nameof(RuntimeDebugCheats)}: upgraded {type} to level {nextConfig.Level}.");
    }

    private void ApplyEquipment(EquipmentType type)
    {
        if (!EnsureInventory() || !EnsureAllEquipment())
            return;

        int currentLevel = _inventory.GetEquipmentLevel(type);
        EquipmentConfig nextConfig = GetNextEquipmentConfig(type, currentLevel);
        if (nextConfig == null)
        {
            Debug.Log($"{nameof(RuntimeDebugCheats)}: {type} already at max level {currentLevel}.");
            return;
        }

        _inventory.ApplyEquipment(nextConfig);
        RefreshEquipmentUpgrader(type);
        _saveLoader?.Save();
        string action = currentLevel <= 0 ? "equipped" : "upgraded";
        Debug.Log($"{nameof(RuntimeDebugCheats)}: {action} {type} to level {nextConfig.Level}.");
    }

    private InstrumentConfig GetNextInstrumentConfig(InstrumentType type, int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        return _allInstruments.Instruments.FirstOrDefault(
            config => config.Type == type && config.Level == nextLevel);
    }

    private static void RefreshInstrumentUpgrader(InstrumentType type)
    {
        InstrumentUpgrader[] upgraders = FindObjectsByType<InstrumentUpgrader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (InstrumentUpgrader upgrader in upgraders)
        {
            if (upgrader.InstrumentType == type)
                upgrader.RefreshFromInventory();
        }
    }

    private EquipmentConfig GetNextEquipmentConfig(EquipmentType type, int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        return _allEquipment.Get(type, nextLevel);
    }

    private static void RefreshEquipmentUpgrader(EquipmentType type)
    {
        EquipmentUpgrader[] upgraders = FindObjectsByType<EquipmentUpgrader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (EquipmentUpgrader upgrader in upgraders)
        {
            if (upgrader.EquipmentType == type)
                upgrader.RefreshFromInventory();
        }
    }

    private void AddSelectedResource(int amount)
    {
        if (!EnsureInventory() || amount <= 0)
            return;

        ResourceType type = (ResourceType)_selectedResourceIndex;
        _inventory.AddResourceWithoutLifetime(type, amount);
    }

    private void RemoveSelectedResource(int amount)
    {
        if (!EnsureInventory() || amount <= 0)
            return;

        ResourceType type = (ResourceType)_selectedResourceIndex;
        int current = _inventory.GetResourceAmount(type);
        int toRemove = Mathf.Min(amount, current);
        if (toRemove > 0)
            _inventory.RemoveResource(type, toRemove);
    }

    private void SetSelectedResourceExact()
    {
        if (!EnsureInventory() || !TryParseNonNegativeInt(_exactAmountText, out int targetAmount))
            return;

        ResourceType type = (ResourceType)_selectedResourceIndex;
        int current = _inventory.GetResourceAmount(type);

        if (targetAmount > current)
            _inventory.AddResourceWithoutLifetime(type, targetAmount - current);
        else if (targetAmount < current)
            _inventory.RemoveResource(type, current - targetAmount);
    }

    private void AddSelectedResourceExact()
    {
        if (!EnsureInventory() || !TryParseNonNegativeInt(_exactAmountText, out int amount) || amount <= 0)
            return;

        AddSelectedResource(amount);
    }

    private static bool TryParseNonNegativeInt(string text, out int value)
    {
        if (int.TryParse(text, out value) && value >= 0)
            return true;

        value = 0;
        Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: enter a non-negative integer amount.");
        return false;
    }

    private void CacheTutorialHandler()
    {
        if (_tutorialHandler == null)
            _tutorialHandler = FindFirstObjectByType<TutorialHandler>(FindObjectsInactive.Include);
    }

    private bool EnsureIslandRegistry()
    {
        TryInjectFromSceneContext();
        if (_islandRegistry != null)
            return true;

        Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: IslandProgressRegistry not injected yet.");
        return false;
    }

    private bool EnsureInventory()
    {
        TryInjectFromSceneContext();
        if (_inventory != null)
            return true;

        Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: Inventory not injected yet.");
        return false;
    }

    private bool EnsureAllInstruments()
    {
        TryInjectFromSceneContext();
        if (_allInstruments != null)
            return true;

        Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: AllInstruments not injected yet.");
        return false;
    }

    private bool EnsureAllEquipment()
    {
        TryInjectFromSceneContext();
        if (_allEquipment != null)
            return true;

        Debug.LogWarning($"{nameof(RuntimeDebugCheats)}: AllEquipment not injected yet.");
        return false;
    }

    private void TryInjectFromSceneContext()
    {
        if (_saveLoader != null && _islandRegistry != null && _inventory != null && _allInstruments != null && _allEquipment != null)
            return;

        SceneContext sceneContext = FindFirstObjectByType<SceneContext>();
        if (sceneContext == null || sceneContext.Container == null)
            return;

        sceneContext.Container.Inject(this);
    }
}
#endif
