#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ResetWorld2ProgressMenu
{
    private const string MenuPath = "Tools/MyLittleUniverse/Reset World 2 Progress";

    [MenuItem(MenuPath)]
    public static void ResetWorld2Progress()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Reset World 2 Progress",
                "While Play Mode is running use Debug Cheats (F1) → Reset World 2 Progress.",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Reset World 2 Progress",
                "Clear World 2 islands, goals, dungeons and world objects.\n\nWorld 1, inventory, IAP and planet unlocks stay.",
                "Reset World 2",
                "Cancel"))
        {
            return;
        }

        if (!SaveWorldAccess.TryResetInStorage(new PlayerPrefsSaveLoader(), SaveWorldIds.World2))
        {
            EditorUtility.DisplayDialog(
                "Reset World 2 Progress",
                "No save found, or World 2 could not be reset.",
                "OK");
            return;
        }

        Debug.Log("ResetWorld2ProgressMenu: World 2 progress cleared. World 1 kept.");
    }
}
#endif
