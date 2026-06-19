using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class SetupMatthewEditor : EditorWindow
{
    [MenuItem("Tools/Setup Mission Commander Matthew")]
    public static void SetupMatthew()
    {
        string animationsPath = "Assets/Matthew - Stylized Character/Matthew/Animations";
        string prefabPath = "Assets/Matthew - Stylized Character/Matthew/Prefab/MatthewModel.prefab";
        string resourcesFolder = "Assets/Resources";
        string targetPrefabPath = "Assets/Resources/MatthewModel.prefab";
        string controllerPath = "Assets/Resources/MatthewAnimator.controller";

        // 1. Create Resources folder if it doesn't exist
        if (!AssetDatabase.IsValidFolder(resourcesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 2. Create Animator Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // Add Parameters
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);

        // Get State Machine
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Load Animation Clips
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animationsPath}/Matthew_Idle_A.fbx");
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animationsPath}/Matthew_Run.fbx");
        AnimationClip jumpClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animationsPath}/Matthew_Jump.fbx");

        if (idleClip == null) Debug.LogWarning("Could not find Matthew_Idle_A.fbx");
        if (walkClip == null) Debug.LogWarning("Could not find Matthew_Run.fbx");
        if (jumpClip == null) Debug.LogWarning("Could not find Matthew_Jump.fbx");

        // Create States
        AnimatorState idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;

        AnimatorState walkState = rootStateMachine.AddState("Walk");
        walkState.motion = walkClip;

        AnimatorState interactState = rootStateMachine.AddState("Interact");
        interactState.motion = jumpClip;

        // Set Default State
        rootStateMachine.defaultState = idleState;

        // Idle -> Walk
        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        idleToWalk.duration = 0.1f;

        // Walk -> Idle
        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        walkToIdle.duration = 0.1f;

        // Any -> Interact
        AnimatorStateTransition anyToInteract = rootStateMachine.AddAnyStateTransition(interactState);
        anyToInteract.AddCondition(AnimatorConditionMode.If, 0, "Interact");
        anyToInteract.duration = 0.1f;

        // Interact -> Idle (after animation finishes)
        AnimatorStateTransition interactToIdle = interactState.AddTransition(idleState);
        interactToIdle.hasExitTime = true;
        interactToIdle.exitTime = 0.9f;
        interactToIdle.duration = 0.1f;

        // 3. Setup Prefab in Resources
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogError($"Could not find source prefab at {prefabPath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = instance.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // We handle movement via scripts/DOTween

        // Save as new prefab in Resources
        PrefabUtility.SaveAsPrefabAsset(instance, targetPrefabPath);
        DestroyImmediate(instance);

        Debug.Log("✅ Matthew Setup Complete! 'MatthewModel' is now in Assets/Resources and ready to spawn.");
    }
}
