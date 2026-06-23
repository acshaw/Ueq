using UnityEditor;

/// <summary>
/// Forces Loop Time (and Loop Pose) ON for every animation clip imported under
/// <see cref="LocomotionFolder"/>. Locomotion clips must loop, but Mixamo FBX
/// imports default Loop Time to OFF — which makes the character freeze on the
/// first frame instead of cycling. This postprocessor makes that automatic, so
/// re-downloading or re-importing a clip can never silently reintroduce the bug.
///
/// It only touches the loop flags; rig type, avatar, and everything else are
/// left exactly as configured in the import settings.
/// </summary>
public class LocomotionClipPostprocessor : AssetPostprocessor
{
    const string LocomotionFolder = "Assets/Animations/Locomotion/";

    void OnPreprocessAnimation()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(LocomotionFolder))
            return;

        var importer = (ModelImporter)assetImporter;

        // defaultClipAnimations reflects the takes Unity auto-generates from the FBX.
        // Copy, flip the loop flags, and assign back as explicit clip definitions.
        var clips = importer.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = true;
            clips[i].loopPose = true;
        }
        importer.clipAnimations = clips;
    }
}
