using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TerrainShapingUtil;

/// <summary>
/// 3.5 — shapes Thornwood (the second real zone) into a real forest terrain matching the four-boundary
/// geography in <c>Assets/Scripts/trellis_zone_design.md</c> ("Zone 2: Thornwood"): a steep, impassable
/// mountain wall on the east (carved by a walkable saddle for the Grukmar's Deep entrance, "mid-north
/// position"), sheer coastal cliffs to the sea on the west (no beach), a gentle down-slope treeline on the
/// south (the Creslin's Field transition), and a dense, hard-walled forest edge on the north (no exit).
///
/// A sibling to <see cref="TerrainZoneSetup"/> (Creslin's Field), not a generalized replacement — the two
/// zones' shaping is genuinely different (the east-wall notch has no Creslin's-Field equivalent) and there
/// are only two outdoor zones total, so a shared parameterized shaper would be speculative. Shared,
/// already-proven-correct primitives live in <see cref="TerrainShapingUtil"/>.
///
/// Anchors off the zone's own <see cref="ZoneEntry"/> "default" marker (Thornwood has no
/// <c>NetworkStartPosition</c>) — open <c>Assets/Scenes/Zones/thornwood.unity</c> directly before running
/// this; never re-run <c>Tools/Zones/Build Zone Scenes</c> afterward, it rebuilds the scene from scratch.
/// Re-runnable: clears + rebuilds its own generated objects each time.
/// </summary>
public static class ThornwoodTerrainSetup
{
    const string AdvEnv   = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";
    const string AssetDir = "Assets/Scenes/Zones/ThornwoodTerrain"; // generated terrain assets (own dir — never shares Creslin's Field's)
    const string NavMeshPath = "Assets/Scenes/Zones/thornwood_navmesh.asset"; // same path 3.0.2 used for the flat scaffold — this replaces it

    const string TerrainName  = "ThornwoodTerrain";
    const string BordersName  = "ThornwoodBorders";
    const string LegacyGround = "thornwood_Ground"; // the 3.0.2 flat-plane scaffold this build replaces

    // ── Field dimensions ("dense forest zone", smaller + narrower than Creslin's field per the doc) ──────
    const float FieldWidth    = 700f;  // X (west cliff → east mountain)
    const float FieldLength   = 820f;  // Z (south treeline → north deep-forest edge)
    const float TerrainHeight = 200f;
    const int   HeightRes     = 65;    // chunkier low-poly facets suit a dense, darker forest floor
    const float SpawnFromSouth = 40f;  // south edge sits this far south of the "default" arrival entry

    // ── Shaping (normalized 0..1 terrain height) ───────────────────────────────────────────────────────
    const float Plateau     = 0.30f;
    const float HillAmp     = 0.09f;
    const float SeaLevel    = 0.04f;
    const float MountainTop = 0.95f;
    const float ArrivalFlatR = 45f;  // small flat clearing around the "default" arrival entry
    const float ArrivalFlatB = 35f;

    // East-wall notch (TW4 — the Grukmar's Deep entrance, "carved into the mountain face, mid-north"):
    const float NotchCenterV  = 0.66f; // normalized Z — mid-north
    const float NotchHalfWidth = 0.07f;
    const float NotchSaddle    = 0.34f; // normalized height of the walkable saddle carved into the wall

