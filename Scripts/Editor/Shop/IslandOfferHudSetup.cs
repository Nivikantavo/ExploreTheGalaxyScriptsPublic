#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class IslandOfferHudSetup
{
    private const string PrefabPath = "Assets/Prefabs/UI/Shop/IslandOfferPopup.prefab";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    [MenuItem("Tools/Shop/Create Island Offer UI Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder("Assets/Prefabs/UI/Shop");
        GameObject root = IslandOfferUiBuilder.BuildUnder(
            null,
            IslandOfferUiBuilder.LoadResourceCellPrefab()).gameObject;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Island Offer UI prefab saved to {PrefabPath}.");
    }

    [MenuItem("Tools/Shop/Setup Island Offer UI in Gameplay Scene")]
    public static void SetupInGameplayScene()
    {
        EnsureFolder("Assets/Prefabs/UI/Shop");

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("IslandOfferHudSetup: Canvas not found in GameplayScene.");
            return;
        }

        IslandOfferPopupView existing = Object.FindFirstObjectByType<IslandOfferPopupView>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Debug.LogWarning("IslandOfferHudSetup: IslandOfferPopupView already exists in the scene.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance;
        if (prefabAsset != null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, canvas.transform);
        }
        else
        {
            instance = IslandOfferUiBuilder.BuildUnder(
                canvas.transform,
                IslandOfferUiBuilder.LoadResourceCellPrefab()).gameObject;
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, PrefabPath, InteractionMode.AutomatedAction);
        }

        instance.name = "IslandOfferView";
        StretchFull(instance.GetComponent<RectTransform>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = instance;
        Debug.Log("Island Offer UI installed in GameplayScene under Canvas.");
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
