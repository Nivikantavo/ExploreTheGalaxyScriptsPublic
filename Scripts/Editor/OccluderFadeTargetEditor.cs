#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class OccluderFadeTargetEditor
{
    [MenuItem("Tools/Occluder Fade/Add To Selected")]
    private static void AddToSelected()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject.GetComponentInChildren<Renderer>(true) == null)
                continue;

            if (selectedObject.GetComponent<OccluderFadeTarget>() == null)
                Undo.AddComponent<OccluderFadeTarget>(selectedObject);
        }
    }
}
#endif
