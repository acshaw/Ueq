using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 — replaces the flat-plane "Reshape Creslins Field" with a real Unity <b>Terrain</b> zone so hilly
/// landform scales to the 1500×1500u field without placing meshes one at a time. Generates a procedural
/// heightmap that matches the authored intent:
///   • <b>West edge</b> — a cliff-lined coastline dropping to a sea (water plane below).
///   • <b>East edge</b> — foothills rising into the feet of mountains.
///   • <b>North edge</b> — a gentle forested rise dressed with a treeline (the Thornwood entrance).
///   • <b>Interior</b> — rolling hills, with the spawn/village pad flattened so buildings sit flush.
///
/// Low-poly read is preserved by (a) keeping all your Synty props on top and (b) a matte, texture-less
/// splat (grass / rock / sand) matched to the Synty palette. A faceted low-poly TERRAIN shader can be
/// dropped onto the terrain material later for hard-edged shading; the landform + splat are shader-agnostic.
///
/// Terrain-native wins: the <b>TerrainCollider</b> gives the CharacterController correct footing for free (no
/// more "sinking into hills"), and Terrain is a first-class NavMesh source (steep cliffs/mountains exceed the
/// agent max-slope → they self-exclude as natural walls). The existing decoration tools (Scatter / Trail /
/// POI) all conform to ground via downward raycasts, so they keep working over the TerrainCollider unchanged.
///
/// Re-runnable: overwrites the generated terrain + assets + border-dressing each run. Menu:
/// <c>Tools/Zones/Build Terrain Zone</c> (and <c>Clear Terrain Zone</c>).
/// </summary>
public static class TerrainZoneSetup
{
    const string AdvEnv   = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";
    const string AssetDir = "Assets/Scenes/SampleScene/Terrain"; // generated terrain assets live beside the scene

    const string TerrainName = "ZoneTerrain";
    const string BordersName  = "ZoneBorders";
    const string GroundProxy  = "Ground"; // kept (renderer/collider off) as a bounds proxy for Scatter's whole-field mode

    // ── Field dimensions (walk 3 u/s, sprint 5 u/s → ~1500u ≈ 8 min walk / 5 min run across) ──────────
    const float FieldWidth   = 1500f; // X  (west → east)
    const float FieldLength   = 1500f; // Z  (south → north)
    const float TerrainHeight  = 220f;  // vertical span (sea floor → mountain top) — taller so mountains read as a wall
    const int   HeightRes      = 129;   // heightmap resolution (2^n + 1) — LOWER = chunkier low-poly facets
    const float SpawnFromSouth = 60f;   // south edge sits this far south of spawn

    // ── Shaping (all in normalized 0..1 terrain height) ───────────────────────────────────────────────
    const float Plateau      = 0.28f; // interior rolling-hill baseline (village pad height)
    const float HillAmp      = 0.14f; // rolling-hill amplitude (bigger = more pronounced rolling everywhere)
    const float SeaLevel     = 0.04f; // cliff base / sea floor
    const float MountainTop  = 1.00f; // east mountain peak (taller)
    const float SpawnFlatR   = 90f;   // village pad radius (flat)
    const float SpawnFlatB   = 60f;   // blend band around the pad

