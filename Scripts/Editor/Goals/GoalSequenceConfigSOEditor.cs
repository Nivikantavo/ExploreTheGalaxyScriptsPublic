using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GoalSequenceConfigSO))]
public class GoalSequenceConfigSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Open Goals Sequence Editor", GUILayout.Height(28f)))
            GoalSequenceEditorWindow.Open((GoalSequenceConfigSO)target);
    }
}
