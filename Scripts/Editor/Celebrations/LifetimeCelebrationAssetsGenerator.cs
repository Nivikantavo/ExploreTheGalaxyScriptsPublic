#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LifetimeCelebrationAssetsGenerator
{
    private const string LifetimeFolder = "Assets/Configs/Celebration/Lifetime";
    private const string CatalogPath = "Assets/Configs/Celebration/CelebrationCatalog.asset";
    private const string AllResourcesPath = "Assets/Configs/ResoursesConf/AllResoursesConfig.asset";

    [MenuItem("Tools/Celebrations/Generate Lifetime Resource Celebrations")]
    public static void GenerateLifetimeResourceCelebrations()
    {
        EnsureFolder(LifetimeFolder);

        CelebrationCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CelebrationCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"Catalog not found at {CatalogPath}");
            return;
        }

        AllResoursesConfig allResources = AssetDatabase.LoadAssetAtPath<AllResoursesConfig>(AllResourcesPath);
        Sprite enemyIcon = FindResourceIcon(allResources, ResourceType.Meat);

        SerializedObject catalogSo = new SerializedObject(catalog);
        SerializedProperty celebrationsProp = catalogSo.FindProperty("_celebrations");

        CreateEnemyKillIfMissing(celebrationsProp, 10, enemyIcon);
        CreateEnemyKillIfMissing(celebrationsProp, 50, enemyIcon);
        CreateEnemyKillIfMissing(celebrationsProp, 100, enemyIcon);

        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Lifetime resource celebrations stay on the single catalog config. Enemy kill celebrations were checked.");
    }

    private static void CreateEnemyKillIfMissing(SerializedProperty celebrationsProp, int threshold, Sprite icon)
    {
        string id = $"enemies_killed_{threshold}";
        for (int i = 0; i < celebrationsProp.arraySize; i++)
        {
            var existing = celebrationsProp.GetArrayElementAtIndex(i).objectReferenceValue as EnemyKillsCelebrationSO;
            if (existing != null && existing.KillThreshold == threshold)
                return;
        }

        string assetPath = $"{LifetimeFolder}/Enemy Kills {threshold}.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyKillsCelebrationSO>(assetPath) != null)
            return;

        var asset = ScriptableObject.CreateInstance<EnemyKillsCelebrationSO>();
        AssetDatabase.CreateAsset(asset, assetPath);

        SerializedObject so = new SerializedObject(asset);
        so.FindProperty("_id").stringValue = id;
        so.FindProperty("_killThreshold").intValue = threshold;
        if (icon != null)
            so.FindProperty("_icon").objectReferenceValue = icon;
        so.ApplyModifiedPropertiesWithoutUndo();

        int index = celebrationsProp.arraySize;
        celebrationsProp.InsertArrayElementAtIndex(index);
        celebrationsProp.GetArrayElementAtIndex(index).objectReferenceValue = asset;
    }

    private static Sprite FindResourceIcon(AllResoursesConfig allResources, ResourceType resourceType)
    {
        if (allResources?.Resourses == null)
            return null;

        foreach (ResourseConfig config in allResources.Resourses)
        {
            if (config != null && config.ResourseType == resourceType)
                return config.Icon;
        }

        return null;
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
