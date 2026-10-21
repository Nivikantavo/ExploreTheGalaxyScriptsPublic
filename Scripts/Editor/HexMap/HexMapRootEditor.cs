using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexMapRoot))]
public class HexMapRootEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Open Hex Map Builder"))
            HexMapEditorWindow.Open();
    }
}
