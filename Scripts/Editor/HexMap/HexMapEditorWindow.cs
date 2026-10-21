using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HexMapEditorWindow : EditorWindow
{
    private const string MenuPath = "Tools/Hex Map Builder";

    private HexMapPalette _palette;
    private HexMapRoot _mapRoot;
    private HexMapPaintMode _paintMode = HexMapPaintMode.Ground;
    private readonly HexMapEditorPreview _preview = new();

    private static readonly HexMapPaintMode[] ToolbarModes =
    {
        HexMapPaintMode.Ground,
        HexMapPaintMode.Environment,
        HexMapPaintMode.ResourceHolder,
        HexMapPaintMode.RewardedResourceSpot,
        HexMapPaintMode.InstrumentUpgrader,
        HexMapPaintMode.EnemySpawnPoint,
        HexMapPaintMode.OneTimeEnemySpawnPoint,
        HexMapPaintMode.OpeningHex,
        HexMapPaintMode.LockHex,
        HexMapPaintMode.Erase
    };

    private static readonly string[] ToolbarLabels =
    {
        "Гекс", "Окружение", "Ресурс", "Ревард", "Апгрейдер", "Враг", "Враг 1x", "Открытие", "Блокировка", "Ластик"
    };

    private int _selectedGroundIndex;
    private int _selectedEnvironmentIndex;
    private int _selectedResourceIndex;
    private int _selectedRewardedResourceIndex;
    private int _selectedUpgraderIndex;
    private int _selectedEnemySpawnPointIndex;

    private bool _showGrid = true;
    private int _gridRadius = 12;
    private float _groundHeight;
    private bool _groundStartsLocked;
    private bool _randomizeEnvironmentRotation = true;
    private bool _randomizeEnvironmentScale;
    private float _environmentScaleMin = 0.7f;
    private float _environmentScaleMax = 1.3f;
    private bool _randomizeResourceScale;
    private float _resourceScaleMin = 0.7f;
    private float _resourceScaleMax = 1.3f;
    private float _placementRotation;
    private int _rewardedResourceAmount = 10;
    private bool _rewardedResourceOneShot;
    private EnemyInitConfig _enemySpawnConfig;
    private float _enemySpawnCooldown = 5f;
    private float _enemyGuardRadius = 5f;
    private float _enemySpawnTriggerOffset = 5f;

    private bool _editModeEnabled;
    private GameObject _openingTargetHex;
    private Vector2Int _openingTargetHexCoord;
    private bool _hasOpeningTarget;
    private Vector2Int _hoverHex;
    private bool _hasHoverHex;
    private HexPlacementInfo _placementInfo;
    private bool _hasValidPlacement;
    private Vector2 _scroll;
    private readonly List<OpeningHexPriceEntry> _openingHexPrices = new()
    {
        new OpeningHexPriceEntry { ResourceType = ResourceType.Wood, Amount = 10 }
    };

    private static readonly Vector3[] CornerBuffer = new Vector3[6];

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var window = GetWindow<HexMapEditorWindow>("Hex Map Builder");
        window.minSize = new Vector2(320f, 520f);
        window.Show();
    }

    [MenuItem(MenuPath + "/Quick Setup")]
    public static void QuickSetup()
    {
        Open();

        var window = GetWindow<HexMapEditorWindow>();
        var existingPalette = AssetDatabase.LoadAssetAtPath<HexMapPalette>("Assets/Configs/HexMapPalette.asset");

        if (existingPalette == null)
        {
            var palette = CreateInstance<HexMapPalette>();
            AssetDatabase.CreateAsset(palette, "Assets/Configs/HexMapPalette.asset");
            TryPopulateDefaultPalette(palette);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            existingPalette = palette;
        }

        var mapRoot = Object.FindFirstObjectByType<HexMapRoot>();
        if (mapRoot == null)
        {
            var go = new GameObject("HexMap");
            Undo.RegisterCreatedObjectUndo(go, "Create Hex Map Root");
            mapRoot = go.AddComponent<HexMapRoot>();
            mapRoot.EnsureContainers();
            Selection.activeGameObject = go;
        }

        window.SetContext(existingPalette, mapRoot);
        window._editModeEnabled = true;
        HexMapEditorState.EditModeEnabled = true;
    }

    public void SetContext(HexMapPalette palette, HexMapRoot mapRoot)
    {
        _palette = palette;
        _mapRoot = mapRoot;
        RefreshLockedHexVisuals();
        Repaint();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        _editModeEnabled = HexMapEditorState.EditModeEnabled;
        RefreshLockedHexVisuals();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        HexMapEditorState.EditModeEnabled = false;
        HexMapEditorLockedHexVisual.RefreshAll(_mapRoot, false);
        HexMapEditorLockedHexVisual.ClearAll();
        _preview.Dispose();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            RefreshLockedHexVisuals();
    }

    private void RefreshLockedHexVisuals()
    {
        if (_mapRoot == null)
            return;

        HexMapEditorLockedHexVisual.RefreshAll(_mapRoot, ShouldShowLockedHexVisual());
    }

    private bool ShouldShowLockedHexVisual()
    {
        return _editModeEnabled
            && !EditorApplication.isPlaying
            && (_paintMode == HexMapPaintMode.LockHex || _paintMode == HexMapPaintMode.OpeningHex);
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Hex Map Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        _palette = (HexMapPalette)EditorGUILayout.ObjectField("Palette", _palette, typeof(HexMapPalette), false);
        _mapRoot = (HexMapRoot)EditorGUILayout.ObjectField("Map Root", _mapRoot, typeof(HexMapRoot), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Map Root"))
            CreateMapRoot();
        if (GUILayout.Button("Create Palette"))
            CreatePaletteAsset();
        EditorGUILayout.EndHorizontal();

        if (_palette == null)
        {
            EditorGUILayout.HelpBox("Назначьте или создайте HexMapPalette с префабами гексов, окружения и геймплейных объектов.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (_mapRoot == null)
        {
            EditorGUILayout.HelpBox("Создайте или выберите HexMapRoot в сцене.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUI.BeginChangeCheck();
        _editModeEnabled = EditorGUILayout.ToggleLeft("Режим редактирования (Edit Mode)", _editModeEnabled, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            HexMapEditorState.EditModeEnabled = _editModeEnabled;
            if (!_editModeEnabled)
            {
                _preview.Hide();
                ClearOpeningTarget();
            }
            RefreshLockedHexVisuals();
            SceneView.RepaintAll();
        }

        if (!_editModeEnabled)
        {
            EditorGUILayout.HelpBox(
                "Edit Mode выключен — Unity работает как обычно. Включите для рисования карты.",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            HandleRotationInput();
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Режим", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int toolbarIndex = System.Array.IndexOf(ToolbarModes, _paintMode);
        if (toolbarIndex < 0)
            toolbarIndex = 0;
        toolbarIndex = GUILayout.Toolbar(toolbarIndex, ToolbarLabels);
        if (EditorGUI.EndChangeCheck())
        {
            _paintMode = ToolbarModes[toolbarIndex];
            ClearOpeningTarget();
            RefreshLockedHexVisuals();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4f);
        DrawModeSettings();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Сетка", EditorStyles.boldLabel);
        _showGrid = EditorGUILayout.Toggle("Показать сетку", _showGrid);
        _gridRadius = EditorGUILayout.IntSlider("Радиус сетки", _gridRadius, 4, 40);

        if (_hasHoverHex)
            EditorGUILayout.LabelField($"Курсор: q={_hoverHex.x}, r={_hoverHex.y}");

        if (_hasValidPlacement)
            EditorGUILayout.LabelField($"Высота поверхности: {_placementInfo.SurfaceY:F2}");

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "ЛКМ — поставить\nПКМ — удалить / сбросить выбор (Открытие) / разблокировать (Блокировка)\nShift+ЛКМ — залить гексы (3x3, только «Гекс»)\nКолёсико — высота гекса (режим «Гекс»)\nQ / E — поворот\nEsc — ластик / сброс (Открытие)",
            MessageType.None);

        HandleRotationInput();
        EditorGUILayout.EndScrollView();
    }

    private void HandleRotationInput()
    {
        if (!_editModeEnabled)
            return;

        if (_paintMode == HexMapPaintMode.Environment && _randomizeEnvironmentRotation)
            return;

        var e = Event.current;
        if (e.type != EventType.KeyDown)
            return;

        if (e.keyCode == KeyCode.Q)
        {
            _placementRotation = (_placementRotation - 15f + 360f) % 360f;
            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }
        else if (e.keyCode == KeyCode.E)
        {
            _placementRotation = (_placementRotation + 15f) % 360f;
            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private void DrawModeSettings()
    {
        switch (_paintMode)
        {
            case HexMapPaintMode.Ground:
                _selectedGroundIndex = DrawPrefabPopup("Префаб грунта", _palette.GroundPrefabs, _selectedGroundIndex);
                _groundHeight = EditorGUILayout.FloatField("Высота гекса", _groundHeight);
                _groundStartsLocked = EditorGUILayout.Toggle("Заблокированный гекс", _groundStartsLocked);
                if (_groundStartsLocked)
                    EditorGUILayout.HelpBox("Гекс будет неактивен до открытия через OpeningHexagon.", MessageType.Info);
                break;

            case HexMapPaintMode.Environment:
                _selectedEnvironmentIndex = DrawPrefabPopup("Префаб окружения", _palette.EnvironmentPrefabs, _selectedEnvironmentIndex);
                _randomizeEnvironmentRotation = EditorGUILayout.Toggle("Случайный поворот", _randomizeEnvironmentRotation);
                if (!_randomizeEnvironmentRotation)
                    _placementRotation = EditorGUILayout.Slider("Поворот Y", _placementRotation, 0f, 360f);
                DrawScaleRandomizationSettings(
                    ref _randomizeEnvironmentScale,
                    ref _environmentScaleMin,
                    ref _environmentScaleMax);
                break;

            case HexMapPaintMode.ResourceHolder:
                _selectedResourceIndex = DrawPrefabPopup("ResourseHolderView", _palette.ResourceHolderPrefabs, _selectedResourceIndex);
                _placementRotation = EditorGUILayout.Slider("Поворот Y", _placementRotation, 0f, 360f);
                DrawScaleRandomizationSettings(
                    ref _randomizeResourceScale,
                    ref _resourceScaleMin,
                    ref _resourceScaleMax);
                break;

            case HexMapPaintMode.RewardedResourceSpot:
                _selectedRewardedResourceIndex = DrawPrefabPopup(
                    "RewardedResourceSpot",
                    _palette.RewardedResourceSpotPrefabs,
                    _selectedRewardedResourceIndex);
                _rewardedResourceAmount = EditorGUILayout.IntField("Количество", _rewardedResourceAmount);
                _rewardedResourceAmount = Mathf.Max(1, _rewardedResourceAmount);
                _rewardedResourceOneShot = EditorGUILayout.Toggle("One Shot", _rewardedResourceOneShot);
                _placementRotation = EditorGUILayout.Slider("Поворот Y", _placementRotation, 0f, 360f);
                break;

            case HexMapPaintMode.InstrumentUpgrader:
                _selectedUpgraderIndex = DrawPrefabPopup("InstrumentUpgrader", _palette.InstrumentUpgraderPrefabs, _selectedUpgraderIndex);
                _placementRotation = EditorGUILayout.Slider("Поворот Y", _placementRotation, 0f, 360f);
                break;

            case HexMapPaintMode.EnemySpawnPoint:
            case HexMapPaintMode.OneTimeEnemySpawnPoint:
                DrawEnemySpawnPointFields();
                break;

            case HexMapPaintMode.OpeningHex:
                EditorGUILayout.ObjectField("OpeningHex prefab", _palette.OpeningHexPrefab, typeof(GameObject), false);
                _placementRotation = EditorGUILayout.Slider("Поворот Y", _placementRotation, 0f, 360f);
                HexMapEditorOpeningHexPriceUtility.DrawPriceEditor(_openingHexPrices);

                if (!_hasOpeningTarget)
                {
                    EditorGUILayout.HelpBox(
                        "Шаг 1: кликните по заблокированному гексу, чтобы выбрать цель открытия.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Шаг 2: выбран гекс ({_openingTargetHexCoord.x}, {_openingTargetHexCoord.y}). Кликните, чтобы поставить открывашку.",
                        MessageType.Warning);
                    if (GUILayout.Button("Сбросить выбор"))
                        ClearOpeningTarget();
                }
                break;

            case HexMapPaintMode.LockHex:
                EditorGUILayout.HelpBox(
                    "ЛКМ — заблокировать существующий гекс.\nПКМ — снять блокировку.\nЗаблокированный гекс неактивен в игре до открытия через OpeningHexagon.",
                    MessageType.Info);

                if (_hasHoverHex && _placementInfo.HexTile != null)
                {
                    var status = _placementInfo.HexTile.StartsLocked ? "заблокирован" : "открыт";
                    EditorGUILayout.LabelField($"Гекс ({_hoverHex.x}, {_hoverHex.y}): {status}");
                }
                break;
        }
    }

    private void ClearOpeningTarget()
    {
        _openingTargetHex = null;
        _hasOpeningTarget = false;
        Repaint();
        SceneView.RepaintAll();
    }

    private static void DrawScaleRandomizationSettings(ref bool randomizeScale, ref float minScale, ref float maxScale)
    {
        randomizeScale = EditorGUILayout.Toggle("Случайный скейл", randomizeScale);
        if (!randomizeScale)
            return;

        EditorGUILayout.BeginHorizontal();
        minScale = EditorGUILayout.FloatField("Скейл мин", minScale);
        maxScale = EditorGUILayout.FloatField("Скейл макс", maxScale);
        EditorGUILayout.EndHorizontal();

        if (minScale > maxScale)
            (minScale, maxScale) = (maxScale, minScale);

        minScale = Mathf.Max(0.01f, minScale);
        maxScale = Mathf.Max(minScale, maxScale);
    }

    private static int DrawPrefabPopup(string label, IReadOnlyList<GameObject> prefabs, int selectedIndex)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            EditorGUILayout.HelpBox($"Добавьте префабы в палитру: {label}", MessageType.Warning);
            return 0;
        }

        var names = new string[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
            names[i] = prefabs[i] != null ? prefabs[i].name : "<missing>";

        selectedIndex = Mathf.Clamp(selectedIndex, 0, prefabs.Count - 1);
        return EditorGUILayout.Popup(label, selectedIndex, names);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        DrawSceneViewToolbar();

        if (!_editModeEnabled || _palette == null || _mapRoot == null)
            return;

        _mapRoot.EnsureContainers();
        float hexSize = _palette.HexSize;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        var e = Event.current;
        UpdatePlacement(e, hexSize);

        if (_showGrid)
            DrawHexGrid(hexSize);

        if (ShouldShowLockedHexVisual())
            DrawLockedHexes(hexSize);
        OpeningHexagonGizmoUtility.DrawAllInMap(_mapRoot);

        if (_hasHoverHex)
            DrawHoverHex(hexSize);

        UpdatePreview();
        HandleRotationInput();

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            if (_paintMode == HexMapPaintMode.OpeningHex && _hasOpeningTarget)
                ClearOpeningTarget();
            else
                _paintMode = HexMapPaintMode.Erase;
            RefreshLockedHexVisuals();
            e.Use();
        }

        if (e.type == EventType.ScrollWheel && _paintMode == HexMapPaintMode.Ground)
        {
            _groundHeight -= e.delta.y * 0.1f;
            _groundHeight = Mathf.Round(_groundHeight * 100f) / 100f;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.Layout)
            return;

        if (e.alt)
            return;

        if (e.type == EventType.MouseDown && e.button == 0 && !e.shift)
        {
            if (TryHandleOpeningHexClick(hexSize))
            {
                e.Use();
            }
            else if (_hasValidPlacement)
            {
                PlaceAtCurrentPosition(hexSize);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDown && e.button == 0 && e.shift && _paintMode == HexMapPaintMode.Ground)
        {
            PaintGroundArea(_hoverHex, hexSize, 1);
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 1)
        {
            if (_paintMode == HexMapPaintMode.OpeningHex && _hasOpeningTarget)
                ClearOpeningTarget();
            else if (_paintMode == HexMapPaintMode.LockHex && _hasHoverHex)
                UnlockHexAt(_hoverHex);
            else
                EraseAtCursor(e.mousePosition);
            e.Use();
        }

        sceneView.Repaint();
    }

    private void DrawSceneViewToolbar()
    {
        Handles.BeginGUI();

        var rect = new Rect(12f, 8f, 200f, 22f);
        EditorGUI.BeginChangeCheck();
        var enabled = GUI.Toggle(rect, _editModeEnabled, "Hex Map Edit Mode", EditorStyles.miniButton);
        if (EditorGUI.EndChangeCheck())
        {
            _editModeEnabled = enabled;
            HexMapEditorState.EditModeEnabled = enabled;
            if (!_editModeEnabled)
            {
                _preview.Hide();
                ClearOpeningTarget();
            }
            RefreshLockedHexVisuals();
            Repaint();
        }

        Handles.EndGUI();
    }

    private bool TryHandleOpeningHexClick(float hexSize)
    {
        if (_paintMode != HexMapPaintMode.OpeningHex || _hasOpeningTarget)
            return false;

        if (!_hasValidPlacement || _placementInfo.HexTile == null || !_placementInfo.HexTile.StartsLocked)
            return false;

        SelectOpeningTarget(_placementInfo.HexTile);
        return true;
    }

    private void SelectOpeningTarget(HexTile hexTile)
    {
        _openingTargetHexCoord = new Vector2Int(hexTile.Q, hexTile.R);
        _openingTargetHex = HexMapEditorPlacementUtility.FindGroundHex(_mapRoot, _openingTargetHexCoord);
        _hasOpeningTarget = _openingTargetHex != null;
        Repaint();
    }

    private void UpdatePlacement(Event e, float hexSize)
    {
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        _hasValidPlacement = false;
        _hasHoverHex = false;
        _placementInfo = HexPlacementInfo.Invalid;

        if (_paintMode == HexMapPaintMode.OpeningHex && !_hasOpeningTarget)
        {
            if (!HexMapEditorPlacementUtility.TryRaycastGroundHex(ray, _mapRoot, hexSize, out _placementInfo))
                return;

            if (_placementInfo.HexTile == null || !_placementInfo.HexTile.StartsLocked)
                return;

            _hoverHex = _placementInfo.Hex;
            _hasHoverHex = true;
            _hasValidPlacement = true;
            return;
        }

        if (RequiresExistingHex(_paintMode))
        {
            if (!HexMapEditorPlacementUtility.TryRaycastGroundHex(ray, _mapRoot, hexSize, out _placementInfo))
                return;

            if (!HexMapEditorPlacementUtility.IsPointInsideHex(
                    _placementInfo.LocalPosition,
                    _placementInfo.HexTile,
                    hexSize,
                    _mapRoot.transform))
                return;

            _hoverHex = _placementInfo.Hex;
            _hasHoverHex = true;
            _hasValidPlacement = _paintMode != HexMapPaintMode.LockHex
                || (_placementInfo.HexTile != null && !_placementInfo.HexTile.StartsLocked);
            return;
        }

        if (_paintMode == HexMapPaintMode.Ground)
        {
            if (!HexMapEditorPlacementUtility.TryGetGroundPlanePlacement(
                    ray, _mapRoot, hexSize, _groundHeight, out _placementInfo))
                return;

            _hoverHex = _placementInfo.Hex;
            _hasHoverHex = true;
            _hasValidPlacement = true;
            return;
        }

        if (_paintMode == HexMapPaintMode.Erase)
        {
            if (HexMapEditorPlacementUtility.TryRaycastGroundHex(ray, _mapRoot, hexSize, out _placementInfo))
            {
                _hoverHex = _placementInfo.Hex;
                _hasHoverHex = true;
            }
        }
    }

    private void UpdatePreview()
    {
        if (_paintMode == HexMapPaintMode.Erase || _paintMode == HexMapPaintMode.Ground || _paintMode == HexMapPaintMode.LockHex)
        {
            _preview.Hide();
            return;
        }

        if (_paintMode == HexMapPaintMode.OpeningHex && !_hasOpeningTarget)
        {
            _preview.Hide();
            return;
        }

        var prefab = GetCurrentPrefab();
        _preview.SetPrefab(prefab);

        if (!_hasValidPlacement || prefab == null)
        {
            _preview.Hide();
            return;
        }

        var hexParent = HexMapEditorPlacementUtility.GetHexParent(_mapRoot, _placementInfo.Hex);
        if (hexParent == null)
        {
            _preview.Hide();
            return;
        }

        float rotationY = GetPreviewRotation();
        var worldPosition = HexMapEditorPlacementUtility.GetPlacementPosition(
            prefab,
            _placementInfo.WorldPosition,
            rotationY,
            hexParent,
            GetPlacementSurfaceLift(_paintMode));
        var worldRotation = HexMapEditorPlacementUtility.GetPlacementRotation(rotationY);

        _preview.UpdateTransform(worldPosition, worldRotation, true);
    }

    private void DrawLockedHexes(float hexSize)
    {
        if (_mapRoot == null)
            return;

        var worldCorners = new Vector3[6];

        foreach (var hexTile in HexMapEditorPlacementUtility.EnumerateGroundHexTiles(_mapRoot))
        {
            if (!hexTile.StartsLocked)
                continue;

            bool isSelected = _hasOpeningTarget
                && _openingTargetHexCoord.x == hexTile.Q
                && _openingTargetHexCoord.y == hexTile.R;

            var localCenter = HexMapEditorPlacementUtility.GetHexCenterLocal(hexTile, _mapRoot.transform);
            HexCoordinates.GetHexCorners(localCenter, hexSize, CornerBuffer);

            for (int i = 0; i < 6; i++)
            {
                CornerBuffer[i].y = localCenter.y;
                worldCorners[i] = _mapRoot.transform.TransformPoint(CornerBuffer[i]);
            }

            Handles.color = isSelected
                ? new Color(1f, 0.85f, 0.2f, 0.45f)
                : new Color(1f, 0.35f, 0.2f, 0.3f);
            Handles.DrawAAConvexPolygon(worldCorners);

            Handles.color = isSelected
                ? new Color(1f, 0.9f, 0.2f, 0.95f)
                : new Color(1f, 0.4f, 0.2f, 0.85f);

            for (int i = 0; i < 6; i++)
                Handles.DrawLine(worldCorners[i], worldCorners[(i + 1) % 6]);
        }
    }

    private void DrawHexGrid(float hexSize)
    {
        Handles.color = new Color(0.4f, 0.8f, 1f, 0.25f);

        var drawnHexes = new HashSet<Vector2Int>();

        foreach (var hexTile in HexMapEditorPlacementUtility.EnumerateGroundHexTiles(_mapRoot))
        {
            drawnHexes.Add(new Vector2Int(hexTile.Q, hexTile.R));
            DrawHexOutline(hexTile, hexSize);
        }

        if (_paintMode != HexMapPaintMode.Ground)
            return;

        for (int q = -_gridRadius; q <= _gridRadius; q++)
        {
            for (int r = -_gridRadius; r <= _gridRadius; r++)
            {
                if (Mathf.Abs(q + r) > _gridRadius)
                    continue;

                var hex = new Vector2Int(q, r);
                if (drawnHexes.Contains(hex))
                    continue;

                DrawHexOutline(q, r, hexSize, _groundHeight);
            }
        }
    }

    private void DrawHexOutline(HexTile hexTile, float hexSize)
    {
        var localCenter = HexMapEditorPlacementUtility.GetHexCenterLocal(hexTile, _mapRoot.transform);
        DrawHexOutlineAtLocalCenter(hexTile.Q, hexTile.R, localCenter, hexSize);
    }

    private void DrawHexOutline(int q, int r, float hexSize, float height)
    {
        var localCenter = HexCoordinates.AxialToWorld(q, r, hexSize);
        localCenter.y = height;
        DrawHexOutlineAtLocalCenter(q, r, localCenter, hexSize);
    }

    private void DrawHexOutlineAtLocalCenter(int q, int r, Vector3 localCenter, float hexSize)
    {
        var center = _mapRoot.transform.TransformPoint(localCenter);
        HexCoordinates.GetHexCorners(localCenter, hexSize, CornerBuffer);

        for (int i = 0; i < 6; i++)
        {
            CornerBuffer[i].y = localCenter.y;
            var a = _mapRoot.transform.TransformPoint(CornerBuffer[i]);
            var b = _mapRoot.transform.TransformPoint(CornerBuffer[(i + 1) % 6]);
            Handles.DrawLine(a, b);
        }

        Handles.Label(center + Vector3.up * 0.05f, $"{q},{r}", EditorStyles.miniLabel);
    }

    private void DrawHoverHex(float hexSize)
    {
        float height = _paintMode == HexMapPaintMode.Ground
            ? _groundHeight
            : _placementInfo.HexTile != null
                ? _placementInfo.HexTile.Height
                : HexMapEditorPlacementUtility.GetHexSurfaceY(_mapRoot, _hoverHex, hexSize, _groundHeight) - _mapRoot.transform.position.y;

        if (_paintMode != HexMapPaintMode.Ground && _hasValidPlacement)
            height = _mapRoot.transform.InverseTransformPoint(_placementInfo.WorldPosition).y;

        Vector3 localCenter;
        if (_placementInfo.HexTile != null && _paintMode != HexMapPaintMode.Ground)
        {
            localCenter = HexMapEditorPlacementUtility.GetHexCenterLocal(_placementInfo.HexTile, _mapRoot.transform);
            localCenter.y = height;
        }
        else
        {
            localCenter = HexCoordinates.AxialToWorld(_hoverHex.x, _hoverHex.y, hexSize);
            localCenter.y = height;
        }

        HexCoordinates.GetHexCorners(localCenter, hexSize, CornerBuffer);

        Handles.color = !_hasValidPlacement || _paintMode == HexMapPaintMode.Erase
            ? new Color(1f, 0.3f, 0.3f, 0.8f)
            : _paintMode == HexMapPaintMode.LockHex
                ? _placementInfo.HexTile != null && _placementInfo.HexTile.StartsLocked
                    ? new Color(0.3f, 0.85f, 1f, 0.9f)
                    : new Color(1f, 0.45f, 0.15f, 0.9f)
                : _paintMode == HexMapPaintMode.OpeningHex && !_hasOpeningTarget
                    ? new Color(1f, 0.75f, 0.2f, 0.9f)
                    : new Color(0.2f, 1f, 0.4f, 0.8f);

        for (int i = 0; i < 6; i++)
        {
            CornerBuffer[i].y = localCenter.y;
            var a = _mapRoot.transform.TransformPoint(CornerBuffer[i]);
            var b = _mapRoot.transform.TransformPoint(CornerBuffer[(i + 1) % 6]);
            Handles.DrawLine(a, b);
        }
    }

    private void PlaceAtCurrentPosition(float hexSize)
    {
        if (_paintMode == HexMapPaintMode.Erase)
            return;

        switch (_paintMode)
        {
            case HexMapPaintMode.Ground:
                PaintGround(_placementInfo.Hex, hexSize);
                break;
            case HexMapPaintMode.Environment:
                PaintFreeObject(_palette.EnvironmentPrefabs, _selectedEnvironmentIndex, HexMapPaintMode.Environment);
                break;
            case HexMapPaintMode.ResourceHolder:
                PaintFreeObject(_palette.ResourceHolderPrefabs, _selectedResourceIndex, HexMapPaintMode.ResourceHolder);
                break;
            case HexMapPaintMode.RewardedResourceSpot:
                PaintRewardedResourceSpot();
                break;
            case HexMapPaintMode.InstrumentUpgrader:
                PaintFreeObject(_palette.InstrumentUpgraderPrefabs, _selectedUpgraderIndex, HexMapPaintMode.InstrumentUpgrader);
                break;
            case HexMapPaintMode.EnemySpawnPoint:
            case HexMapPaintMode.OneTimeEnemySpawnPoint:
                PaintEnemySpawnPoint();
                break;
            case HexMapPaintMode.OpeningHex:
                PaintOpeningHex();
                break;
            case HexMapPaintMode.LockHex:
                LockHexAt(_placementInfo.Hex);
                break;
        }
    }

    private void LockHexAt(Vector2Int hex)
    {
        var hexGo = HexMapEditorPlacementUtility.FindGroundHex(_mapRoot, hex);
        if (hexGo == null)
            return;

        var hexTile = hexGo.GetComponent<HexTile>();
        if (hexTile == null || hexTile.StartsLocked)
            return;

        Undo.RecordObject(hexTile, "Lock Hex");
        hexTile.SetStartsLocked(true);
        EditorUtility.SetDirty(hexTile);
        HexMapEditorLockedHexVisual.RefreshHex(hexGo, ShouldShowLockedHexVisual());
    }

    private void UnlockHexAt(Vector2Int hex)
    {
        var hexGo = HexMapEditorPlacementUtility.FindGroundHex(_mapRoot, hex);
        if (hexGo == null)
            return;

        var hexTile = hexGo.GetComponent<HexTile>();
        if (hexTile == null || !hexTile.StartsLocked)
            return;

        Undo.RecordObject(hexTile, "Unlock Hex");
        hexTile.SetStartsLocked(false);
        EditorUtility.SetDirty(hexTile);
        HexMapEditorLockedHexVisual.RefreshHex(hexGo, ShouldShowLockedHexVisual());
    }

    private void PaintGroundArea(Vector2Int center, float hexSize, int radius)
    {
        for (int dq = -radius; dq <= radius; dq++)
        {
            for (int dr = -radius; dr <= radius; dr++)
            {
                if (Mathf.Abs(dq + dr) > radius)
                    continue;

                PaintGround(new Vector2Int(center.x + dq, center.y + dr), hexSize);
            }
        }
    }

    private void PaintGround(Vector2Int hex, float hexSize)
    {
        var prefabs = _palette.GroundPrefabs;
        if (prefabs == null || prefabs.Count == 0 || _selectedGroundIndex >= prefabs.Count)
            return;

        var prefab = prefabs[_selectedGroundIndex];
        if (prefab == null)
            return;

        var existingHex = HexMapEditorPlacementUtility.FindGroundHex(_mapRoot, hex);
        var preservedChildren = new List<(Transform transform, Vector3 localPosition, Quaternion localRotation)>();
        if (existingHex != null)
        {
            foreach (var child in HexMapEditorPlacementUtility.CollectPlacedChildren(existingHex.transform))
            {
                preservedChildren.Add((child, child.localPosition, child.localRotation));
                Undo.SetTransformParent(child, _mapRoot.transform, "Detach Hex Child");
            }
        }

        HexTile oldHexTile = existingHex != null ? existingHex.GetComponent<HexTile>() : null;

        if (existingHex != null)
            Undo.DestroyObjectImmediate(existingHex);

        EraseLegacyPlacedObjectsAtHex(hex);

        Transform parent = existingHex != null ? existingHex.transform.parent : _mapRoot.Ground;
        Vector3 localPos;
        if (existingHex != null)
        {
            localPos = existingHex.transform.localPosition;
            if (parent == _mapRoot.Ground)
                localPos.y = _groundHeight;
        }
        else
        {
            localPos = HexCoordinates.AxialToWorld(hex.x, hex.y, hexSize);
            localPos.y = _groundHeight;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (instance == null)
            return;

        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.identity;
        instance.name = $"{prefab.name} ({hex.x},{hex.y})";

        var hexTile = instance.GetComponent<HexTile>();
        if (hexTile == null)
            hexTile = instance.AddComponent<HexTile>();
        hexTile.Initialize(hex.x, hex.y, _groundHeight, _groundStartsLocked);

        var placed = instance.GetComponent<HexMapPlacedObject>();
        if (placed == null)
            placed = instance.AddComponent<HexMapPlacedObject>();
        placed.Initialize(HexMapPaintMode.Ground, hex.x, hex.y);

        foreach (var (child, localPosition, localRotation) in preservedChildren)
        {
            Undo.SetTransformParent(child, instance.transform, "Reparent Hex Child");
            child.localPosition = localPosition;
            child.localRotation = localRotation;
        }

        if (oldHexTile != null)
            HexMapEditorPlacementUtility.UpdateOpeningHexReferences(oldHexTile, hexTile);

        if (_openingTargetHex == existingHex)
            _openingTargetHex = instance;

        HexMapEditorLockedHexVisual.RefreshHex(instance, _groundStartsLocked && ShouldShowLockedHexVisual());

        Undo.RegisterCreatedObjectUndo(instance, "Paint Hex Ground");
    }

    private void PaintFreeObject(
        IReadOnlyList<GameObject> prefabs,
        int selectedIndex,
        HexMapPaintMode mode)
    {
        if (prefabs == null || prefabs.Count == 0 || selectedIndex >= prefabs.Count)
            return;

        var prefab = prefabs[selectedIndex];
        if (prefab == null)
            return;

        var hexParent = HexMapEditorPlacementUtility.GetHexParent(_mapRoot, _placementInfo.Hex);
        if (hexParent == null)
        {
            Debug.LogWarning($"Нет грунтового гекса в ({_placementInfo.Hex.x}, {_placementInfo.Hex.y}). Сначала поставьте гекс.");
            return;
        }

        float rotationY = GetPlacementRotation();
        var worldPosition = HexMapEditorPlacementUtility.GetPlacementPosition(
            prefab,
            _placementInfo.WorldPosition,
            rotationY,
            hexParent,
            GetPlacementSurfaceLift(mode));
        var worldRotation = HexMapEditorPlacementUtility.GetPlacementRotation(rotationY);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hexParent);
        if (instance == null)
            return;

        instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        HexMapEditorPlacementUtility.ApplyCompensatedLocalScale(
            instance.transform,
            hexParent,
            GetPlacementScaleFactor(mode));
        instance.name = $"{prefab.name} ({_placementInfo.Hex.x},{_placementInfo.Hex.y})";

        var placed = instance.GetComponent<HexMapPlacedObject>();
        if (placed == null)
            placed = instance.AddComponent<HexMapPlacedObject>();
        placed.Initialize(mode, _placementInfo.Hex.x, _placementInfo.Hex.y);

        var resourceHolder = instance.GetComponentInChildren<ResourseHolderView>(true);
        if (resourceHolder != null && resourceHolder.AssignSaveIdIfEmpty())
            EditorUtility.SetDirty(resourceHolder);

        Undo.RegisterCreatedObjectUndo(instance, $"Paint {mode}");
    }

    private void PaintRewardedResourceSpot()
    {
        var prefabs = _palette.RewardedResourceSpotPrefabs;
        if (prefabs == null || prefabs.Count == 0 || _selectedRewardedResourceIndex >= prefabs.Count)
            return;

        var prefab = prefabs[_selectedRewardedResourceIndex];
        if (prefab == null)
            return;

        var hexParent = HexMapEditorPlacementUtility.GetHexParent(_mapRoot, _placementInfo.Hex);
        if (hexParent == null)
        {
            Debug.LogWarning($"Нет грунтового гекса в ({_placementInfo.Hex.x}, {_placementInfo.Hex.y}). Сначала поставьте гекс.");
            return;
        }

        float rotationY = GetPlacementRotation();
        var worldPosition = HexMapEditorPlacementUtility.GetPlacementPosition(
            prefab,
            _placementInfo.WorldPosition,
            rotationY,
            hexParent);
        var worldRotation = HexMapEditorPlacementUtility.GetPlacementRotation(rotationY);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hexParent);
        if (instance == null)
            return;

        instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        HexMapEditorPlacementUtility.ApplyCompensatedLocalScale(instance.transform, hexParent);
        instance.name = $"{prefab.name} ({_placementInfo.Hex.x},{_placementInfo.Hex.y})";

        var placed = instance.GetComponent<HexMapPlacedObject>();
        if (placed == null)
            placed = instance.AddComponent<HexMapPlacedObject>();
        placed.Initialize(HexMapPaintMode.RewardedResourceSpot, _placementInfo.Hex.x, _placementInfo.Hex.y);

        var spot = instance.GetComponentInChildren<RewardedResourceSpot>(true);
        if (spot != null)
        {
            var serializedObject = new SerializedObject(spot);
            serializedObject.FindProperty("_resourceAmount").intValue = Mathf.Max(1, _rewardedResourceAmount);
            serializedObject.FindProperty("_oneShot").boolValue = _rewardedResourceOneShot;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.RegisterCreatedObjectUndo(instance, "Paint Rewarded Resource Spot");
    }

    private void DrawEnemySpawnPointFields()
    {
        bool isOneTime = _paintMode == HexMapPaintMode.OneTimeEnemySpawnPoint;

        _selectedEnemySpawnPointIndex = DrawPrefabPopup(
            "EnemySpawnPoint",
            _palette.EnemySpawnPointPrefabs,
            _selectedEnemySpawnPointIndex);
        _enemySpawnConfig = (EnemyInitConfig)EditorGUILayout.ObjectField(
            "Конфиг врага", _enemySpawnConfig, typeof(EnemyInitConfig), false);

        if (!isOneTime)
        {
            _enemySpawnCooldown = EditorGUILayout.FloatField("Кулдаун спавна", _enemySpawnCooldown);
            _enemySpawnCooldown = Mathf.Max(0f, _enemySpawnCooldown);
        }

        _enemyGuardRadius = EditorGUILayout.FloatField("Радиус охраны", _enemyGuardRadius);
        _enemyGuardRadius = Mathf.Max(0f, _enemyGuardRadius);
        _enemySpawnTriggerOffset = EditorGUILayout.FloatField("Оффсет спавна", _enemySpawnTriggerOffset);
        _enemySpawnTriggerOffset = Mathf.Max(0f, _enemySpawnTriggerOffset);
        _placementRotation = EditorGUILayout.Slider("Поворот Y", _placementRotation, 0f, 360f);
        if (_enemySpawnConfig == null)
            EditorGUILayout.HelpBox("Назначьте EnemyInitConfig, чтобы точка спавна создавала врага.", MessageType.Warning);
    }

    private void PaintEnemySpawnPoint()
    {
        var prefabs = _palette.EnemySpawnPointPrefabs;
        if (prefabs == null || prefabs.Count == 0 || _selectedEnemySpawnPointIndex >= prefabs.Count)
            return;

        var prefab = prefabs[_selectedEnemySpawnPointIndex];
        if (prefab == null)
            return;

        var hexParent = HexMapEditorPlacementUtility.GetHexParent(_mapRoot, _placementInfo.Hex);
        if (hexParent == null)
        {
            Debug.LogWarning($"Нет грунтового гекса в ({_placementInfo.Hex.x}, {_placementInfo.Hex.y}). Сначала поставьте гекс.");
            return;
        }

        float rotationY = GetPlacementRotation();
        var worldPosition = HexMapEditorPlacementUtility.GetPlacementPosition(
            prefab,
            _placementInfo.WorldPosition,
            rotationY,
            hexParent);
        var worldRotation = HexMapEditorPlacementUtility.GetPlacementRotation(rotationY);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hexParent);
        if (instance == null)
            return;

        instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        HexMapEditorPlacementUtility.ApplyCompensatedLocalScale(instance.transform, hexParent);
        instance.name = $"{prefab.name} ({_placementInfo.Hex.x},{_placementInfo.Hex.y})";

        bool isOneTime = _paintMode == HexMapPaintMode.OneTimeEnemySpawnPoint;
        HexMapPaintMode paintMode = isOneTime
            ? HexMapPaintMode.OneTimeEnemySpawnPoint
            : HexMapPaintMode.EnemySpawnPoint;

        var placed = instance.GetComponent<HexMapPlacedObject>();
        if (placed == null)
            placed = instance.AddComponent<HexMapPlacedObject>();
        placed.Initialize(paintMode, _placementInfo.Hex.x, _placementInfo.Hex.y);

        var spawnPoint = instance.GetComponentInChildren<EnemySpawnPoint>(true);
        if (spawnPoint != null)
        {
            var serializedObject = new SerializedObject(spawnPoint);
            serializedObject.FindProperty("_enemyConfig").objectReferenceValue = _enemySpawnConfig;
            serializedObject.FindProperty("_respawnTime").floatValue = isOneTime ? 0f : Mathf.Max(0f, _enemySpawnCooldown);
            serializedObject.FindProperty("_enemyGuardRadius").floatValue = Mathf.Max(0f, _enemyGuardRadius);
            serializedObject.FindProperty("_spawnTriggerOffset").floatValue = Mathf.Max(0f, _enemySpawnTriggerOffset);
            serializedObject.FindProperty("_isOneTime").boolValue = isOneTime;
            if (isOneTime)
                serializedObject.FindProperty("_saveId").stringValue = System.Guid.NewGuid().ToString("N");
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.RegisterCreatedObjectUndo(instance, isOneTime ? "Paint One-Time Enemy Spawn Point" : "Paint Enemy Spawn Point");
    }

    private void PaintOpeningHex()
    {
        var prefab = _palette.OpeningHexPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("OpeningHex prefab не назначен в палитре.");
            return;
        }

        if (!_hasOpeningTarget || _openingTargetHex == null)
        {
            Debug.LogWarning("Сначала выберите заблокированный гекс.");
            return;
        }

        if (!HexMapEditorOpeningHexPriceUtility.TryBuildPriceDictionary(_openingHexPrices, out var price, out var error))
        {
            Debug.LogWarning(error);
            return;
        }

        float rotationY = _placementRotation;
        var hexParent = HexMapEditorPlacementUtility.GetHexParent(_mapRoot, _placementInfo.Hex);
        if (hexParent == null)
        {
            Debug.LogWarning($"Нет грунтового гекса в ({_placementInfo.Hex.x}, {_placementInfo.Hex.y}). Сначала поставьте гекс.");
            return;
        }

        var worldPosition = HexMapEditorPlacementUtility.GetPlacementPosition(
            prefab,
            _placementInfo.WorldPosition,
            rotationY,
            hexParent);
        var worldRotation = HexMapEditorPlacementUtility.GetPlacementRotation(rotationY);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hexParent);
        if (instance == null)
            return;

        instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        HexMapEditorPlacementUtility.ApplyCompensatedLocalScale(instance.transform, hexParent);
        instance.name = $"{prefab.name} ({_placementInfo.Hex.x},{_placementInfo.Hex.y})";

        var placed = instance.GetComponent<HexMapPlacedObject>();
        if (placed == null)
            placed = instance.AddComponent<HexMapPlacedObject>();
        placed.Initialize(HexMapPaintMode.OpeningHex, _placementInfo.Hex.x, _placementInfo.Hex.y);

        var opening = instance.GetComponent<OpeningHexagon>();
        if (opening != null)
        {
            var targetTile = _openingTargetHex != null ? _openingTargetHex.GetComponent<HexTile>() : null;
            var serializedObject = new SerializedObject(opening);
            serializedObject.FindProperty("_openedHexagon").objectReferenceValue = targetTile;
            HexMapEditorOpeningHexPriceUtility.ApplyPrice(serializedObject, price);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.RegisterCreatedObjectUndo(instance, "Paint Opening Hex");
        ClearOpeningTarget();
    }

    private void EraseAtCursor(Vector2 mousePosition)
    {
        if (_paintMode == HexMapPaintMode.Erase)
        {
            var picked = HexMapEditorPlacementUtility.PickPlacedObject(mousePosition, _mapRoot, null);
            if (picked != null)
            {
                Undo.DestroyObjectImmediate(picked);
                return;
            }

            if (_hasHoverHex)
                EraseGroundHex(_hoverHex);
            return;
        }

        if (_paintMode == HexMapPaintMode.Ground)
        {
            if (_hasHoverHex)
                EraseGroundHex(_hoverHex);
            return;
        }

        var filtered = HexMapEditorPlacementUtility.PickPlacedObject(mousePosition, _mapRoot, _paintMode);
        if (filtered != null)
            Undo.DestroyObjectImmediate(filtered);
    }

    private void EraseGroundHex(Vector2Int hex)
    {
        var groundHex = HexMapEditorPlacementUtility.FindGroundHex(_mapRoot, hex);
        if (groundHex != null)
        {
            Undo.DestroyObjectImmediate(groundHex);
            return;
        }

        EraseLegacyPlacedObjectsAtHex(hex);
    }

    private void EraseLegacyPlacedObjectsAtHex(Vector2Int hex)
    {
        EraseFromContainer(_mapRoot.Ground, hex, null);
        EraseFromContainer(_mapRoot.Environment, hex, null);
        EraseFromContainer(_mapRoot.Resources, hex, null);
        EraseFromContainer(_mapRoot.Gameplay, hex, null);
    }

    private static void EraseFromContainer(Transform container, Vector2Int hex, HexMapPaintMode? modeFilter)
    {
        if (container == null)
            return;

        var toDestroy = new HashSet<GameObject>();

        foreach (var placed in container.GetComponentsInChildren<HexMapPlacedObject>(true))
        {
            if (placed.Q != hex.x || placed.R != hex.y)
                continue;

            if (modeFilter.HasValue && placed.PaintMode != modeFilter.Value)
                continue;

            toDestroy.Add(placed.gameObject);
        }

        foreach (var hexTile in container.GetComponentsInChildren<HexTile>(true))
        {
            if (hexTile.Q != hex.x || hexTile.R != hex.y)
                continue;

            if (modeFilter.HasValue && modeFilter.Value != HexMapPaintMode.Ground)
                continue;

            toDestroy.Add(hexTile.gameObject);
        }

        foreach (var go in toDestroy)
            Undo.DestroyObjectImmediate(go);
    }

    private static float GetPlacementSurfaceLift(HexMapPaintMode mode)
    {
        return mode == HexMapPaintMode.Environment
            ? HexMapEditorPlacementUtility.EnvironmentPlacementLift
            : 0f;
    }

    private float GetPreviewRotation()
    {
        return _placementRotation;
    }

    private float GetPlacementRotation()
    {
        if (_paintMode == HexMapPaintMode.Environment && _randomizeEnvironmentRotation)
            return Random.Range(0f, 360f);

        return _placementRotation;
    }

    private float GetPlacementScaleFactor(HexMapPaintMode mode)
    {
        return mode switch
        {
            HexMapPaintMode.Environment when _randomizeEnvironmentScale =>
                Random.Range(_environmentScaleMin, _environmentScaleMax),
            HexMapPaintMode.ResourceHolder when _randomizeResourceScale =>
                Random.Range(_resourceScaleMin, _resourceScaleMax),
            _ => 1f
        };
    }

    private GameObject GetCurrentPrefab()
    {
        return _paintMode switch
        {
            HexMapPaintMode.Environment => GetPrefabAt(_palette.EnvironmentPrefabs, _selectedEnvironmentIndex),
            HexMapPaintMode.ResourceHolder => GetPrefabAt(_palette.ResourceHolderPrefabs, _selectedResourceIndex),
            HexMapPaintMode.RewardedResourceSpot => GetPrefabAt(_palette.RewardedResourceSpotPrefabs, _selectedRewardedResourceIndex),
            HexMapPaintMode.InstrumentUpgrader => GetPrefabAt(_palette.InstrumentUpgraderPrefabs, _selectedUpgraderIndex),
            HexMapPaintMode.EnemySpawnPoint => GetPrefabAt(_palette.EnemySpawnPointPrefabs, _selectedEnemySpawnPointIndex),
            HexMapPaintMode.OneTimeEnemySpawnPoint => GetPrefabAt(_palette.EnemySpawnPointPrefabs, _selectedEnemySpawnPointIndex),
            HexMapPaintMode.OpeningHex => _palette.OpeningHexPrefab,
            _ => null
        };
    }

    private static GameObject GetPrefabAt(IReadOnlyList<GameObject> prefabs, int index)
    {
        if (prefabs == null || prefabs.Count == 0 || index >= prefabs.Count)
            return null;

        return prefabs[index];
    }

    private static bool RequiresExistingHex(HexMapPaintMode mode)
    {
        return mode is HexMapPaintMode.Environment
            or HexMapPaintMode.ResourceHolder
            or HexMapPaintMode.RewardedResourceSpot
            or HexMapPaintMode.InstrumentUpgrader
            or HexMapPaintMode.EnemySpawnPoint
            or HexMapPaintMode.OneTimeEnemySpawnPoint
            or HexMapPaintMode.OpeningHex
            or HexMapPaintMode.LockHex;
    }

    private void CreateMapRoot()
    {
        var existing = Object.FindFirstObjectByType<HexMapRoot>();
        if (existing != null)
        {
            _mapRoot = existing;
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("HexMap");
        Undo.RegisterCreatedObjectUndo(go, "Create Hex Map Root");
        _mapRoot = go.AddComponent<HexMapRoot>();
        _mapRoot.EnsureContainers();
        Selection.activeGameObject = go;
    }

    private void CreatePaletteAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Hex Map Palette",
            "HexMapPalette",
            "asset",
            "Выберите путь для палитры");

        if (string.IsNullOrEmpty(path))
            return;

        var palette = CreateInstance<HexMapPalette>();
        AssetDatabase.CreateAsset(palette, path);
        AssetDatabase.SaveAssets();

        _palette = palette;
        TryPopulateDefaultPalette(_palette);
        EditorUtility.SetDirty(_palette);
        AssetDatabase.SaveAssets();

        Selection.activeObject = palette;
    }

    private static void TryPopulateDefaultPalette(HexMapPalette palette)
    {
        var so = new SerializedObject(palette);

        AddPrefabsFromFolder(so.FindProperty("_groundPrefabs"), "Assets/3D Game My Home Planet/Prefabs/Ground");
        AddPrefabsFromFolder(so.FindProperty("_environmentPrefabs"),
            "Assets/3D Game My Home Planet/Prefabs",
            "Assets/3D Game My Little World_2/Prefabs");
        AddPrefabsFromFolder(so.FindProperty("_resourceHolderPrefabs"), "Assets/Prefabs/Objects/ResoursHolders");
        AddPrefabsFromFolder(so.FindProperty("_rewardedResourceSpotPrefabs"), "Assets/Prefabs/Objects/ResourceRewardObjects");
        AddPrefabsFromFolder(so.FindProperty("_instrumentUpgraderPrefabs"), "Assets/Prefabs/Objects");

        var openingHex = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/OpeningHex.prefab");
        if (openingHex != null)
            so.FindProperty("_openingHexPrefab").objectReferenceValue = openingHex;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddPrefabsFromFolder(SerializedProperty listProperty, params string[] folders)
    {
        listProperty.ClearArray();

        var added = new HashSet<string>();
        int index = 0;

        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!added.Add(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                if (folder.Contains("ResoursHolders") && prefab.GetComponentInChildren<ResourseHolderView>(true) == null)
                    continue;
                if (folder.Contains("ResourceRewardObjects") &&
                    prefab.GetComponentInChildren<RewardedResourceSpot>(true) == null)
                    continue;
                if (folder.EndsWith("Objects") &&
                    prefab.GetComponentInChildren<InstrumentUpgrader>(true) == null &&
                    prefab.GetComponentInChildren<EquipmentUpgrader>(true) == null)
                    continue;
                if (folder.Contains("Ground") && !prefab.name.StartsWith("Grade"))
                    continue;
                if (folder.Contains("My Home Planet/Prefabs") && !folder.Contains("Ground"))
                {
                    if (!prefab.name.StartsWith("Grass") && prefab.name != "flowers" && prefab.name != "fence")
                        continue;
                }

                listProperty.InsertArrayElementAtIndex(index);
                listProperty.GetArrayElementAtIndex(index).objectReferenceValue = prefab;
                index++;
            }
        }
    }
}
