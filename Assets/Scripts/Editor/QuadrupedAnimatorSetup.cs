using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds a minimal Speed-blend AnimatorController per quadruped, mirroring <see cref="PlayerAnimatorSetup"/>'s
/// recipe. Quadrupeds can't retarget the shared Humanoid <c>PlayerLocomotion</c> controller (they're imported as
/// Generic rigs — no biped skeleton to retarget onto), so each species needs its own controller built from its
/// own clips. The result plugs into the existing non-Humanoid seam: <see cref="MobModelCatalog.Entry.animatorController"/>.
///
/// Clips are matched by the ANIMATION CLIP'S OWN NAME first (falls back to the source file name) — needed
/// because some packs (e.g. Wolf) embed every clip as a sub-asset of one shared FBX, where the file name alone
/// can't tell "Idle" from "Walking" apart.
///
/// Usage: Tools/Character/Build Quadruped Locomotion Controllers. Re-run after adding a new species below,
/// or after re-importing a pack's clips (rebuilds fresh each time — safe to re-run).
/// </summary>
public static class QuadrupedAnimatorSetup
{
    const string SpeedParam  = "Speed";
    const string AttackParam = "Attack";

    class Species
    {
        public string Name;
        public string ClipSource;  // a folder (one clip per FBX) or a single FBX (clips embedded as sub-assets)
        public string OutputPath;
        public float  WalkSpeed;
        public float  RunSpeed;
    }

    // Add a new quadruped here — no new code needed elsewhere.
    static readonly Species[] AllSpecies =
    {
        new Species
        {
            Name       = "Bear",
            ClipSource = "Assets/Blink/Art/Animals/Stylized/Bear/Bear_Animations",
            OutputPath = "Assets/Animations/BearLocomotion.controller",
            WalkSpeed  = 2f,
            RunSpeed   = 4.5f,
        },
        new Species
        {
            Name       = "Wolf",
            ClipSource = "Assets/Wolf/Models/Wolf.fbx",
            OutputPath = "Assets/Animations/WolfLocomotion.controller",
            WalkSpeed  = 2.5f,
            RunSpeed   = 6f,
        },
    };

    [MenuItem("Tools/Character/Build Quadruped Locomotion Controllers")]
    public static void BuildAll()
    {
        foreach (var species in AllSpecies) Build(species);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void Build(Species species)
    {
        var entries = CollectClips(species.ClipSource);
        if (entries.Count == 0)
        {
            Debug.LogError($"[QuadrupedAnimatorSetup] {species.Name}: no AnimationClips found at " +
                            $"'{species.ClipSource}'. Check the path and that the FBX imported cleanly.");
            return;
        }

        AnimationClip Match(Func<string, bool> pred) => entries.FirstOrDefault(e => pred(e.key)).clip;

        var idle   = Match(k => k.Contains("idle") && !k.Contains("combat"));
        var walk   = Match(k => k.Contains("walk") && !k.Contains("back"));
        var run    = Match(k => (k.Contains("run") || k.Contains("jog")) && !k.Contains("back"));
        var attack = Match(k => k.Contains("attack") || k.Contains("bite"));

        if (idle == null) Debug.LogWarning($"[QuadrupedAnimatorSetup] {species.Name}: no 'idle' clip found — leaving idle node empty.");
        if (walk == null) Debug.LogWarning($"[QuadrupedAnimatorSetup] {species.Name}: no 'walk' clip found — leaving walk node empty.");
        if (run  == null) Debug.LogWarning($"[QuadrupedAnimatorSetup] {species.Name}: no 'run' clip found — leaving run node empty.");
        if (attack == null) Debug.LogWarning($"[QuadrupedAnimatorSetup] {species.Name}: no 'attack/bite' clip found — skipping Attack state (nothing currently triggers it — mob auto-attack has no anim hook yet).");

        var dir = Path.GetDirectoryName(species.OutputPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(dir), Path.GetFileName(dir));

        var controller = AnimatorController.CreateAnimatorControllerAtPath(species.OutputPath);
        controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);

        var blendTree = new BlendTree
        {
            name                   = "Locomotion",
            blendType              = BlendTreeType.Simple1D,
            blendParameter         = SpeedParam,
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(blendTree, controller);

        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, species.WalkSpeed);
        blendTree.AddChild(run,  species.RunSpeed);

        var sm    = controller.layers[0].stateMachine;
        var state = sm.AddState("Locomotion");
        state.motion    = blendTree;
        sm.defaultState = state;

        if (attack != null) AddTriggeredState(controller, sm, state, attack, AttackParam, "Attack");

        EditorUtility.SetDirty(controller);
        EditorGUIUtility.PingObject(controller);
        Debug.Log($"[QuadrupedAnimatorSetup] Built {species.OutputPath} — idle:{(idle ? idle.name : "MISSING")} " +
                  $"walk:{(walk ? walk.name : "MISSING")} run:{(run ? run.name : "MISSING")} " +
                  $"attack:{(attack ? attack.name : "MISSING")}. Set this as the animatorController on this " +
                  $"species' MobModelCatalog entry (non-Humanoid override).");
    }

    // A folder = one clip per FBX (e.g. Bear's Bear_Animations/); a single asset path = clips embedded as
    // sub-assets of one shared FBX (e.g. Wolf.fbx). Match key prefers the clip's own name (distinguishes
    // sub-assets sharing one file) and falls back to the source file name.
    static List<(AnimationClip clip, string key)> CollectClips(string clipSource)
    {
        var entries = new List<(AnimationClip, string)>();

        IEnumerable<string> assetPaths = AssetDatabase.IsValidFolder(clipSource)
            ? AssetDatabase.FindAssets(string.Empty, new[] { clipSource }).Select(AssetDatabase.GUIDToAssetPath)
            : new[] { clipSource };

        foreach (var path in assetPaths)
        {
            var fileKey = Path.GetFileNameWithoutExtension(path).ToLower();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is not AnimationClip c || c.name.StartsWith("__preview")) continue;
                var key = string.IsNullOrEmpty(c.name) ? fileKey : c.name.ToLower();
                entries.Add((c, key));
            }
        }

        return entries;
    }

    // One-shot state driven by a trigger: Any State → state (on trigger), then state → Locomotion once 90%
    // of the clip has played. Mirrors PlayerAnimatorSetup.AddTriggeredState.
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
