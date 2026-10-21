#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AchievementAssetsGenerator
{
    private const string Folder = "Assets/Configs/Achievements";
    private const string CatalogPath = Folder + "/AchievementCatalog.asset";
    private const string AllResourcesPath = "Assets/Configs/ResoursesConf/AllResoursesConfig.asset";

    private static readonly int[] ResourceThresholds = { 10000, 50000, 100000 };
    private static readonly int[] WoodRockThresholds = { 10000, 50000, 100000 };
    private static readonly int[] KillThresholds = { 100, 500, 1000 };

    [MenuItem("Tools/Achievements/Generate Achievement Assets")]
    public static void Generate()
    {
        EnsureFolder(Folder);

        AllResoursesConfig allResources = AssetDatabase.LoadAssetAtPath<AllResoursesConfig>(AllResourcesPath);
        Dictionary<ResourceType, Sprite> icons = BuildIconMap(allResources);

        AchievementCatalogSO catalog = AssetDatabase.LoadAssetAtPath<AchievementCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AchievementCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject catalogSo = new SerializedObject(catalog);
        SerializedProperty listProp = catalogSo.FindProperty("_achievements");

        var existingIds = new HashSet<string>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var existing = listProp.GetArrayElementAtIndex(i).objectReferenceValue as AchievementDefinitionSO;
            if (existing != null && !string.IsNullOrEmpty(existing.Id))
                existingIds.Add(existing.Id);
        }

        foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
        {
            int[] thresholds = resourceType is ResourceType.Wood or ResourceType.Rock
                ? WoodRockThresholds
                : ResourceThresholds;

            foreach (int threshold in thresholds)
            {
                string id = $"ach_lifetime_{resourceType.ToString().ToLowerInvariant()}_{threshold}";
                if (existingIds.Contains(id))
                    continue;

                string assetPath = $"{Folder}/Lifetime {resourceType} {threshold}.asset";
                var asset = ScriptableObject.CreateInstance<LifetimeResourceAchievementSO>();
                AssetDatabase.CreateAsset(asset, assetPath);

                SerializedObject so = new SerializedObject(asset);
                so.FindProperty("_id").stringValue = id;
                so.FindProperty("_resourceType").enumValueIndex = (int)resourceType;
                so.FindProperty("_requiredAmount").intValue = threshold;
                if (icons.TryGetValue(resourceType, out Sprite icon) && icon != null)
                    so.FindProperty("_icon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();

                int index = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(index);
                listProp.GetArrayElementAtIndex(index).objectReferenceValue = asset;
                existingIds.Add(id);
            }
        }

        foreach (int threshold in KillThresholds)
        {
            string id = $"ach_enemies_killed_{threshold}";
            if (existingIds.Contains(id))
                continue;

            string assetPath = $"{Folder}/Enemy Kills {threshold}.asset";
            var asset = ScriptableObject.CreateInstance<EnemyKillsAchievementSO>();
            AssetDatabase.CreateAsset(asset, assetPath);

            SerializedObject so = new SerializedObject(asset);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_killThreshold").intValue = threshold;
            if (icons.TryGetValue(ResourceType.Meat, out Sprite icon) && icon != null)
                so.FindProperty("_icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();

            int index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);
            listProp.GetArrayElementAtIndex(index).objectReferenceValue = asset;
            existingIds.Add(id);
        }

        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Achievement assets generated. Catalog entries: {listProp.arraySize}");
    }

    private static Dictionary<ResourceType, Sprite> BuildIconMap(AllResoursesConfig allResources)
    {
        var map = new Dictionary<ResourceType, Sprite>();
        if (allResources?.Resourses == null)
            return map;

        foreach (ResourseConfig config in allResources.Resourses)
        {
            if (config != null)
                map[config.ResourseType] = config.Icon;
        }

        return map;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
