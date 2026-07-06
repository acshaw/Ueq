using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds a 1D locomotion blend-tree AnimatorController from Mixamo (or any
/// Humanoid) clips, so you don't have to wire the blend tree by hand.
///
/// Usage:
///   1. Import your Mixamo FBX clips into <see cref="ClipFolder"/> (set each
///      one's Rig → Animation Type → Humanoid, and Loop Time → on).
///   2. Run Tools/Build Player Locomotion Controller.
///   3. Assign the generated controller to the Animator on your Synty character,
///      and add the <c>PlayerAnimator</c> component to that same object.
///
/// Clips are matched by filename keyword (idle / walk / run|jog|sprint). Any
/// slot it can't find is left empty with a warning — just drag the clip into
/// the blend tree node afterwards.
/// </summary>
public static class PlayerAnimatorSetup
{
    const string ClipFolder   = "Assets/Animations/Locomotion";
    const string OutputPath   = "Assets/Animations/PlayerLocomotion.controller";
    const string SpeedParam   = "Speed";
    const string AttackParam  = "Attack";
    const string KickParam    = "Kick";
    const string CastParam    = "Cast";
    const string SitParam     = "Sitting";

    // Blend thresholds — match NetworkedPlayer.moveSpeed (3, walk) and sprintSpeed (5, run).
    // PlayerAnimator feeds the real world-space speed, so these must equal the actual
    // movement speeds or the character will play the wrong clip (e.g. running while walking).
    const float WalkSpeed = 3f;
    const float RunSpeed  = 5f;

    [MenuItem("Tools/Build Player Locomotion Controller")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(ClipFolder))
        {
            EditorUtility.DisplayDialog("Build Locomotion Controller",
                $"No clips folder found.\n\nCreate {ClipFolder} and import your Mixamo " +
                "FBX clips (Idle, Walk, Run) into it first, set each to Humanoid, then re-run.",
                "OK");
            Debug.LogError($"[PlayerAnimatorSetup] Missing folder: {ClipFolder}");
            return;
        }

        // Collect every AnimationClip in the folder, including clips nested inside FBX
        // files. Match on the SOURCE FILENAME, not the clip's internal name — Mixamo
        // names every embedded clip "mixamo.com", so the filename is the only reliable key.
        var entries = new List<(AnimationClip clip, string key)>();
        foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { ClipFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var key  = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview"))
                    entries.Add((c, key));
        }

        if (entries.Count == 0)
        {
            Debug.LogError($"[PlayerAnimatorSetup] No AnimationClips found in {ClipFolder}. " +
                           "Check the FBX rigs are set to Humanoid.");
            return;
        }

        AnimationClip Match(System.Func<string, bool> pred) =>
            entries.FirstOrDefault(e => pred(e.key)).clip;

        var idle = Match(k => k.Contains("idle"));
        // Exclude "backward(s)" so plain "Walking" wins over "Walking Backwards".
        var walk = Match(k => k.Contains("walk") && !k.Contains("back"));
        var run  = Match(k => k.Contains("run") || k.Contains("jog") || k.Contains("sprint"));
        var attack = Match(k => k.Contains("attack") || k.Contains("slash") || k.Contains("swing"));
        var kick   = Match(k => k.Contains("kick"));
        var cast   = Match(k => k.Contains("cast") || k.Contains("spell"));
        var sit    = Match(k => k.Contains("sit"));

        if (idle == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'idle' clip found — leaving idle node empty.");
        if (walk == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'walk' clip found — leaving walk node empty.");
        if (run  == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'run/jog/sprint' clip found — leaving run node empty.");
        if (attack == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'attack/slash/swing' clip found — skipping Attack state.");
        if (kick   == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'kick' clip found — skipping Kick state.");
        if (cast   == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'cast/spell' clip found — skipping Cast state (3.1.5 spells won't animate).");
        if (sit    == null) Debug.LogWarning("[PlayerAnimatorSetup] No 'sit' clip found — skipping Sit state (3.1.7 sitting won't animate). Import sitting.fbx as Humanoid + Loop Time.");

        // Ensure the output directory exists.
        var dir = System.IO.Path.GetDirectoryName(OutputPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir), System.IO.Path.GetFileName(dir));

        // Fresh controller each run.
        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
        controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);

        var blendTree = new BlendTree
        {
            name                  = "Locomotion",
            blendType             = BlendTreeType.Simple1D,
            blendParameter        = SpeedParam,
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(blendTree, controller);

        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, WalkSpeed);
        blendTree.AddChild(run,  RunSpeed);

        var sm    = controller.layers[0].stateMachine;
        var state = sm.AddState("Locomotion");
        state.motion       = blendTree;
        sm.defaultState    = state;

        // One-shot action states (Attack, Kick, …): each entered from anywhere via
        // its own trigger, returning to Locomotion when the clip finishes. Full-body
        // override — the player stands still to perform these, so blending the upper
        // body isn't needed yet.
        if (attack != null) AddTriggeredState(controller, sm, state, attack, AttackParam, "Attack");
        if (kick   != null) AddTriggeredState(controller, sm, state, kick,   KickParam,   "Kick");
        if (cast   != null) AddTriggeredState(controller, sm, state, cast,   CastParam,   "Cast");

        // Held (bool-driven) state: Locomotion → Sit while Sitting is true, back when it clears (3.1.7).
        if (sit    != null) AddBoolState(controller, sm, state, sit, SitParam, "Sit");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(controller);
        Debug.Log($"[PlayerAnimatorSetup] Built {OutputPath} — idle:{(idle ? idle.name : "MISSING")} " +
                  $"walk:{(walk ? walk.name : "MISSING")} run:{(run ? run.name : "MISSING")} " +
                  $"attack:{(attack ? attack.name : "MISSING")} kick:{(kick ? kick.name : "MISSING")} " +
                  $"cast:{(cast ? cast.name : "MISSING")} sit:{(sit ? sit.name : "MISSING")}");
    }

    // Builds a held state driven by a bool: Locomotion → state while the bool is true, state → Locomotion
    // when it clears. Used for sitting (3.1.7) — a looping pose, not a one-shot like AddTriggeredState.
    static void AddBoolState(AnimatorController controller, AnimatorStateMachine sm,
                             AnimatorState loco, AnimationClip clip,
                             string boolParam, string stateName)
    {
        controller.AddParameter(boolParam, AnimatorControllerParameterType.Bool);

        var holdState    = sm.AddState(stateName);
        holdState.motion = clip;

        var toState = loco.AddTransition(holdState);
        toState.AddCondition(AnimatorConditionMode.If, 0f, boolParam);
        toState.hasExitTime = false;
        toState.duration    = 0.15f;

        var toLoco = holdState.AddTransition(loco);
        toLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, boolParam);
        toLoco.hasExitTime = false;
        toLoco.duration    = 0.15f;
    }

    // Builds a one-shot state driven by a trigger: Any State → state (on trigger),
    // then state → Locomotion once 90% of the clip has played.
    static void AddTriggeredState(AnimatorController controller, AnimatorStateMachine sm,
                                  AnimatorState loco, AnimationClip clip,
                                  string triggerParam, string stateName)
    {
        controller.AddParameter(triggerParam, AnimatorControllerParameterType.Trigger);

        var actionState    = sm.AddState(stateName);
        actionState.motion = clip;

        var toAction = sm.AddAnyStateTransition(actionState);
        toAction.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
        toAction.hasExitTime         = false;
        toAction.duration            = 0.05f;
        toAction.canTransitionToSelf = false;

        var toLoco = actionState.AddTransition(loco);
        toLoco.hasExitTime = true;
        toLoco.exitTime    = 0.9f;
        toLoco.duration    = 0.1f;
    }
}
