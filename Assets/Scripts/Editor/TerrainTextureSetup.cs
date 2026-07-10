using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 — textures the ZoneTerrain with Synty's <b>POLYGON Nature Biomes</b> (Meadow/Forest) authored
/// TerrainLayers and switches it to Unity's default URP <b>Terrain-Lit</b> material so the layers actually
/// render and can be hand-painted with the built-in Paint Texture brush. Replaces the old solid-color
/// faceted <c>Ueq/LowPolyTerrain</c> surface — the Synty ground textures are themselves stylized/low-poly,
/// and the low-poly read still comes from the chunky hill geometry + props on top.
///
/// The base layer set (paintable in the Terrain inspector):
///   0 Grass · 1 Grass+Flowers · 2 Dirt · 3 Rock · 4 Footpath tile · 5 Mud
/// The auto splat lays grass everywhere, rock on steep faces + mountain tops, dirt in the low coast band;
/// flowers/footpath/mud start empty so you paint (or use <c>Tools/Zones/Paint Path Along Children</c>) them in.
///
/// Menu: <c>Tools/Zones/Apply Synty Terrain Textures</c> — runs on the EXISTING terrain (no heightmap rebuild,
/// props untouched). <see cref="TerrainZoneSetup"/>'s Build calls the same code so a fresh build matches.
/// </summary>
public static class TerrainTextureSetup
{
    const string PnbTerrain = "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Terrain/";
    const string AssetDir   = "Assets/Scenes/SampleScene/Terrain"; // generated material lives beside the scene
    const int    AlphaRes   = 256;                                 // splatmap resolution

    // Curated layer palette — ORDER defines the splat channel indices used below. Grass_01 is the base.
    // Each entry: (source terrainlayer, tileSize world-units/repeat). Synty authors these at 3–4u which reads
    // busy across a 1500u field, so we copy them into the scene's Terrain folder at a larger tiling (paths +
    // flowers stay tighter). Tune here + re-run Apply Synty Terrain Textures — the shared pack assets are
    // never modified.
    static readonly (string file, float tiling)[] LayerSpecs =
    {
        ("Terrain_Meadow_Grass_01",         14f), // 0 base grass
        ("Terrain_Meadow_Grass_Flowers_01", 10f), // 1 meadow flowers (hand-paint accents)
        ("Terrain_Meadow_Dirt_01",          12f), // 2 low ground / coast
        ("Terrain_Meadow_Rock_01",          12f), // 3 slopes / mountain tops
        ("Terrain_Meadow_Footpath_Tile_01",  4f), // 4 trails (Paint Path tool) — tighter reads as a path
        ("Terrain_Meadow_Mud_01",           10f), // 5 wet spots (hand-paint)
    };
    const int Grass = 0, Dirt = 2, Rock = 3;

    [MenuItem("Tools/Zones/Apply Synty Terrain Textures")]
    public static void Apply()
    {
        var terrain = FindTerrain();
        if (terrain == null)
        {
            Debug.LogWarning("[TerrainTex] No Terrain found — run Tools/Zones/Build Terrain Zone first.");
            return;
        }
        if (!ApplyToTerrain(terrain)) return;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrain.gameObject;
        Debug.Log("[TerrainTex] Applied Synty Meadow terrain layers + Terrain-Lit material to '" + terrain.name +
                  "'. Base splat: grass / rock on slopes+peaks / dirt on the coast. NEXT: hand-paint with the " +
                  "Terrain inspector's Paint Texture tab, or run Tools/Zones/Paint Path Along Children for trails. " +
                  "Tiling is enlarged via scene-local layer copies in " + AssetDir + " — adjust the LayerSpecs " +
                  "tiling values + re-run this if the ground reads too busy or too stretched.");
    }

    /// <summary>Assign the Synty layers + default Terrain-Lit material + regenerate the auto splat. Returns false if the layers are missing.</summary>
    public static bool ApplyToTerrain(Terrain terrain)
    {
        var layers = LoadLayers();
        if (layers == null)
        {
            Debug.LogError("[TerrainTex] Missing Nature Biomes terrain layers under " + PnbTerrain +
                           " — is the POLYGON Nature Biomes (Meadow/Forest) pack imported?");
            return false;
        }

        var td = terrain.terrainData;
        td.terrainLayers = layers;
        terrain.materialTemplate = DefaultTerrainLitMaterial();
        terrain.drawInstanced = true; // standard terrain rendering (custom faceted shader path is retired)
        RegenerateSplat(terrain);
        return true;
    }