    [MenuItem("Tools/Zones/Build Thornwood Terrain")]
    public static void Build()
    {
        Vector3 origin = FindDefaultEntryOrigin();
        float cornerX = origin.x - FieldWidth * 0.5f;
        float cornerZ = origin.z - SpawnFromSouth;
        float baseY   = -Plateau * TerrainHeight; // anchor so the arrival clearing lands near the entry's authored Y
        Vector3 corner = new(cornerX, baseY, cornerZ);
        Vector2 originXZ = new(origin.x, origin.z);

        ClearGenerated();
        Directory.CreateDirectory(AssetDir);

        // ── Heightmap ─────────────────────────────────────────────────────────────────────────────────
        var data = new TerrainData();
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(FieldWidth, TerrainHeight, FieldLength);

        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
        {
            float v = zi / (float)(res - 1);
            float worldZ = cornerZ + v * FieldLength;
            for (int xi = 0; xi < res; xi++)
            {
                float u = xi / (float)(res - 1);
                float worldX = cornerX + u * FieldWidth;

                float h = ShapeHeight(u, v);

                float d = Vector2.Distance(new Vector2(worldX, worldZ), originXZ);
                float flat = SStep(ArrivalFlatR + ArrivalFlatB, ArrivalFlatR, d);
                h = Mathf.Lerp(h, Plateau, flat);

                heights[zi, xi] = Mathf.Clamp(h, 0.002f, 0.998f);
            }
        }
        data.SetHeights(0, 0, heights);
        data.size = new Vector3(FieldWidth, TerrainHeight, FieldLength); // re-assert after SetHeights
        Debug.Log($"[ThornwoodTerrain] heightmapRes={res}, size={data.size}, " +
                  $"arrival height={data.GetInterpolatedHeight(0.5f, (SpawnFromSouth) / FieldLength):F1} (expect ~{Plateau * TerrainHeight:F0}).");

        AssetDatabase.CreateAsset(data, $"{AssetDir}/ThornwoodTerrainData.asset");
        var terrainGo = Terrain.CreateTerrainGameObject(data);
        terrainGo.name = TerrainName;
        terrainGo.transform.position = corner;
        var terrain = terrainGo.GetComponent<Terrain>();
        TerrainTextureSetup.ApplyToTerrain(terrain, AssetDir);

        RemoveLegacyGround();

        RepositionMarkers(terrain, corner);
        BuildBorders(terrain, corner, origin);

        // Bake + persist AFTER borders (props carve the navmesh too) — Thornwood is additively loaded, so
        // the bake must be saved as an asset or the zone reloads with no navmesh (3.0.1).
        BakeAndPersistTerrainNavMesh(terrainGo, NavMeshPath);

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrainGo;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[ThornwoodTerrain] Built. NEXT: Tools/Zones/Add Synty Trees to Terrain then Paint Trees for " +
                  "canopy density; Tools/Zones/Build Thornwood Encounters for population; SAVE the scene.");
    }

    [MenuItem("Tools/Zones/Clear Thornwood Terrain")]
    public static void ClearGenerated()
    {
        foreach (var n in new[] { TerrainName, BordersName })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static void RemoveLegacyGround()
    {
        var go = GameObject.Find(LegacyGround);
        if (go != null)
        {
            Object.DestroyImmediate(go); // its flat-plane NavMeshSurface goes with it — the terrain owns the bake now
            Debug.Log($"[ThornwoodTerrain] Removed the 3.0.2 flat scaffold '{LegacyGround}'.");
        }
    }

    // ── Heightmap shaping ─────────────────────────────────────────────────────────────────────────────
    static float ShapeHeight(float u, float v)
    {
        float broad = Fbm(u, v, 3f, 3, 881f);
        float mid   = Fbm(u, v, 6f, 4, 881f);
        float fine  = Fbm(u, v, 12f, 3, 881f);
        float hills = broad * 0.5f + mid * 0.3f + fine * 0.2f;
        float h = Plateau + (hills - 0.5f) * 2f * HillAmp;

        // East: impassable mountain wall — carved by a walkable saddle mid-north for the Grukmar entrance.
        float east = SStep(0.74f, 1f, u);
        float rugged = Fbm(u, v, 10f, 4, 205f);
        float wallHeight = MountainTop * 0.55f + rugged * 0.45f * MountainTop;
        float notch = Mathf.Clamp01(1f - Mathf.Abs(v - NotchCenterV) / NotchHalfWidth);
        float wallTarget = Mathf.Lerp(wallHeight, NotchSaddle, notch);
        h = Mathf.Lerp(h, wallTarget, Mathf.Pow(east, 2.4f));

        // West: sheer cliff to the sea — no beach.
        float west = SStep(0.09f, 0f, u);
        h = Mathf.Lerp(h, SeaLevel, Mathf.Pow(west, 2.4f));

        // North: deep forest rising to a hard zone edge (no exit — no gap needed).
        float north = SStep(0.85f, 1f, v);
        h += north * 0.05f;

        return h;
    }

    // ── Border dressing (south treeline gap at the Creslin's portal, hard north wall, east/west accents) ──
    static void BuildBorders(Terrain terrain, Vector3 corner, Vector3 origin)
    {
        var root = new GameObject(BordersName).transform;
        var rng  = new System.Random(51015);

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

        // South treeline (dense — "canopy blocks light"), gap only around the Creslin's portal corridor.
        if (trees.Count > 0)
        {
            var line = new GameObject("SouthTreeline").transform; line.SetParent(root, false);
            for (int rowi = 0; rowi < 2; rowi++)
            {
                float v = 0.06f + rowi * 0.025f;
                for (float x = -FieldWidth * 0.5f + 15f; x <= FieldWidth * 0.5f - 15f; x += 12f)
                {
                    float wx = origin.x + x + rowi * 6f;
                    float wz = corner.z + v * FieldLength;
                    if (Mathf.Abs(wx - origin.x) < 22f) continue; // keep the way south (to Creslin's Field) open
                    PlaceOnSurface(line, terrain, corner, trees[rng.Next(trees.Count)], wx, wz, rng, 0.2f, true);
                }
            }

            // North: a hard wall — dense, no gap (the doc gives this edge no exit).
            var north = new GameObject("NorthTreeline").transform; north.SetParent(root, false);
            for (int rowi = 0; rowi < 3; rowi++)
            {
                float v = 0.92f + rowi * 0.02f;
                for (float x = -FieldWidth * 0.5f + 15f; x <= FieldWidth * 0.5f - 15f; x += 11f)
                {
                    float wx = origin.x + x + rowi * 5f;
                    float wz = corner.z + v * FieldLength;
                    PlaceOnSurface(north, terrain, corner, trees[rng.Next(trees.Count)], wx, wz, rng, 0.2f, true);
                }
            }
        }

        // East foothill/mountain rocks + a few west cliff-top rocks accent the walls.
        if (rocks.Count > 0)
        {
            var stone = new GameObject("BoundaryRocks").transform; stone.SetParent(root, false);
            for (float z = -FieldLength * 0.5f + 25f; z <= FieldLength * 0.5f - 25f; z += 30f)
            {
                float wz = origin.z - SpawnFromSouth + FieldLength * 0.5f + z;
                PlaceOnSurface(stone, terrain, corner, rocks[rng.Next(rocks.Count)], corner.x + FieldWidth * 0.90f, wz, rng, 0.35f, true);
                if (rng.Next(2) == 0)
                    PlaceOnSurface(stone, terrain, corner, rocks[rng.Next(rocks.Count)], corner.x + FieldWidth * 0.83f, wz, rng, 0.35f, true);
                if (rng.Next(3) == 0)
                    PlaceOnSurface(stone, terrain, corner, rocks[rng.Next(rocks.Count)], corner.x + FieldWidth * 0.12f, wz, rng, 0.3f, true);
            }
        }
    }

    static void PlaceOnSurface(Transform parent, Terrain terrain, Vector3 corner, GameObject prefab,
                               float worldX, float worldZ, System.Random rng, float scaleVar, bool keepCollider)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float y = SurfaceY(terrain, corner, FieldWidth, FieldLength, worldX, worldZ);
        go.transform.position = new Vector3(worldX + (float)(rng.NextDouble() * 3 - 1.5), y, worldZ + (float)(rng.NextDouble() * 3 - 1.5));
        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
        go.transform.localScale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleVar;
        if (!keepCollider) StripColliders(go);
    }

    // ── Networking anchors ────────────────────────────────────────────────────────────────────────────

    static Vector3 FindDefaultEntryOrigin()
    {
        foreach (var e in Object.FindObjectsByType<ZoneEntry>(FindObjectsSortMode.None))
            if (e.entryId == "default") return new Vector3(e.transform.position.x, 0f, e.transform.position.z);

        Debug.LogWarning("[ThornwoodTerrain] No ZoneEntry(\"default\") found in the open scene — is thornwood.unity " +
                          "open (and only thornwood)? Falling back to the 3.0.2 scaffold's known position.");
        return new Vector3(5000f, 0f, -8f); // matches ZoneSetup.BuildZoneScenes' default entry placement
    }

    // Snap the existing markers onto the new surface (XZ unchanged) and carve the Grukmar entrance into the
    // east-wall notch (TW4 — "carved into the mountain face, eastern boundary, mid-north position").
    static void RepositionMarkers(Terrain terrain, Vector3 corner)
    {
        foreach (var e in Object.FindObjectsByType<ZoneEntry>(FindObjectsSortMode.None))
        {
            if (e.entryId == "from_grukmar") continue; // repositioned below, into the notch
            var p = e.transform.position;
            e.transform.position = new Vector3(p.x, SurfaceY(terrain, corner, FieldWidth, FieldLength, p.x, p.z), p.z);
        }
        foreach (var p in Object.FindObjectsByType<ZonePortal>(FindObjectsSortMode.None))
        {
            if (p.targetZoneId == "grukmars_deep") continue; // repositioned below, into the notch
            var pos = p.transform.position;
            p.transform.position = new Vector3(pos.x, SurfaceY(terrain, corner, FieldWidth, FieldLength, pos.x, pos.z), pos.z);
        }

        ZonePortal grukmarPortal = null;
        foreach (var p in Object.FindObjectsByType<ZonePortal>(FindObjectsSortMode.None))
            if (p.targetZoneId == "grukmars_deep") { grukmarPortal = p; break; }

        if (grukmarPortal == null)
        {
            Debug.LogWarning("[ThornwoodTerrain] No ZonePortal→grukmars_deep found — place one in the east-wall notch by hand.");
            return;
        }

        float notchX = corner.x + FieldWidth * 0.93f;
        float notchZ = corner.z + NotchCenterV * FieldLength;
        Vector3 notchPos = new(notchX, SurfaceY(terrain, corner, FieldWidth, FieldLength, notchX, notchZ), notchZ);
        grukmarPortal.transform.position = notchPos;

        ZoneEntry fromGrukmar = null;
        foreach (var e in Object.FindObjectsByType<ZoneEntry>(FindObjectsSortMode.None))
            if (e.entryId == "from_grukmar") { fromGrukmar = e; break; }

        if (fromGrukmar != null)
        {
            float retX = notchX - 10f; // just west of the notch, back into the walkable forest
            Vector3 retPos = new(retX, SurfaceY(terrain, corner, FieldWidth, FieldLength, retX, notchZ), notchZ);
            fromGrukmar.transform.position = retPos;
            fromGrukmar.transform.rotation = Quaternion.Euler(0f, 270f, 0f); // face west, away from the rock face
        }

        Debug.Log($"[ThornwoodTerrain] Moved the Grukmar's Deep portal into the east-wall notch at {notchPos}" +
                  (fromGrukmar != null ? " + its return entry just west of it." : " (no from_grukmar entry found to move)."));
    }
}
