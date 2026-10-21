using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class GoalSequenceEditorWindow : EditorWindow
{
    private const string MenuPath = "Tools/Goals Sequence Editor";
    private const string DefaultConfigPath = "Assets/Configs/Goal/GoalSequenceConfig.asset";
    private const string DefaultGoalsFolder = "Assets/Configs/Goal";

    private static readonly string[] GoalTypeLabels =
    {
        "Collect Resource",
        "Upgrade Instrument",
        "Explore Island",
        "Fill Opening Hexagon",
        "Rescue NPC",
        "Upgrade Equipment"
    };

    private GoalSequenceConfigSO _sequenceConfig;
    private SerializedObject _sequenceSerialized;
    private ReorderableList _goalsList;
    private SerializedObject _selectedGoalSerialized;
    private GoalDefinitionSO _selectedGoal;
    private Vector2 _sequenceScroll;
    private Vector2 _inspectorScroll;
    private string _newGoalName = "New Goal";
    private string _goalsFolder = DefaultGoalsFolder;
    private int _createTypeIndex;
    private string _renameBuffer = string.Empty;
    private bool _autoAddToSequence = true;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var window = GetWindow<GoalSequenceEditorWindow>("Goals Sequence");
        window.minSize = new Vector2(720f, 480f);
        window.Show();
    }

    public static void Open(GoalSequenceConfigSO config)
    {
        Open();
        var window = GetWindow<GoalSequenceEditorWindow>();
        window.SetSequenceConfig(config);
    }

    private void OnEnable()
    {
        if (_sequenceConfig == null)
            _sequenceConfig = AssetDatabase.LoadAssetAtPath<GoalSequenceConfigSO>(DefaultConfigPath);

        RebuildSequenceSerialized();
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeObject is GoalDefinitionSO goal)
            SelectGoal(goal);
        else if (Selection.activeObject is GoalSequenceConfigSO config)
            SetSequenceConfig(config);
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSequencePane();
            DrawDetailsPane();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            var nextConfig = (GoalSequenceConfigSO)EditorGUILayout.ObjectField(
                _sequenceConfig,
                typeof(GoalSequenceConfigSO),
                false,
                GUILayout.MinWidth(220f));

            if (EditorGUI.EndChangeCheck())
                SetSequenceConfig(nextConfig);

            if (GUILayout.Button("Find Config", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                FindOrPickConfig();

            if (GUILayout.Button("Create Config", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                CreateConfigAsset();

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_sequenceConfig == null))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    SaveAll();
            }
        }
    }

    private void DrawSequencePane()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.48f)))
        {
            EditorGUILayout.LabelField("Sequence Order", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "List order is the play order. Use Sync Names With Order if asset names should match indices.",
                MessageType.None);

            if (_sequenceConfig == null || _goalsList == null)
            {
                EditorGUILayout.HelpBox("Select or create a Goal Sequence Config.", MessageType.Info);
                return;
            }

            _sequenceSerialized.Update();
            EditorGUILayout.PropertyField(_sequenceSerialized.FindProperty("_worldId"));
            EditorGUILayout.PropertyField(_sequenceSerialized.FindProperty("_nextSequence"));
            _sequenceSerialized.ApplyModifiedProperties();

            _sequenceScroll = EditorGUILayout.BeginScrollView(_sequenceScroll, GUILayout.ExpandHeight(true));
            _sequenceSerialized.Update();
            _goalsList.DoLayoutList();
            _sequenceSerialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Existing Goal..."))
                    AddExistingGoal();

                if (GUILayout.Button("Sync Names With Order"))
                    SyncAssetNamesWithOrder();
            }
        }
    }

    private void DrawDetailsPane()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            DrawCreateSection();
            EditorGUILayout.Space(8f);
            DrawSelectedGoalSection();
        }
    }

    private void DrawCreateSection()
    {
        EditorGUILayout.LabelField("Create Goal", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            _createTypeIndex = EditorGUILayout.Popup("Type", _createTypeIndex, GoalTypeLabels);
            _newGoalName = EditorGUILayout.TextField("Asset Name", _newGoalName);
            _goalsFolder = EditorGUILayout.TextField("Folder", _goalsFolder);
            _autoAddToSequence = EditorGUILayout.Toggle("Add To Sequence", _autoAddToSequence);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Suggest Name From Order"))
                    SuggestNameFromOrder();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newGoalName)))
                {
                    if (GUILayout.Button("Create Goal", GUILayout.Height(28f)))
                        CreateGoalAsset();
                }
            }
        }
    }

    private void DrawSelectedGoalSection()
    {
        EditorGUILayout.LabelField("Selected Goal", EditorStyles.boldLabel);

        if (_selectedGoal == null)
        {
            EditorGUILayout.HelpBox("Select a goal in the list.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.ObjectField("Asset", _selectedGoal, typeof(GoalDefinitionSO), false);
            EditorGUILayout.LabelField("Type", _selectedGoal.GetType().Name);

            using (new EditorGUILayout.HorizontalScope())
            {
                _renameBuffer = EditorGUILayout.TextField("Rename", _renameBuffer);
                if (GUILayout.Button("Apply", GUILayout.Width(60f)))
                    RenameSelectedGoal(_renameBuffer);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping"))
                    EditorGUIUtility.PingObject(_selectedGoal);

                if (GUILayout.Button("Select In Project"))
                    Selection.activeObject = _selectedGoal;

                if (GUILayout.Button("Duplicate"))
                    DuplicateSelectedGoal();

                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                if (GUILayout.Button("Delete Asset"))
                    DeleteSelectedGoalAsset();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(6f);
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

            if (_selectedGoalSerialized == null)
                _selectedGoalSerialized = new SerializedObject(_selectedGoal);

            _selectedGoalSerialized.Update();
            DrawGoalProperties(_selectedGoalSerialized);
            _selectedGoalSerialized.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }
    }

    private static void DrawGoalProperties(SerializedObject serializedObject)
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "m_Script")
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    private void SetSequenceConfig(GoalSequenceConfigSO config)
    {
        _sequenceConfig = config;
        RebuildSequenceSerialized();
        Repaint();
    }

    private void RebuildSequenceSerialized()
    {
        _goalsList = null;
        _sequenceSerialized = null;

        if (_sequenceConfig == null)
            return;

        _sequenceSerialized = new SerializedObject(_sequenceConfig);
        SerializedProperty goalsProperty = _sequenceSerialized.FindProperty("_goals");
        _goalsList = new ReorderableList(_sequenceSerialized, goalsProperty, true, true, true, true)
        {
            drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, $"Goals ({goalsProperty.arraySize})");
            },
            drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty element = goalsProperty.GetArrayElementAtIndex(index);
                var goal = element.objectReferenceValue as GoalDefinitionSO;

                const float indexWidth = 32f;
                const float typeWidth = 72f;
                float y = rect.y + 2f;
                float height = EditorGUIUtility.singleLineHeight;

                var indexRect = new Rect(rect.x, y, indexWidth, height);
                var typeRect = new Rect(rect.x + indexWidth, y, typeWidth, height);
                var fieldRect = new Rect(rect.x + indexWidth + typeWidth, y, rect.width - indexWidth - typeWidth, height);

                EditorGUI.LabelField(indexRect, $"{index + 1}.", EditorStyles.miniBoldLabel);
                EditorGUI.LabelField(typeRect, goal != null ? GetShortTypeLabel(goal) : "—", EditorStyles.miniLabel);

                EditorGUI.BeginChangeCheck();
                EditorGUI.ObjectField(fieldRect, element, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                    _sequenceSerialized.ApplyModifiedProperties();
            },
            onSelectCallback = list =>
            {
                if (list.index < 0 || list.index >= goalsProperty.arraySize)
                    return;

                var goal = goalsProperty.GetArrayElementAtIndex(list.index).objectReferenceValue as GoalDefinitionSO;
                SelectGoal(goal);
            },
            onAddCallback = list =>
            {
                AddExistingGoal();
            },
            onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= goalsProperty.arraySize)
                    return;

                SerializedProperty element = goalsProperty.GetArrayElementAtIndex(list.index);
                if (element.objectReferenceValue != null)
                    goalsProperty.DeleteArrayElementAtIndex(list.index);
                goalsProperty.DeleteArrayElementAtIndex(list.index);
                _sequenceSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_sequenceConfig);
            },
            onReorderCallback = _ =>
            {
                _sequenceSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_sequenceConfig);
            },
            elementHeight = EditorGUIUtility.singleLineHeight + 4f
        };

        if (_goalsList.count > 0)
        {
            _goalsList.index = Mathf.Clamp(_goalsList.index, 0, _goalsList.count - 1);
            var first = goalsProperty.GetArrayElementAtIndex(_goalsList.index).objectReferenceValue as GoalDefinitionSO;
            if (_selectedGoal == null && first != null)
                SelectGoal(first);
        }
    }

    private void SelectGoal(GoalDefinitionSO goal)
    {
        _selectedGoal = goal;
        _selectedGoalSerialized = goal != null ? new SerializedObject(goal) : null;
        _renameBuffer = goal != null ? goal.name : string.Empty;
        Repaint();
    }

    private void FindOrPickConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:GoalSequenceConfigSO");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Goals", "GoalSequenceConfigSO not found. Create one first.", "OK");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        SetSequenceConfig(AssetDatabase.LoadAssetAtPath<GoalSequenceConfigSO>(path));
        EditorGUIUtility.PingObject(_sequenceConfig);
    }

    private void CreateConfigAsset()
    {
        EnsureFolderExists(DefaultGoalsFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultGoalsFolder}/GoalSequenceConfig.asset");
        var config = CreateInstance<GoalSequenceConfigSO>();
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        SetSequenceConfig(config);
        Selection.activeObject = config;
    }

    private void SuggestNameFromOrder()
    {
        int nextIndex = GetGoalsCount() + 1;
        string typeLabel = GoalTypeLabels[_createTypeIndex];
        _newGoalName = $"{nextIndex} {typeLabel}";
    }

    private void CreateGoalAsset()
    {
        if (_sequenceConfig == null && _autoAddToSequence)
        {
            EditorUtility.DisplayDialog("Goals", "Select a Goal Sequence Config first.", "OK");
            return;
        }

        EnsureFolderExists(_goalsFolder);

        string safeName = SanitizeFileName(_newGoalName.Trim());
        if (string.IsNullOrEmpty(safeName))
        {
            EditorUtility.DisplayDialog("Goals", "Asset name is empty.", "OK");
            return;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{_goalsFolder}/{safeName}.asset");
        GoalDefinitionSO goal = CreateGoalInstance(_createTypeIndex);
        AssetDatabase.CreateAsset(goal, path);
        Undo.RegisterCreatedObjectUndo(goal, "Create Goal");

        if (_autoAddToSequence && _sequenceConfig != null)
            AppendGoalToSequence(goal);

        AssetDatabase.SaveAssets();
        SelectGoal(goal);
        Selection.activeObject = goal;
        EditorGUIUtility.PingObject(goal);
        SuggestNameFromOrder();
    }

    private static GoalDefinitionSO CreateGoalInstance(int typeIndex)
    {
        return typeIndex switch
        {
            0 => CreateInstance<CollectResourceGoalSO>(),
            1 => CreateInstance<UpgradeInstrumentGoalSO>(),
            2 => CreateInstance<ExploreIslandGoalSO>(),
            3 => CreateInstance<FillOpeningHexagonGoalSO>(),
            4 => CreateInstance<RescueNpcGoalSO>(),
            5 => CreateInstance<UpgradeEquipmentGoalSO>(),
            _ => CreateInstance<CollectResourceGoalSO>()
        };
    }

    private void AddExistingGoal()
    {
        if (_sequenceConfig == null)
        {
            EditorUtility.DisplayDialog("Goals", "Select a Goal Sequence Config first.", "OK");
            return;
        }

        string absolutePath = EditorUtility.OpenFilePanel("Select Goal Asset", DefaultGoalsFolder, "asset");
        if (string.IsNullOrEmpty(absolutePath))
            return;

        string projectPath = AbsoluteToProjectPath(absolutePath);
        if (string.IsNullOrEmpty(projectPath))
        {
            EditorUtility.DisplayDialog("Goals", "File must be inside the project.", "OK");
            return;
        }

        var goal = AssetDatabase.LoadAssetAtPath<GoalDefinitionSO>(projectPath);
        if (goal == null)
        {
            EditorUtility.DisplayDialog("Goals", "Selected asset is not a GoalDefinitionSO.", "OK");
            return;
        }

        AppendGoalToSequence(goal);
        SelectGoal(goal);
    }

    private void AppendGoalToSequence(GoalDefinitionSO goal)
    {
        _sequenceSerialized.Update();
        SerializedProperty goalsProperty = _sequenceSerialized.FindProperty("_goals");

        for (int i = 0; i < goalsProperty.arraySize; i++)
        {
            if (goalsProperty.GetArrayElementAtIndex(i).objectReferenceValue == goal)
            {
                _goalsList.index = i;
                return;
            }
        }

        int index = goalsProperty.arraySize;
        goalsProperty.InsertArrayElementAtIndex(index);
        goalsProperty.GetArrayElementAtIndex(index).objectReferenceValue = goal;
        _sequenceSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_sequenceConfig);
        _goalsList.index = index;
    }

    private void SyncAssetNamesWithOrder()
    {
        if (_sequenceConfig == null)
            return;

        if (!EditorUtility.DisplayDialog(
                "Sync Names",
                "Rename goal assets to match sequence order?\nFormat: \"N Original Name\"",
                "Rename",
                "Cancel"))
            return;

        _sequenceSerialized.Update();
        SerializedProperty goalsProperty = _sequenceSerialized.FindProperty("_goals");
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < goalsProperty.arraySize; i++)
        {
            var goal = goalsProperty.GetArrayElementAtIndex(i).objectReferenceValue as GoalDefinitionSO;
            if (goal == null)
                continue;

            string baseName = StripLeadingOrderPrefix(goal.name);
            string desired = SanitizeFileName($"{i + 1} {baseName}");
            desired = MakeUniqueName(desired, usedNames);
            usedNames.Add(desired);

            if (goal.name == desired)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(goal);
            string error = AssetDatabase.RenameAsset(assetPath, desired);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"Failed to rename '{goal.name}': {error}");
        }

        if (_selectedGoal != null)
            _renameBuffer = _selectedGoal.name;

        AssetDatabase.SaveAssets();
        Repaint();
    }

    private void RenameSelectedGoal(string newName)
    {
        if (_selectedGoal == null || string.IsNullOrWhiteSpace(newName))
            return;

        string desired = SanitizeFileName(newName.Trim());
        if (desired == _selectedGoal.name)
            return;

        string assetPath = AssetDatabase.GetAssetPath(_selectedGoal);
        string error = AssetDatabase.RenameAsset(assetPath, desired);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Rename Failed", error, "OK");
            return;
        }

        _renameBuffer = _selectedGoal.name;
        AssetDatabase.SaveAssets();
    }

    private void DuplicateSelectedGoal()
    {
        if (_selectedGoal == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(_selectedGoal);
        string folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? DefaultGoalsFolder;
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{_selectedGoal.name}.asset");

        if (!AssetDatabase.CopyAsset(sourcePath, uniquePath))
        {
            EditorUtility.DisplayDialog("Goals", "Failed to copy asset.", "OK");
            return;
        }

        var copy = AssetDatabase.LoadAssetAtPath<GoalDefinitionSO>(uniquePath);
        if (copy != null && _autoAddToSequence && _sequenceConfig != null)
            AppendGoalToSequence(copy);

        SelectGoal(copy);
        Selection.activeObject = copy;
        AssetDatabase.SaveAssets();
    }

    private void DeleteSelectedGoalAsset()
    {
        if (_selectedGoal == null)
            return;

        string path = AssetDatabase.GetAssetPath(_selectedGoal);
        if (!EditorUtility.DisplayDialog(
                "Delete Goal",
                $"Delete asset?\n{path}\n\nIt will also be removed from the sequence.",
                "Delete",
                "Cancel"))
            return;

        RemoveGoalFromSequence(_selectedGoal);
        AssetDatabase.DeleteAsset(path);
        SelectGoal(null);
        AssetDatabase.SaveAssets();
    }

    private void RemoveGoalFromSequence(GoalDefinitionSO goal)
    {
        if (_sequenceConfig == null || goal == null)
            return;

        _sequenceSerialized.Update();
        SerializedProperty goalsProperty = _sequenceSerialized.FindProperty("_goals");

        for (int i = goalsProperty.arraySize - 1; i >= 0; i--)
        {
            if (goalsProperty.GetArrayElementAtIndex(i).objectReferenceValue != goal)
                continue;

            if (goalsProperty.GetArrayElementAtIndex(i).objectReferenceValue != null)
                goalsProperty.DeleteArrayElementAtIndex(i);
            goalsProperty.DeleteArrayElementAtIndex(i);
        }

        _sequenceSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_sequenceConfig);
    }

    private int GetGoalsCount()
    {
        if (_sequenceConfig == null || _sequenceSerialized == null)
            return 0;

        _sequenceSerialized.Update();
        return _sequenceSerialized.FindProperty("_goals").arraySize;
    }

    private void SaveAll()
    {
        if (_sequenceSerialized != null)
            _sequenceSerialized.ApplyModifiedProperties();

        if (_selectedGoalSerialized != null)
            _selectedGoalSerialized.ApplyModifiedProperties();

        if (_sequenceConfig != null)
            EditorUtility.SetDirty(_sequenceConfig);

        if (_selectedGoal != null)
            EditorUtility.SetDirty(_selectedGoal);

        AssetDatabase.SaveAssets();
    }

    private static string GetShortTypeLabel(GoalDefinitionSO goal)
    {
        return goal switch
        {
            CollectResourceGoalSO => "Collect",
            UpgradeInstrumentGoalSO => "Upgrade",
            ExploreIslandGoalSO => "Explore",
            FillOpeningHexagonGoalSO => "Fill Hex",
            RescueNpcGoalSO => "Rescue",
            UpgradeEquipmentGoalSO => "Equip",
            _ => goal.GetType().Name.Replace("GoalSO", string.Empty)
        };
    }

    private static string StripLeadingOrderPrefix(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Goal";

        string stripped = Regex.Replace(name, @"^\d+(\.\d+)?\s+", string.Empty).Trim();
        return string.IsNullOrEmpty(stripped) ? name : stripped;
    }

    private static string MakeUniqueName(string desired, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(desired))
            return desired;

        int suffix = 2;
        while (usedNames.Contains($"{desired} {suffix}"))
            suffix++;

        return $"{desired} {suffix}";
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return name.Trim();
    }

    private static void EnsureFolderExists(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string AbsoluteToProjectPath(string absolutePath)
    {
        absolutePath = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');

        if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return null;

        return "Assets" + absolutePath.Substring(dataPath.Length);
    }
}
