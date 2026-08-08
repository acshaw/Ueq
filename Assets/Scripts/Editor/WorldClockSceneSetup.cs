using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 5.12 — one-click scene wiring for the day/night + lunar cycle, mirroring how
/// <c>ZoneSetup.cs</c>'s <c>Tools/Zones/Build Zone Scenes</c> wires <c>ZoneManager</c> onto the
/// NetworkManager GameObject: idempotent, safe to re-run, no hand-edited scene YAML.
///
/// Wires: <see cref="WorldClock"/> onto the NetworkManager GO; <see cref="SunDriver"/> onto the scene's
/// Directional Light; a "WorldClockVisuals" root holding <see cref="SkyDriver"/> and a
/// <see cref="MoonRig"/> + its disc quad. Creates the two runtime shader materials as real assets
/// (idempotent find-or-create, same pattern as <c>TerrainTextureSetup.DefaultTerrainLitMaterial</c>) and
/// assigns the sky one to <c>RenderSettings.skybox</c> — the drivers themselves Instantiate() a runtime
/// copy before mutating it, so this asset stays a clean template (see SkyDriver's doc comment).
///
/// DC9 (clouds) shipped, then was cut post-implementation — the procedural puff-cluster fallback looked
/// bad in the user's first in-editor test and no owned Synty pack had a real cloud prop to use instead.
/// <see cref="RemoveAnyLeftoverCloudField"/> cleans up a "CloudField" child from an earlier run of this
/// tool if one exists, so re-running stays idempotent for anyone who already ran the cloud-era version.
/// </summary>
public static class WorldClockSceneSetup
{
    const string MaterialDir = "Assets/Materials/WorldClock";
    const string SettingsAssetPath = "Assets/Resources/WorldClockSettings.asset";

    [MenuItem("Tools/World Clock/Setup Scene")]
    public static void SetupScene()
    {
        EnsureSettingsAsset();
        WireNetworkManager();
        var skyMat  = FindOrCreateMaterial("Ueq/StylizedSky", MaterialDir, "StylizedSky.mat");
        var moonMat = FindOrCreateMaterial("Ueq/ProceduralMoon", MaterialDir, "ProceduralMoon.mat");

        WireSun();

        if (skyMat != null)
        {
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Flat;
        }

        var visuals = EnsureVisualsRoot();
        WireSky(visuals);
        WireMoon(visuals, moonMat);
        RemoveAnyLeftoverCloudField(visuals);

        EditorUtility.SetDirty(visuals);
        Debug.Log("[WorldClockSetup] Scene wired. Recompile/enter Play to verify — see the 5.12 devplan's test plan.");
    }

    static void EnsureSettingsAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<WorldClockSettings>(SettingsAssetPath) != null) return;
        Directory.CreateDirectory("Assets/Resources");
        var settings = ScriptableObject.CreateInstance<WorldClockSettings>();
        AssetDatabase.CreateAsset(settings, SettingsAssetPath);
        Debug.Log($"[WorldClockSetup] Created {SettingsAssetPath} with default day/lunar lengths — tune in the Inspector.");
    }

    static void WireNetworkManager()
    {
        var nm = Object.FindAnyObjectByType<GameNetworkManager>();
        if (nm == null) { Debug.LogWarning("[WorldClockSetup] No GameNetworkManager in the active scene — skipping."); return; }
        if (nm.GetComponent<WorldClock>() == null) nm.gameObject.AddComponent<WorldClock>();
    }

    static void WireSun()
    {
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null)
        {
            Debug.LogWarning("[WorldClockSetup] No Directional Light found in the active scene — add one, then re-run.");
            return;
        }
        if (sun.GetComponent<SunDriver>() == null) sun.gameObject.AddComponent<SunDriver>();
        EditorUtility.SetDirty(sun.gameObject);
    }

    static GameObject EnsureVisualsRoot()
    {
        var go = GameObject.Find("WorldClockVisuals");
        if (go == null) go = new GameObject("WorldClockVisuals");
        return go;
    }

    static void WireSky(GameObject visuals)
    {
        if (visuals.GetComponent<SkyDriver>() == null) visuals.AddComponent<SkyDriver>();
    }

    static void WireMoon(GameObject visuals, Material moonMat)
    {
        var rigT = visuals.transform.Find("MoonRig");
        GameObject rig = rigT != null ? rigT.gameObject : new GameObject("MoonRig");
        rig.transform.SetParent(visuals.transform, false);

        var discT = rig.transform.Find("MoonDisc");
        GameObject disc;
        if (discT != null)
        {
            disc = discT.gameObject;
        }
        else
        {
            disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "MoonDisc";
            disc.transform.SetParent(rig.transform, false);
            var col = disc.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
        if (moonMat != null) disc.GetComponent<MeshRenderer>().sharedMaterial = moonMat;

        var moonRig = rig.GetComponent<MoonRig>();
        if (moonRig == null) moonRig = rig.AddComponent<MoonRig>();

        var so = new SerializedObject(moonRig);
        so.FindProperty("disc").objectReferenceValue = disc.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>DC9 cut — see the class doc comment. Cleans up a "CloudField" child left behind by an
    /// earlier run of this tool (the CloudField component/script no longer exists, so any GameObject
    /// carrying it would otherwise sit there as a dangling reference).</summary>
    static void RemoveAnyLeftoverCloudField(GameObject visuals)
    {
        var fieldT = visuals.transform.Find("CloudField");
        if (fieldT == null) return;
        Object.DestroyImmediate(fieldT.gameObject);
        Debug.Log("[WorldClockSetup] Removed a leftover CloudField from an earlier run (clouds were cut — see the 5.12 devplan).");
    }

    static Material FindOrCreateMaterial(string shaderName, string dir, string fileName)
    {
        var sh = Shader.Find(shaderName);
        if (sh == null)
        {
            Debug.LogError($"[WorldClockSetup] Shader '{shaderName}' not found (compile error?) — skipping {fileName}.");
            return null;
        }
        Directory.CreateDirectory(dir);
        string path = $"{dir}/{fileName}";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(sh) { name = Path.GetFileNameWithoutExtension(fileName) };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != sh)
        {
            mat.shader = sh;
        }
        return mat;
    }
}
