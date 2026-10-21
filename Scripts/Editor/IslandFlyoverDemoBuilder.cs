#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

public static class IslandFlyoverDemoBuilder
{
    private const string RootName = "Demo";
    private const string RailName = "Rail";
    private const string CameraName = "Flyover Camera";
    private const float TargetDurationSeconds = 80f;
    private const int FlyoverPriority = 100;

    [MenuItem("Tools/MyLittleUniverse/Build Island Flyover Demo")]
    public static void BuildActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Island flyover: exit Play mode before rebuilding the rail.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        List<IslandStop> stops = CollectStops(scene);
        if (stops.Count == 0)
        {
            Debug.LogWarning($"Island flyover: scene '{scene.name}' has no islands.");
            return;
        }

        stops.Sort((a, b) => a.Index.CompareTo(b.Index));

        IslandFlyoverDemo previous = FindExisting(scene);
        float speed = 0f;
        float pitch = 16f;
        bool loop = false;
        bool hideScreenUi = true;
        bool wasActive = false;
        if (previous != null)
        {
            speed = previous.Speed;
            pitch = previous.DownwardPitch;
            loop = previous.Loop;
            hideScreenUi = previous.HideScreenUi;
            wasActive = previous.gameObject.activeSelf;
            Undo.DestroyObjectImmediate(previous.gameObject);
        }

        float clearance = ChooseClearance(stops);
        List<Vector3> knots = BuildKnots(stops, clearance);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Island Flyover Demo");
        SceneManager.MoveGameObjectToScene(root, scene);

        GameObject railObject = new GameObject(RailName);
        Undo.RegisterCreatedObjectUndo(railObject, "Build Island Flyover Demo");
        Undo.SetTransformParent(railObject.transform, root.transform, "Build Island Flyover Demo");
        SplineContainer spline = railObject.AddComponent<SplineContainer>();
        CinemachineSplineSmoother smoother = railObject.AddComponent<CinemachineSplineSmoother>();
        smoother.AutoSmooth = true;
        WriteSpline(spline, railObject.transform, knots);
        smoother.SmoothSplineNow();
        EditorUtility.SetDirty(spline);

        GameObject cameraObject = new GameObject(CameraName);
        Undo.RegisterCreatedObjectUndo(cameraObject, "Build Island Flyover Demo");
        Undo.SetTransformParent(cameraObject.transform, root.transform, "Build Island Flyover Demo");
        CinemachineCamera flyoverCamera = cameraObject.AddComponent<CinemachineCamera>();
        PrioritySettings priority = flyoverCamera.Priority;
        priority.Enabled = true;
        priority.Value = FlyoverPriority;
        flyoverCamera.Priority = priority;
        CopyOutputChannel(flyoverCamera, scene);

        LensSettings lens = flyoverCamera.Lens;
        lens.FieldOfView = 40f;
        lens.NearClipPlane = 0.3f;
        lens.FarClipPlane = 3000f;
        flyoverCamera.Lens = lens;

        CinemachineSplineDolly dolly = cameraObject.AddComponent<CinemachineSplineDolly>();
        dolly.Spline = spline;
        dolly.PositionUnits = PathIndexUnit.Distance;
        dolly.CameraPosition = 0f;
        dolly.CameraRotation = CinemachineSplineDolly.RotationMode.SplineNoRoll;
        dolly.Damping = default;

        IslandFlyoverPitch pitchComponent = cameraObject.AddComponent<IslandFlyoverPitch>();
        IslandFlyoverDemo demo = root.AddComponent<IslandFlyoverDemo>();

        float length = spline.CalculateLength();
        if (speed <= 0f)
            speed = Mathf.Max(1f, length / TargetDurationSeconds);

        demo.Bind(flyoverCamera, dolly, pitchComponent);
        demo.ApplyAuthoredSettings(speed, pitch, loop, hideScreenUi);
        root.SetActive(wasActive);

