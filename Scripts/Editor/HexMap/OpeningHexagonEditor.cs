using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OpeningHexagon))]
public class OpeningHexagonEditor : Editor
{
    private void OnSceneGUI()
    {
        var opener = (OpeningHexagon)target;
        opener.DrawEditorGizmo();
    }
}

public static class OpeningHexagonGizmoUtility
{
    public static void DrawEditorGizmo(this OpeningHexagon opener)
    {
        if (opener == null)
            return;

        var from = opener.transform.position + Vector3.up * 0.15f;

        var openedHex = opener.OpenedHexagon;
        if (openedHex != null)
        {
            var to = openedHex.transform.position + Vector3.up * 0.15f;
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            Handles.DrawDottedLine(from, to, 4f);
            Handles.DrawSolidDisc(to, Vector3.up, 0.12f);
        }

        if (opener.ObjectToDisable != null)
        {
            var to = opener.ObjectToDisable.transform.position + Vector3.up * 0.15f;
            Handles.color = new Color(1f, 0.35f, 0.2f, 0.95f);
            Handles.DrawDottedLine(from, to, 4f);
            Handles.DrawSolidDisc(to, Vector3.up, 0.12f);
        }
    }

    public static void DrawAllInMap(HexMapRoot mapRoot)
    {
        if (mapRoot == null)
            return;

        mapRoot.EnsureContainers();

        foreach (Transform child in mapRoot.transform.GetComponentsInChildren<Transform>(true))
        {
            if (!child.TryGetComponent<OpeningHexagon>(out var opener))
                continue;

            opener.DrawEditorGizmo();
        }
    }
}