    [MenuItem("Tools/Zones/Build Terrain Zone")]
    public static void Build()
    {
        Vector3 spawn  = FindSpawnOrigin();
        float cornerX  = spawn.x - FieldWidth * 0.5f;
        float cornerZ  = spawn.z - SpawnFromSouth;
        // Anchor Y so the flattened village pad lands at world y ≈ 0 (matches the existing spawn height).
        float baseY    = -Plateau * TerrainHeight;
        Vector3 corner = new(cornerX, baseY, cornerZ);
        Vector2 spawnXZ = new(spawn.x, spawn.z);

        ClearGenerated();
        Directory.CreateDirectory(AssetDir);

        // ── TerrainData ───────────────────────────────────────────────────────────────────────────────
        // Order matters: set heightmapResolution FIRST (its setter can reset size), then size. Read the
        // resolution BACK to size the heights array so SetHeights fills the WHOLE map — a size mismatch
        // fills only a corner and leaves the rest flat at 0 (→ flat terrain + all-"low-ground" dirt splat).
        var data = new TerrainData();
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(FieldWidth, TerrainHeight, FieldLength);

        int res = data.heightmapResolution;
        var heights = new float[res, res]; // heights[z, x] in 0..1
        for (int zi = 0; zi < res; zi++)
        {
            float v = zi / (float)(res - 1);
            float worldZ = cornerZ + v * FieldLength;
            for (int xi = 0; xi < res; xi++)
            {
                float u = xi / (float)(res - 1);
                float worldX = cornerX + u * FieldWidth;

                float h = ShapeHeight(u, v);

                // Village pad: flatten to plateau near the spawn so buildings sit flush.
                float d = Vector2.Distance(new Vector2(worldX, worldZ), spawnXZ);
                float flat = SStep(SpawnFlatR + SpawnFlatB, SpawnFlatR, d);
                h = Mathf.Lerp(h, Plateau, flat);

                heights[zi, xi] = Mathf.Clamp(h, 0.002f, 0.998f);
            }
        }
        data.SetHeights(0, 0, heights);
        data.size = new Vector3(FieldWidth, TerrainHeight, FieldLength); // re-assert the vertical scale
        Debug.Log($"[TerrainZone] heightmapRes={res}, size={data.size}, " +
                  $"center height={data.GetInterpolatedHeight(0.5f, 0.5f):F1} (expect ~{Plateau * TerrainHeight:F0}).");

        // Splat layers + textures are applied by TerrainTextureSetup after the GameObject exists (below).

        // ── Terrain GameObject ──────────────────────────────────────────────────────────────────────────
        AssetDatabase.CreateAsset(data, $"{AssetDir}/CreslinsTerrainData.asset"); // persist after fully built
        var terrainGo = Terrain.CreateTerrainGameObject(data);
        terrainGo.name = TerrainName;
        terrainGo.transform.position = corner;
        var terrainComp = terrainGo.GetComponent<Terrain>();
        // Synty Nature Biomes ground textures on the default URP Terrain-Lit material + height/slope auto splat.
        TerrainTextureSetup.ApplyToTerrain(terrainComp);
        AddNavMeshSurface(terrainGo);

        // ── Ground bounds-proxy (renderer + collider off) so Scatter's whole-field mode still works ──────
        EnsureGroundProxy(new Vector3(spawn.x, 0f, cornerZ + FieldLength * 0.5f));

        // ── Place the networking anchors onto the new surface ───────────────────────────────────────────
        var terrain = terrainGo.GetComponent<Terrain>();
        SnapSpawnToSurface(terrain, corner, spawn);
        MovePortalNorth(terrain, corner, spawn);

        // ── Border dressing (west water + cliff rocks, east foothill rocks, north treeline) ─────────────
        BuildBorders(terrain, corner, spawn);

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrainGo;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TerrainZone] Built {FieldWidth}x{FieldLength}u terrain zone around spawn {spawn}. " +
                  "NEXT: (1) delete any leftover flat 'CreslinsField'/'SyntyTerrain' roots + hand-placed hills; " +
                  "(2) run Tools/Zones/Build Trellis Starter Hub (village drops on the flat pad), then Scatter " +
                  "Props for interior vegetation + Build Path Along Children for trails (all conform to the " +
                  "terrain); (3) rebake the navmesh (Tools/Terrain/Rebake NavMesh) — cliffs/mountains self-exclude " +
                  "as walls; (4) SAVE. Surface = Synty ground textures on the default Terrain-Lit material; " +
                  "tune per-layer tileSize on the .terrainlayer assets for facet scale.");
    }

    [MenuItem("Tools/Zones/Clear Terrain Zone")]
    public static void ClearGenerated()
    {
        foreach (var n in new[] { TerrainName, BordersName })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ── Heightmap shaping ─────────────────────────────────────────────────────────────────────────────
    static float ShapeHeight(float u, float v)
    {
        // Rolling hills everywhere — layered broad + medium + fine octaves for varied, pronounced rolling.
        float broad = Fbm(u, v, 3f,  3);
        float mid   = Fbm(u, v, 6f,  4);
        float fine  = Fbm(u, v, 12f, 3);
        float hills = broad * 0.45f + mid * 0.35f + fine * 0.20f;
        float h = Plateau + (hills - 0.5f) * 2f * HillAmp;

        // East foothills → mountains: a steeper, taller wall — narrow band near the edge, high exponent.
        float east = SStep(0.72f, 1f, u);
        float rugged = Fbm(u, v, 11f, 4);
        h = Mathf.Lerp(h, MountainTop * 0.55f + rugged * 0.45f * MountainTop, Mathf.Pow(east, 2.6f));

        // West cliff → sea: steep, back-loaded descent in the outer band.
        float west = SStep(0.10f, 0f, u);
        h = Mathf.Lerp(h, SeaLevel, Mathf.Pow(west, 2.2f));

        // North: gentle forested rise (kept walkable; the treeline props are the real wall).
        float north = SStep(0.82f, 1f, v);
        h += north * 0.05f;

        return h;
    }

    // Fractal Perlin noise in [0,1]. `features` = cycles across the terrain.
    static float Fbm(float u, float v, float features, int octaves)
    {
        float sum = 0f, amp = 1f, freq = features, norm = 0f;
        const float seed = 137.31f;
        for (int i = 0; i < octaves; i++)
        {
            sum  += amp * Mathf.PerlinNoise(u * freq + seed, v * freq + seed);
            norm += amp;
            amp  *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    // GLSL-style smoothstep (handles e0 > e1 for descending ramps).
    static float SStep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    // ── Border dressing ───────────────────────────────────────────────────────────────────────────────
    static void BuildBorders(Terrain terrain, Vector3 corner, Vector3 spawn)
    {
        var root = new GameObject(BordersName).transform;
        var rng  = new System.Random(9901);

        // West coastline: a water plane sitting just above the sea floor across the western low band.
        var water = AssetDatabase.LoadAssetAtPath<GameObject>(AdvEnv + "SM_Env_Water_01.prefab");
        if (water != null)
        {
            var w = (GameObject)PrefabUtility.InstantiatePrefab(water, root);
            MeasureFootprint(w, out var size, out _);
            float coverX = FieldWidth * 0.14f, coverZ = FieldLength;
            w.transform.localScale = new Vector3(
                size.x > 0.01f ? coverX / size.x : 2f, 1f, size.z > 0.01f ? coverZ / size.z : 2f);
            float seaY = corner.y + (SeaLevel + 0.02f) * TerrainHeight;
            w.transform.position = new Vector3(corner.x + FieldWidth * 0.05f, seaY, corner.z + FieldLength * 0.5f);
            StripColliders(w);
        }

        var trees = LoadAll(AdvEnv,
            "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05",
            "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03");
        var rocks = LoadAll(AdvEnv, "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_05");

        // North treeline (two staggered rows), with a gap around the portal corridor at x ≈ spawn.x.
        if (trees.Count > 0)
        {
            var line = new GameObject("NorthTreeline").transform; line.SetParent(root, false);
            for (int rowi = 0; rowi < 2; rowi++)
            {
                float v = 0.9f + rowi * 0.03f;
                for (float x = -FieldWidth * 0.5f + 20f; x <= FieldWidth * 0.5f - 20f; x += 16f)
                {
                    float wx = spawn.x + x + rowi * 8f;
                    float wz = corner.z + v * FieldLength;
                    if (Mathf.Abs(wx - spawn.x) < 25f) continue; // keep the way north open
                    PlaceOnSurface(line, terrain, corner, trees[rng.Next(trees.Count)], wx, wz, rng, 0.2f, true);
                }
            }
        }

        // East foothill rocks + a few west cliff-top rocks accent the boundaries.
        if (rocks.Count > 0)
        {
            var stone = new GameObject("BoundaryRocks").transform; stone.SetParent(root, false);
            for (float z = -FieldLength * 0.5f + 30f; z <= FieldLength * 0.5f - 30f; z += 34f)
            {
                float wz = spawn.z - SpawnFromSouth + FieldLength * 0.5f + z;
                // east foothills (a couple of depths)
                PlaceOnSurface(stone, terrain, corner, rocks[rng.Next(rocks.Count)], corner.x + FieldWidth * 0.90f, wz, rng, 0.35f, true);
                if (rng.Next(2) == 0)
                    PlaceOnSurface(stone, terrain, corner, rocks[rng.Next(rocks.Count)], corner.x + FieldWidth * 0.84f, wz, rng, 0.35f, true);
                // west cliff top
                if (rng.Next(3) == 0)
                    PlaceOnSurface(stone, terrain, corner, rocks[rng.Next(rocks.Count)], corner.x + FieldWidth * 0.13f, wz, rng, 0.3f, true);
            }
        }
    }

    static void PlaceOnSurface(Transform parent, Terrain terrain, Vector3 corner, GameObject prefab,
                               float worldX, float worldZ, System.Random rng, float scaleVar, bool keepCollider)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float y = SurfaceY(terrain, corner, worldX, worldZ);
        go.transform.position = new Vector3(worldX + (float)(rng.NextDouble() * 3 - 1.5), y, worldZ + (float)(rng.NextDouble() * 3 - 1.5));
        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
        go.transform.localScale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleVar;
        if (!keepCollider) StripColliders(go);
    }

    // ── Networking anchors ────────────────────────────────────────────────────────────────────────────
    static void SnapSpawnToSurface(Terrain terrain, Vector3 corner, Vector3 spawn)
    {
        var start = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        if (start == null) return;
        float y = SurfaceY(terrain, corner, spawn.x, spawn.z) + 1f; // slight lift; gravity settles the CharacterController
        start.transform.position = new Vector3(spawn.x, y, spawn.z);
    }

    static void MovePortalNorth(Terrain terrain, Vector3 corner, Vector3 spawn)
    {
        ZonePortal portal = null;
        foreach (var p in Object.FindObjectsByType<ZonePortal>(FindObjectsSortMode.None))
            if (p.targetZoneId == "thornwood") { portal = p; break; }
        if (portal == null)
        {
            Debug.LogWarning("[TerrainZone] No ZonePortal→thornwood found — place one near the north edge by hand.");
            return;
        }

        Vector3 oldPos = portal.transform.position;
        float pz = corner.z + FieldLength - 40f; // just inside the north edge
        Vector3 newPos = new(spawn.x, SurfaceY(terrain, corner, spawn.x, pz) + 0.1f, pz);
        portal.transform.position = newPos;

        ZoneEntry ret = null; float best = float.MaxValue;
        foreach (var e in Object.FindObjectsByType<ZoneEntry>(FindObjectsSortMode.None))
        {
            if (e.entryId == "default") continue;
            float d = (e.transform.position - oldPos).sqrMagnitude;
            if (d < best) { best = d; ret = e; }
        }
        if (ret != null)
        {
            Vector3 rp = newPos + new Vector3(0f, 0f, -6f);
            ret.transform.position = new Vector3(rp.x, SurfaceY(terrain, corner, rp.x, rp.z) + 0.1f, rp.z);
        }
        Debug.Log($"[TerrainZone] Moved Thornwood portal to {newPos}" + (ret != null ? $" + return entry '{ret.entryId}'." : "."));
    }

    static float SurfaceY(Terrain terrain, Vector3 corner, float worldX, float worldZ)
    {
        float u = Mathf.Clamp01((worldX - corner.x) / FieldWidth);
        float v = Mathf.Clamp01((worldZ - corner.z) / FieldLength);
        return corner.y + terrain.terrainData.GetInterpolatedHeight(u, v);
    }

    // ── Ground bounds-proxy (no renderer, no collider — Terrain owns collision + raycasts) ─────────────
    static void EnsureGroundProxy(Vector3 center)
    {
        var ground = GameObject.Find(GroundProxy);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundProxy;
        }
        ground.transform.position   = new Vector3(center.x, 0f, center.z);
        ground.transform.rotation   = Quaternion.identity;
        ground.transform.localScale = new Vector3(FieldWidth / 10f, 1f, FieldLength / 10f); // plane = 10u @ scale 1

        var mr = ground.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;                                // hidden — terrain renders the ground
        foreach (var c in ground.GetComponents<Collider>()) Object.DestroyImmediate(c); // don't intercept raycasts

        // Retire the flat-plane NavMeshSurface if present — the terrain now owns the bake.
        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType != null)
        {
            var s = ground.GetComponent(surfaceType);
            if (s != null) Object.DestroyImmediate(s);
        }
    }

    static void AddNavMeshSurface(GameObject go)
    {
        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType == null)
        {
            Debug.LogWarning("[TerrainZone] AI Navigation package missing — add a NavMeshSurface to the terrain + bake.");
            return;
        }
        var surface = go.GetComponent(surfaceType) ?? go.AddComponent(surfaceType);
        var so = new SerializedObject(surface);
        SetEnum(so, "m_UseGeometry", "PhysicsColliders"); // TerrainCollider + prop colliders (the Ground proxy has none)
        SetEnum(so, "m_CollectObjects", "All");           // whole scene: terrain + Synty props (which carve/exclude)
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetEnum(SerializedObject so, string prop, string name)
    {
        var p = so.FindProperty(prop);
        if (p == null) return;
        int idx = System.Array.IndexOf(p.enumNames, name);
        if (idx >= 0) p.enumValueIndex = idx;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────────
    static Vector3 FindSpawnOrigin()
    {
        var start = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        Vector3 p = start != null ? start.transform.position : new Vector3(0f, 0f, -5f);
        return new Vector3(p.x, 0f, p.z);
    }

    static System.Collections.Generic.List<GameObject> LoadAll(string folder, params string[] names)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(folder + n + ".prefab");
            if (p != null) list.Add(p);
        }
        return list;
    }

    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    static void MeasureFootprint(GameObject go, out Vector3 size, out Vector3 center)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { size = new Vector3(4, 0, 4); center = Vector3.zero; return; }
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        size = b.size; center = b.center;
    }

    static System.Type FindType(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
