#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CrocodileAnimationGenerator
{
    public const string MenuPath = "Tools/MyLittleUniverse/Generate Crocodile Animations";

    private const string OutputFolder = "Assets/3D Enivronment Assets/crocodile-low-poly/Animations";
    private const string IdlePath = OutputFolder + "/CrocodileIdle.anim";
    private const string WalkPath = OutputFolder + "/CrocodileWalk.anim";
    private const string AttackPath = OutputFolder + "/CrocodileAttack.anim";
    private const string ControllerPath = OutputFolder + "/Crocodile.controller";
    private const string SceneObjectName = "cocodrilo_final";

    private const float IdleDuration = 2.4f;
    private const float WalkDuration = 0.72f;
    private const float AttackDuration = 0.9f;
    private const float SampleRate = 30f;

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        Transform root = FindSceneCrocodile();
        if (root == null)
        {
            Debug.LogError($"CrocodileAnimationGenerator: '{SceneObjectName}' not found in the active scene.");
            return;
        }

        EnsureFolder(OutputFolder);

        RestPose rest = RestPose.Capture(root);

        AnimationClip idle = GetOrCreateClip(IdlePath);
        AnimationClip walk = GetOrCreateClip(WalkPath);
        AnimationClip attack = GetOrCreateClip(AttackPath);

        WriteIdle(idle, rest);
        WriteWalk(walk, rest);
        WriteAttack(attack, rest);
        SetAttackEvents(attack);

        AnimatorController controller = BuildController(idle, walk, attack);
        AssignToSceneObject(root.gameObject, controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CrocodileAnimationGenerator: clips and controller written to {OutputFolder}, assigned on '{root.name}'.");
    }

    private static Transform FindSceneCrocodile()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == SceneObjectName)
                return roots[i].transform;

            Transform nested = FindChildByName(roots[i].transform, SceneObjectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindChildByName(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void WriteIdle(AnimationClip clip, RestPose rest)
    {
        clip.ClearCurves();
        SampleClip(clip, IdleDuration, t =>
        {
            float breath = Mathf.Sin(t * Mathf.PI * 2f / IdleDuration);
            float look = Mathf.Sin(t * Mathf.PI / IdleDuration);
            float jaw = Mathf.Max(0f, breath) * 5f;
            return new PoseOffsets
            {
                HeadEuler = new Vector3(breath * 3f, 0f, look * 8f),
                JawEuler = new Vector3(jaw, 0f, 0f),
                TailEuler = new Vector3(0f, 0f, breath * 12f),
                Leg1Euler = new Vector3(breath * 2f, 0f, 0f),
                Leg2Euler = new Vector3(-breath * 2f, 0f, 0f),
                Leg3Euler = new Vector3(-breath * 1.5f, 0f, 0f),
                Leg4Euler = new Vector3(breath * 1.5f, 0f, 0f)
            };
        }, rest);
        SetLoop(clip, true);
        EditorUtility.SetDirty(clip);
    }

    private static void WriteWalk(AnimationClip clip, RestPose rest)
    {
        clip.ClearCurves();
        SampleClip(clip, WalkDuration, t =>
        {
            float stride = Mathf.Sin(t * Mathf.PI * 2f / WalkDuration);
            float plant = Mathf.Cos(t * Mathf.PI * 2f / WalkDuration);
            return new PoseOffsets
            {
                HeadEuler = new Vector3(stride * 6f, 0f, plant * 4f),
                JawEuler = new Vector3(Mathf.Max(0f, stride) * 3f, 0f, 0f),
                TailEuler = new Vector3(0f, 0f, stride * 22f),
                Leg1Euler = new Vector3(stride * 32f, 0f, plant * 6f),
                Leg2Euler = new Vector3(-stride * 32f, 0f, -plant * 6f),
                Leg3Euler = new Vector3(-stride * 28f, 0f, plant * 5f),
                Leg4Euler = new Vector3(stride * 28f, 0f, -plant * 5f)
            };
        }, rest);
        SetLoop(clip, true);
        EditorUtility.SetDirty(clip);
    }

    private static void WriteAttack(AnimationClip clip, RestPose rest)
    {
        clip.ClearCurves();
        SampleClip(clip, AttackDuration, t =>
        {
            float n = t / AttackDuration;
            float coil = Peak(n, 0.08f, 0.18f);
            float strike = Peak(n, 0.22f, 0.48f);
            float bite = Peak(n, 0.38f, 0.58f);
            float recover = Mathf.Clamp01((n - 0.55f) / 0.45f);

            float headPitch = Mathf.Lerp(-14f * coil + 24f * strike, 0f, recover);
            float jawOpen = Mathf.Lerp(8f * coil + 46f * strike * (1f - bite), 0f, recover);
            float tailWhip = Mathf.Lerp(-8f * coil + 28f * Mathf.Sin(n * Mathf.PI * 4f) * strike, 0f, recover);
            float lunge = Mathf.Lerp(-0.04f * coil - 0.2f * strike, 0f, recover);

            return new PoseOffsets
            {
                HeadEuler = new Vector3(headPitch, 0f, strike * 4f),
                HeadPosition = new Vector3(0f, lunge, 0f),
                JawEuler = new Vector3(jawOpen, 0f, 0f),
                TailEuler = new Vector3(strike * 8f, 0f, tailWhip),
                Leg1Euler = new Vector3(strike * 10f, 0f, 0f),
                Leg2Euler = new Vector3(strike * 10f, 0f, 0f),
                Leg3Euler = new Vector3(-strike * 8f, 0f, 0f),
                Leg4Euler = new Vector3(-strike * 8f, 0f, 0f)
            };
        }, rest);
        SetLoop(clip, false);
        EditorUtility.SetDirty(clip);
    }

    private static void SetAttackEvents(AnimationClip clip)
    {
        AnimationUtility.SetAnimationEvents(clip, new[]
        {
            new AnimationEvent { time = AttackDuration * 0.38f, functionName = "StartHit" },
            new AnimationEvent { time = AttackDuration * 0.58f, functionName = "EndHit" }
        });
        EditorUtility.SetDirty(clip);
    }

    private static float Peak(float n, float start, float end)
    {
        if (n <= start || n >= end)
            return 0f;

        float u = Mathf.InverseLerp(start, end, n);
        return Mathf.Sin(u * Mathf.PI);
    }

    private static void SampleClip(AnimationClip clip, float duration, Func<float, PoseOffsets> evaluate, RestPose rest)
    {
        int frames = Mathf.Max(2, Mathf.RoundToInt(duration * SampleRate));
        var headRot = CreateQuaternionTracks();
        var jawRot = CreateQuaternionTracks();
        var tailRot = CreateQuaternionTracks();
        var leg1Rot = CreateQuaternionTracks();
        var leg2Rot = CreateQuaternionTracks();
        var leg3Rot = CreateQuaternionTracks();
        var leg4Rot = CreateQuaternionTracks();
        var headPos = new AnimationCurve[3];
        for (int i = 0; i < 3; i++)
            headPos[i] = new AnimationCurve();

        for (int i = 0; i <= frames; i++)
        {
            float t = duration * i / frames;
            PoseOffsets pose = evaluate(t);
            AddQuaternionKey(headRot, t, rest.HeadRotation * Quaternion.Euler(pose.HeadEuler));
            AddQuaternionKey(jawRot, t, rest.JawRotation * Quaternion.Euler(pose.JawEuler));
            AddQuaternionKey(tailRot, t, rest.TailRotation * Quaternion.Euler(pose.TailEuler));
            AddQuaternionKey(leg1Rot, t, rest.Leg1Rotation * Quaternion.Euler(pose.Leg1Euler));
            AddQuaternionKey(leg2Rot, t, rest.Leg2Rotation * Quaternion.Euler(pose.Leg2Euler));
            AddQuaternionKey(leg3Rot, t, rest.Leg3Rotation * Quaternion.Euler(pose.Leg3Euler));
            AddQuaternionKey(leg4Rot, t, rest.Leg4Rotation * Quaternion.Euler(pose.Leg4Euler));

            Vector3 position = rest.HeadPosition + pose.HeadPosition;
            headPos[0].AddKey(t, position.x);
            headPos[1].AddKey(t, position.y);
            headPos[2].AddKey(t, position.z);
        }

        BindQuaternion(clip, "Head", headRot);
        BindQuaternion(clip, "Head/Jaw", jawRot);
        BindQuaternion(clip, "Tail1", tailRot);
        BindQuaternion(clip, "Leg1", leg1Rot);
        BindQuaternion(clip, "Leg2", leg2Rot);
        BindQuaternion(clip, "Leg3", leg3Rot);
        BindQuaternion(clip, "Leg4", leg4Rot);

        Smooth(headPos[0]);
        Smooth(headPos[1]);
        Smooth(headPos[2]);
        clip.SetCurve("Head", typeof(Transform), "localPosition.x", headPos[0]);
        clip.SetCurve("Head", typeof(Transform), "localPosition.y", headPos[1]);
        clip.SetCurve("Head", typeof(Transform), "localPosition.z", headPos[2]);

        clip.EnsureQuaternionContinuity();
        clip.frameRate = SampleRate;
    }

    private static AnimationCurve[] CreateQuaternionTracks()
    {
        return new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
    }

    private static void AddQuaternionKey(AnimationCurve[] tracks, float time, Quaternion rotation)
    {
        tracks[0].AddKey(time, rotation.x);
        tracks[1].AddKey(time, rotation.y);
        tracks[2].AddKey(time, rotation.z);
        tracks[3].AddKey(time, rotation.w);
    }

    private static void BindQuaternion(AnimationClip clip, string path, AnimationCurve[] tracks)
    {
        for (int i = 0; i < tracks.Length; i++)
            Smooth(tracks[i]);

        clip.SetCurve(path, typeof(Transform), "localRotation.x", tracks[0]);
        clip.SetCurve(path, typeof(Transform), "localRotation.y", tracks[1]);
        clip.SetCurve(path, typeof(Transform), "localRotation.z", tracks[2]);
        clip.SetCurve(path, typeof(Transform), "localRotation.w", tracks[3]);
    }

    private static void Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
    }

    private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip attack)
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        ChildAnimatorState[] leftover = machine.states;
        for (int i = 0; i < leftover.Length; i++)
            machine.RemoveState(leftover[i].state);

        AnimatorState idleState = machine.AddState("Idle", new Vector3(260f, 120f, 0f));
        AnimatorState walkState = machine.AddState("Walk", new Vector3(260f, 220f, 0f));
        AnimatorState attackState = machine.AddState("Attack", new Vector3(520f, 170f, 0f));

        idleState.motion = idle;
        walkState.motion = walk;
        attackState.motion = attack;
        machine.defaultState = idleState;

        AddBoolTransition(idleState, walkState, "IsRunning", true, false, 0.12f);
        AddBoolTransition(walkState, idleState, "IsRunning", false, false, 0.18f);
        AddTriggerTransition(idleState, attackState, "Attack", 0.08f);
        AddTriggerTransition(walkState, attackState, "Attack", 0.08f);

        AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.85f;
        attackToIdle.hasFixedDuration = true;
        attackToIdle.duration = 0.15f;
        attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsRunning");

        AnimatorStateTransition attackToWalk = attackState.AddTransition(walkState);
        attackToWalk.hasExitTime = true;
        attackToWalk.exitTime = 0.85f;
        attackToWalk.hasFixedDuration = true;
        attackToWalk.duration = 0.12f;
        attackToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsRunning");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool value, bool hasExitTime, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = hasExitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.exitTime = 0f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string param, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.exitTime = 0f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, param);
    }

    private static void AssignToSceneObject(GameObject root, AnimatorController controller)
    {
        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(root);

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        CrocodileAnimation driver = root.GetComponent<CrocodileAnimation>();
        if (driver == null)
            driver = Undo.AddComponent<CrocodileAnimation>(root);

        SerializedObject so = new SerializedObject(driver);
        so.FindProperty("_animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
    }

    private static AnimationClip GetOrCreateClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = SampleRate };
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
            clip.frameRate = SampleRate;
        }

        return clip;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = "Assets/3D Enivronment Assets/crocodile-low-poly";
        if (!AssetDatabase.IsValidFolder(parent))
            throw new InvalidOperationException($"Missing folder: {parent}");

        AssetDatabase.CreateFolder(parent, "Animations");
    }

    private struct PoseOffsets
    {
        public Vector3 HeadEuler;
        public Vector3 HeadPosition;
        public Vector3 JawEuler;
        public Vector3 TailEuler;
        public Vector3 Leg1Euler;
        public Vector3 Leg2Euler;
        public Vector3 Leg3Euler;
        public Vector3 Leg4Euler;
    }

    private struct RestPose
    {
        public Quaternion HeadRotation;
        public Vector3 HeadPosition;
        public Quaternion JawRotation;
        public Quaternion TailRotation;
        public Quaternion Leg1Rotation;
        public Quaternion Leg2Rotation;
        public Quaternion Leg3Rotation;
        public Quaternion Leg4Rotation;

        public static RestPose Capture(Transform root)
        {
            Transform head = root.Find("Head");
            Transform jaw = root.Find("Head/Jaw");
            Transform tail = root.Find("Tail1");
            Transform leg1 = root.Find("Leg1");
            Transform leg2 = root.Find("Leg2");
            Transform leg3 = root.Find("Leg3");
            Transform leg4 = root.Find("Leg4");

            return new RestPose
            {
                HeadRotation = head != null ? head.localRotation : Quaternion.identity,
                HeadPosition = head != null ? head.localPosition : Vector3.zero,
                JawRotation = jaw != null ? jaw.localRotation : Quaternion.identity,
                TailRotation = tail != null ? tail.localRotation : Quaternion.identity,
                Leg1Rotation = leg1 != null ? leg1.localRotation : Quaternion.identity,
                Leg2Rotation = leg2 != null ? leg2.localRotation : Quaternion.identity,
                Leg3Rotation = leg3 != null ? leg3.localRotation : Quaternion.identity,
                Leg4Rotation = leg4 != null ? leg4.localRotation : Quaternion.identity
            };
        }
    }
}
#endif