        if (knots.Count > 0)
            cameraObject.transform.position = knots[0];

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        float duration = speed > 0.01f ? length / speed : 0f;
        Debug.Log(
            $"Island flyover: {stops.Count} islands, rail {length:0} m, {speed:0.0} m/s, about {duration:0} s. " +
            "Enable Demo to record. Disable or delete it to restore the gameplay camera.",
            root);
    }

    private static IslandFlyoverDemo FindExisting(Scene scene)
    {
        IslandFlyoverDemo[] demos = Object.FindObjectsByType<IslandFlyoverDemo>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (IslandFlyoverDemo demo in demos)
        {
            if (demo != null && demo.gameObject.scene == scene)
                return demo;
        }

        return null;
    }

    private static List<IslandStop> CollectStops(Scene scene)
    {
        IslandProgressHandler[] islands = Object.FindObjectsByType<IslandProgressHandler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        List<IslandStop> stops = new List<IslandStop>(islands.Length);
        foreach (IslandProgressHandler island in islands)
        {
            if (island == null || island.gameObject.scene != scene)
                continue;

            Vector3 focus = island.GetFocusWorldPosition();
            float radius = EstimateRadius(island, focus);
            stops.Add(new IslandStop(island.IslandIndex, focus, radius));
        }

        return stops;
    }

    private static float EstimateRadius(IslandProgressHandler island, Vector3 focus)
    {
        float radius = 8f;
        HexTile[] tiles = island.GetComponentsInChildren<HexTile>(true);
        foreach (HexTile tile in tiles)
        {
            if (tile == null)
                continue;

            Vector3 offset = tile.transform.position - focus;
            offset.y = 0f;
            radius = Mathf.Max(radius, offset.magnitude);
        }

        return radius;
    }

    private static float ChooseClearance(List<IslandStop> stops)
    {
        float[] radii = new float[stops.Count];
        for (int i = 0; i < stops.Count; i++)
            radii[i] = stops[i].Radius;

        System.Array.Sort(radii);
        float median = radii[radii.Length / 2];
        return Mathf.Clamp(median * 1.15f, 28f, 55f);
    }

    private static List<Vector3> BuildKnots(List<IslandStop> stops, float clearance)
    {
        List<Vector3> knots = new List<Vector3>(stops.Count + 2);
        float lead = ChooseLead(stops);

        Vector3 enter = stops.Count > 1
            ? FlatDirection(stops[0].Focus, stops[1].Focus)
            : Vector3.forward;
        knots.Add(stops[0].Focus - enter * lead + Vector3.up * clearance);

        foreach (IslandStop stop in stops)
            knots.Add(stop.Focus + Vector3.up * clearance);

        Vector3 exit = stops.Count > 1
            ? FlatDirection(stops[stops.Count - 2].Focus, stops[stops.Count - 1].Focus)
            : Vector3.forward;
        IslandStop last = stops[stops.Count - 1];
        knots.Add(last.Focus + exit * lead + Vector3.up * clearance);
        return knots;
    }

    private static float ChooseLead(List<IslandStop> stops)
    {
        if (stops.Count < 2)
            return 24f;

        float spacing = 0f;
        for (int i = 1; i < stops.Count; i++)
        {
            Vector3 delta = stops[i].Focus - stops[i - 1].Focus;
            delta.y = 0f;
            spacing += delta.magnitude;
        }

        spacing /= stops.Count - 1;
        return Mathf.Clamp(spacing * 0.45f, 16f, 48f);
    }

    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return Vector3.forward;

        return direction.normalized;
    }

    private static void WriteSpline(SplineContainer container, Transform rail, List<Vector3> worldKnots)
    {
        Spline spline = container.Spline;
        spline.Clear();
        spline.Closed = false;

        foreach (Vector3 worldKnot in worldKnots)
        {
            float3 local = rail.InverseTransformPoint(worldKnot);
            spline.Add(new BezierKnot(local), TangentMode.AutoSmooth);
        }
    }

    private static void CopyOutputChannel(CinemachineCamera flyoverCamera, Scene scene)
    {
        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null || camera == flyoverCamera || camera.gameObject.scene != scene)
                continue;

            flyoverCamera.OutputChannel = camera.OutputChannel;
            return;
        }
    }

    private readonly struct IslandStop
    {
        public IslandStop(int index, Vector3 focus, float radius)
        {
            Index = index;
            Focus = focus;
            Radius = radius;
        }

        public int Index { get; }
        public Vector3 Focus { get; }
        public float Radius { get; }
    }
}

[CustomEditor(typeof(IslandFlyoverDemo))]
public class IslandFlyoverDemoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        IslandFlyoverDemo demo = (IslandFlyoverDemo)target;
        float length = demo.PathLength;
        float duration = demo.Speed > 0.01f ? length / demo.Speed : 0f;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            $"Длина рельсы: {length:0} м. Пролёт займёт около {duration:0} с.\n\n" +
            "Путь — дочерний объект Rail. Точки можно двигать в Scene view.\n" +
            "Скорость и наклон — на этом объекте.\n" +
            "Включите Demo перед записью. Выключите или удалите его, чтобы вернуть игровую камеру.",
            MessageType.Info);

        if (GUILayout.Button("Пересобрать рельсу по островам"))
            IslandFlyoverDemoBuilder.BuildActiveScene();
    }
}

[InitializeOnLoad]
internal static class IslandFlyoverAutoBuild
{
    private static bool _waitingForCompile;

    static IslandFlyoverAutoBuild()
    {
        EditorApplication.delayCall += TryBuildOpenScene;
        EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += TryBuildOpenScene;
    }

    private static void TryBuildOpenScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (_waitingForCompile)
                return;

            _waitingForCompile = true;
            EditorApplication.delayCall += () =>
            {
                _waitingForCompile = false;
                TryBuildOpenScene();
            };
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            return;

        string scenePath = scene.path.Replace('\\', '/');
        if (!scenePath.StartsWith("Assets/Scenes/GameplayScene"))
            return;

        string key = "MyLittleUniverse.IslandFlyoverDemo." + scenePath;
        if (EditorPrefs.GetBool(key, false))
            return;

        if (Object.FindObjectsByType<IslandFlyoverDemo>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(demo => demo != null && demo.gameObject.scene == scene))
        {
            EditorPrefs.SetBool(key, true);
            return;
        }

        IslandFlyoverDemoBuilder.BuildActiveScene();
        bool created = Object.FindObjectsByType<IslandFlyoverDemo>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(demo => demo != null && demo.gameObject.scene == scene);
        if (created)
            EditorPrefs.SetBool(key, true);
    }
}

[InitializeOnLoad]
internal static class IslandFlyoverPreviewReset
{
    static IslandFlyoverPreviewReset()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        IslandFlyoverDemo[] demos = Object.FindObjectsByType<IslandFlyoverDemo>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (IslandFlyoverDemo demo in demos)
        {
            if (demo != null)
                demo.ApplyPreviewPosition();
        }
    }
}
#endif