    /// <summary>Repaint the height/slope base splat (grass / rock / dirt) — wipes any hand-painting. Public so the Path Painter can offer a "reset base" button.</summary>
    public static void RegenerateSplat(Terrain terrain)
    {
        var td = terrain.terrainData;
        int layerCount = td.terrainLayers.Length;
        if (layerCount == 0) return;

        td.alphamapResolution = AlphaRes;
        int res = td.alphamapResolution;
        float ty = td.size.y;
        var maps = new float[res, res, layerCount];

        for (int az = 0; az < res; az++)
        {
            float v = az / (float)(res - 1);
            for (int ax = 0; ax < res; ax++)
            {
                float u = ax / (float)(res - 1);
                float steep = td.GetSteepness(u, v);              // degrees
                float hh    = td.GetInterpolatedHeight(u, v) / ty; // 0..1

                float wRock = Mathf.Clamp01((steep - 15f) / 22f);              // steep faces → rock (~15°+)
                wRock = Mathf.Max(wRock, Mathf.Clamp01((hh - 0.44f) / 0.22f)); // high ground → rock (mountains)
                float wDirt = Mathf.Clamp01((0.17f - hh) / 0.10f);            // low ground → dirt
                wDirt = Mathf.Max(wDirt, SStep(0.08f, 0f, u));                // far-west beach band
                float wGrass = Mathf.Max(0f, 1f - wRock - wDirt);

                float s = wGrass + wRock + wDirt;
                if (s < 1e-4f) { wGrass = 1f; s = 1f; }
                maps[az, ax, Grass] = wGrass / s;
                if (Dirt < layerCount) maps[az, ax, Dirt] = wDirt / s;
                if (Rock < layerCount) maps[az, ax, Rock] = wRock / s;
                // Flowers / footpath / mud channels stay 0 — painted in by hand or the path tool.
            }
        }
        td.SetAlphamaps(0, 0, maps);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────
    static TerrainLayer[] LoadLayers()
    {
        Directory.CreateDirectory(AssetDir);
        var layers = new TerrainLayer[LayerSpecs.Length];
        for (int i = 0; i < LayerSpecs.Length; i++)
        {
            layers[i] = LoadLocalLayer(LayerSpecs[i].file, LayerSpecs[i].tiling);
            if (layers[i] == null)
            {
                Debug.LogError("[TerrainTex] Missing terrain layer: " + LayerSpecs[i].file);
                return null;
            }
        }
        return layers;
    }

    // Get-or-create a scene-local copy of a Synty terrain layer at our tiling — keeps the vendored pack asset
    // pristine and lets us tune tileSize without editing it. Re-runnable: refreshes the tiling each Apply.
    static TerrainLayer LoadLocalLayer(string file, float tiling)
    {
        string dstPath = AssetDir + "/" + file + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dstPath);
        if (layer == null)
        {
            string srcPath = PnbTerrain + file + ".terrainlayer";
            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(srcPath) == null) return null;
            if (!AssetDatabase.CopyAsset(srcPath, dstPath)) return null;
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dstPath);
        }
        if (layer != null)
        {
            layer.tileSize = new Vector2(tiling, tiling);
            EditorUtility.SetDirty(layer);
        }
        return layer;
    }

    static Material DefaultTerrainLitMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (sh == null)
        {
            Debug.LogWarning("[TerrainTex] URP Terrain/Lit shader not found — leaving the terrain's default material.");
            return null;
        }
        Directory.CreateDirectory(AssetDir);
        string path = AssetDir + "/SyntyTerrainLit.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(sh) { name = "SyntyTerrainLit" };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != sh)
        {
            mat.shader = sh;
        }
        return mat;
    }

    static Terrain FindTerrain()
    {
        var go = GameObject.Find("ZoneTerrain");
        return go != null ? go.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
    }

    // GLSL-style smoothstep (handles e0 > e1 for descending ramps).
    static float SStep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }
}
