#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class NpcWorkerAnimatorSetup
{
    public const string MenuPath = "Tools/MyLittleUniverse/Setup Npc Worker Animator";

    private const string FbxPath =
        "Assets/3D Enivronment Assets/Characters Shaun by Quaternius - eJFT9MxzOM/Characters_Shaun.fbx";
    private const string OutputFolder = "Assets/Animations/Npc";
    private const string ControllerPath = OutputFolder + "/NpcWorker.controller";
    private const string PrefabPath = "Assets/Prefabs/Objects/NpcRescueSite.prefab";
    private const string DuckClipPath = OutputFolder + "/Duck.anim";
    private const string IdleClipPath = OutputFolder + "/Idle.anim";
    private const string RunClipPath = OutputFolder + "/Run.anim";
    private const string WaveClipPath = OutputFolder + "/Wave.anim";

    [MenuItem(MenuPath)]
    public static void Setup()
    {
        AnimationClip duckSource = FindFbxClip("CharacterArmature|Duck");
        AnimationClip idleSource = FindFbxClip("CharacterArmature|Idle");
        AnimationClip runSource = FindFbxClip("CharacterArmature|Run");
        AnimationClip waveSource = FindFbxClip("CharacterArmature|Wave");

        if (duckSource == null || idleSource == null || runSource == null || waveSource == null)
        {
            Debug.LogError("NpcWorkerAnimatorSetup: missing Shaun clips Duck/Idle/Run/Wave.");
            return;
        }

        EnsureFolder(OutputFolder);

        AnimationClip duck = ExtractClip(duckSource, DuckClipPath, true);
        AnimationClip idle = ExtractClip(idleSource, IdleClipPath, true);
        AnimationClip run = ExtractClip(runSource, RunClipPath, true);
        AnimationClip wave = ExtractClip(waveSource, WaveClipPath, false);

        AnimatorController controller = BuildController(duck, idle, run, wave);
        AssignToPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"NpcWorkerAnimatorSetup: controller written to {ControllerPath} and assigned on {PrefabPath}.");
    }

    private static AnimationClip FindFbxClip(string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && clip.name == clipName && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    private static AnimationClip ExtractClip(AnimationClip source, string destPath, bool loop)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(destPath);

        AnimationClip copy = Object.Instantiate(source);
        copy.name = Path.GetFileNameWithoutExtension(destPath);
        AssetDatabase.CreateAsset(copy, destPath);
        SetLoop(copy, loop);
        EditorUtility.SetDirty(copy);
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(destPath);
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
    }

    private static AnimatorController BuildController(
        AnimationClip duck,
        AnimationClip idle,
        AnimationClip run,
        AnimationClip wave)
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        ChildAnimatorState[] leftover = machine.states;
        for (int i = 0; i < leftover.Length; i++)
            machine.RemoveState(leftover[i].state);

        AnimatorState duckState = machine.AddState("Duck", new Vector3(260f, 120f, 0f));
        AnimatorState runState = machine.AddState("Run", new Vector3(520f, 120f, 0f));
        AnimatorState waveState = machine.AddState("Wave", new Vector3(520f, 220f, 0f));
        AnimatorState idleState = machine.AddState("Idle", new Vector3(260f, 220f, 0f));

        duckState.motion = duck;
        runState.motion = run;
        waveState.motion = wave;
        idleState.motion = idle;
        machine.defaultState = duckState;

        AnimatorStateTransition waveToIdle = waveState.AddTransition(idleState);
        waveToIdle.hasExitTime = true;
        waveToIdle.exitTime = 0.9f;
        waveToIdle.hasFixedDuration = true;
        waveToIdle.duration = 0.2f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AssignToPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform shaun = FindChildByName(root.transform, "Shaun");
            if (shaun == null)
            {
                Debug.LogError("NpcWorkerAnimatorSetup: Shaun not found in prefab.");
                return;
            }

            Animator animator = shaun.GetComponent<Animator>();
            if (animator == null)
                animator = shaun.gameObject.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            NpcWorker worker = root.GetComponentInChildren<NpcWorker>(true);
            if (worker != null)
            {
                SerializedObject workerSo = new SerializedObject(worker);
                workerSo.FindProperty("_animator").objectReferenceValue = animator;
                workerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            NpcRescueSite site = root.GetComponent<NpcRescueSite>();
            if (site != null)
            {
                Transform cageDoor = FindChildByName(root.transform, "Cylinder.018");
                SerializedObject siteSo = new SerializedObject(site);
                if (cageDoor != null)
                    siteSo.FindProperty("_cageDoor").objectReferenceValue = cageDoor;
                siteSo.FindProperty("_saveId").stringValue = string.Empty;
                siteSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform nested = FindChildByName(parent.GetChild(i), name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
#endif
