#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AddResourceEffectsSetup
{
    private const string PickupPopupPrefabPath = "Assets/Prefabs/UI/PickupResoursePopup.prefab";
    private const string LowLevelPopupPrefabPath = "Assets/Prefabs/UI/LowLevelHitPopup.prefab";
    private const string MenuPath = "Tools/MyLittleUniverse/Add Resource Effects To Selected";

    [MenuItem(MenuPath)]
    public static void AddResourceEffects()
    {
        GameObject pickupPopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPopupPrefabPath);
        GameObject lowLevelPopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LowLevelPopupPrefabPath);

        if (pickupPopupPrefab == null)
            Debug.LogWarning($"AddResourceEffectsSetup: Prefab not found at {PickupPopupPrefabPath}. ResoursePopapEffects prefab will not be assigned.");
        if (lowLevelPopupPrefab == null)
            Debug.LogWarning($"AddResourceEffectsSetup: Prefab not found at {LowLevelPopupPrefabPath}. ResourceLowLevelPopap prefab will not be assigned.");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Add Resource Effects");
        int group = Undo.GetCurrentGroup();

        int processed = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            ConfigureObject(go, pickupPopupPrefab, lowLevelPopupPrefab);
            processed++;
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"AddResourceEffectsSetup: Added resource effects to {processed} object(s).");
    }

    [MenuItem(MenuPath, true)]
    public static bool AddResourceEffectsValidate()
    {
        return Selection.activeGameObject != null;
    }

    private static void ConfigureObject(GameObject go, GameObject pickupPopupPrefab, GameObject lowLevelPopupPrefab)
    {
        ShakingAnimation shaking = GetOrAdd<ShakingAnimation>(go);
        SetObjectReference(shaking, "_target", go.transform);

        RepairAnimation repair = GetOrAdd<RepairAnimation>(go);
        SetObjectReference(repair, "_target", go.transform);

        ResoursePopapEffects popapEffects = GetOrAdd<ResoursePopapEffects>(go);
        if (pickupPopupPrefab != null)
            SetObjectReference(popapEffects, "_pickupPopupPrefab", pickupPopupPrefab);

        GetOrAdd<ResourcesObjectDropEffect>(go);

        ResourceLowLevelPopap lowLevelPopap = GetOrAdd<ResourceLowLevelPopap>(go);
        if (lowLevelPopupPrefab != null)
            SetObjectReference(lowLevelPopap, "_pickupPopupPrefab", lowLevelPopupPrefab);

        EditorUtility.SetDirty(go);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    private static void SetObjectReference(Component component, string propertyName, Object value)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"AddResourceEffectsSetup: Property '{propertyName}' not found on {component.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
#endif
