using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ad hoc, 2026-09-20 — builds a single-room, walk-in inn near the Trellis hub from the PolygonFantasyKingdom
/// House modular kit (walls/floor/roof), NOT the pre-merged Presets folder. Checked directly: the Presets
/// (e.g. SM_Bld_Preset_Tavern_01_Optimized) are exterior-only decorative shells with no walkable interior —
/// confirmed by grepping Synty's own "Houses With Interiors" demo scene, which is built entirely from these
/// same modular House pieces and never touches the Presets folder.
///
/// Every piece is placed by MEASURING its actual rendered bounds at tool-run time (Renderer.bounds on a
/// throwaway instance at identity transform), then positioning it so that measured geometry lands on the
/// intended world point — this works regardless of where Synty authored each prefab's pivot, which was
/// never visually verified before writing this. Same "curated baseline, hand-tune in-editor afterward"
/// spirit as TrellisHubSetup.cs.
///
/// HONEST LIMITATION: the roof is the one piece placed by trust in the prefab's own authored pitch/shape
/// rather than fully derived geometry — I could not visually confirm roof panel orientation before writing
/// this. Check it first if anything looks wrong after a run.
///
/// Re-runnable: clears a prior "TrellisInn" root each run. Menu: Tools/Zones/Build Trellis Inn.
/// </summary>
public static class TrellisInnSetup
{
    const string House = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/House/";
    const string InnRoot = "TrellisInn";

    // Single room (per the user's call) — N wall segments per side, not counting corners.
    const int SegmentsX = 3;
    const int SegmentsZ = 3;
    // Which side gets the door (0=south/-Z, 1=east/+X, 2=north/+Z, 3=west/-X) and which segment along it.
    const int DoorSide = 0;
    const int DoorSegmentIndex = 1;

    [MenuItem("Tools/Zones/Build Trellis Inn")]
    public static void Build()
    {
        Vector3 origin = FindOrigin();
        Clear();
        Physics.SyncTransforms();

        var root = new GameObject(InnRoot).transform;
        root.position = origin;

        var wallPrefab   = Load("SM_Bld_House_Wall_01");
        var doorPrefab   = Load("SM_Bld_House_Wall_Door_01");
        var cornerPrefab = Load("SM_Bld_House_Base_Corner_01");
        var floorPrefab  = Load("SM_Bld_House_Floor_Wood_01");
        if (wallPrefab == null || doorPrefab == null || cornerPrefab == null || floorPrefab == null)
        {
            Debug.LogError("[TrellisInn] One or more required prefabs failed to load (see warnings above) — aborted.");
            return;
        }

        var wallBounds   = MeasureBounds(wallPrefab);
        var cornerBounds = MeasureBounds(cornerPrefab);

        float wallLen    = Mathf.Max(wallBounds.size.x, wallBounds.size.z);
        float wallHeight = wallBounds.size.y;
        float cornerLen  = Mathf.Max(cornerBounds.size.x, cornerBounds.size.z);

        float sizeX = SegmentsX * wallLen + 2f * cornerLen;
        float sizeZ = SegmentsZ * wallLen + 2f * cornerLen;

        BuildWalls(root, wallPrefab, doorPrefab, cornerPrefab, wallBounds, cornerBounds, wallLen, cornerLen, wallHeight, sizeX, sizeZ);
        BuildFloor(root, floorPrefab, sizeX, sizeZ);
        BuildRoof(root, sizeX, sizeZ, wallHeight);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TrellisInn] Built a {sizeX:F1}x{sizeZ:F1}u single-room inn at {origin} (select an empty " +
                  "to reposition before running; nothing selected = spawn point). Walls/floor placed from " +
                  "measured bounds — should connect with no gaps/overlaps regardless of the prefabs' own " +
                  "pivot convention. THE ROOF IS THE PART MOST LIKELY TO NEED HAND-ADJUSTMENT (pitch/rotation " +
                  "couldn't be visually verified before writing this) — check it first. NEXT: rebake the " +
                  "navmesh (Tools/Terrain/Rebake NavMesh) once you're happy with placement.");
    }

    [MenuItem("Tools/Zones/Clear Trellis Inn")]
    public static void Clear()
    {
        var go = GameObject.Find(InnRoot);
        if (go != null) Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // Center on the SELECTED object if one is selected (same convention as TrellisHubSetup); otherwise the
    // spawn point, then a hardcoded fallback — offset east of the existing hub so it doesn't overlap the
    // village layout TrellisHubSetup.cs already placed.
    static Vector3 FindOrigin()
    {
        var sel = Selection.activeTransform;
        if (sel != null && sel.name != InnRoot && !IsInn(sel))
            return new Vector3(sel.position.x, 0f, sel.position.z);
        var start = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        Vector3 p = start != null ? start.transform.position : new Vector3(0f, 0f, -5f);
        return new Vector3(p.x + 30f, 0f, p.z); // east of the hub's own layout (which runs out to x≈13-20)
    }

    static bool IsInn(Transform t)
    {
        for (var a = t; a != null; a = a.parent) if (a.name == InnRoot) return true;
        return false;
    }

    // ── Walls ─────────────────────────────────────────────────────────────────

    static void BuildWalls(Transform root, GameObject wallPrefab, GameObject doorPrefab, GameObject cornerPrefab,
        Bounds wallBounds, Bounds cornerBounds, float wallLen, float cornerLen, float wallHeight, float sizeX, float sizeZ)
    {
        var walls = new GameObject("Walls").transform;
        walls.SetParent(root, false);

        float hx = sizeX / 2f, hz = sizeZ / 2f;
        float wallCenterY = wallHeight * 0.5f; // rests the measured-bounds bottom at ground level (root.y)

        // Corner centers, 0°/90°/180°/270° around SM_Bld_House_Base_Corner_01's own authored facing —
        // the other candidate (besides the roof) worth checking first if a corner looks mirrored/rotated wrong.
        var cornerPositions = new (Vector3 pos, float rot)[]
        {
            (new Vector3(-hx, 0f, -hz), 0f),    // SW
            (new Vector3( hx, 0f, -hz), 90f),   // SE
            (new Vector3( hx, 0f,  hz), 180f),  // NE
            (new Vector3(-hx, 0f,  hz), 270f),  // NW
        };
        foreach (var (pos, rot) in cornerPositions)
            PlaceByCenter(cornerPrefab, cornerBounds, root.position + pos + Vector3.up * wallCenterY, rot, walls);

        // 4 sides, each walked from one corner toward the next along that side's direction. The rotations
        // below (0/90/180/270) assume the wall mesh's own long axis is local X at identity — true half the
        // time by construction, so corrected by axisOffset (measured, not assumed) rather than guessed:
        // if the wall is actually authored long-axis-Z, every rotation below needs the same +90° shift to
        // still point the wall's length along the direction it's walking, or none of the 4 sides would
        // actually span their gap (they'd sit perpendicular to the wall line instead).
        float axisOffset = wallBounds.size.x >= wallBounds.size.z ? 0f : 90f;
        BuildSide(walls, wallPrefab, doorPrefab, wallBounds, wallLen, wallCenterY, SegmentsX, DoorSide == 0,
            root.position + new Vector3(-hx + cornerLen, 0f, -hz), Vector3.right, 0f + axisOffset);
        BuildSide(walls, wallPrefab, doorPrefab, wallBounds, wallLen, wallCenterY, SegmentsZ, DoorSide == 1,
            root.position + new Vector3(hx, 0f, -hz + cornerLen), Vector3.forward, 90f + axisOffset);
        BuildSide(walls, wallPrefab, doorPrefab, wallBounds, wallLen, wallCenterY, SegmentsX, DoorSide == 2,
            root.position + new Vector3(hx - cornerLen, 0f, hz), Vector3.left, 180f + axisOffset);
        BuildSide(walls, wallPrefab, doorPrefab, wallBounds, wallLen, wallCenterY, SegmentsZ, DoorSide == 3,
            root.position + new Vector3(-hx, 0f, hz - cornerLen), Vector3.back, 270f + axisOffset);
    }

    static void BuildSide(Transform parent, GameObject wallPrefab, GameObject doorPrefab, Bounds wallBounds,
        float wallLen, float wallCenterY, int segments, bool hasDoor, Vector3 startWorld, Vector3 dir, float rotY)
    {
        var doorBounds = MeasureBounds(doorPrefab);
        for (int i = 0; i < segments; i++)
        {
            Vector3 worldCenter = startWorld + dir * (wallLen * (i + 0.5f)) + Vector3.up * wallCenterY;
            bool useDoor = hasDoor && i == DoorSegmentIndex;
            PlaceByCenter(useDoor ? doorPrefab : wallPrefab, useDoor ? doorBounds : wallBounds, worldCenter, rotY, parent);
        }
    }

    // ── Floor ─────────────────────────────────────────────────────────────────

    static void BuildFloor(Transform root, GameObject floorPrefab, float sizeX, float sizeZ)
    {
        var floor = new GameObject("Floor").transform;
        floor.SetParent(root, false);

        var bounds = MeasureBounds(floorPrefab);
        float tileX = bounds.size.x, tileZ = bounds.size.z;
        if (tileX <= 0.01f || tileZ <= 0.01f)
        {
            Debug.LogWarning("[TrellisInn] Floor tile measured to ~zero size — skipping floor.");
            return;
        }

        int countX = Mathf.Max(1, Mathf.RoundToInt(sizeX / tileX));
        int countZ = Mathf.Max(1, Mathf.RoundToInt(sizeZ / tileZ));

        for (int x = 0; x < countX; x++)
        for (int z = 0; z < countZ; z++)
        {
            float cx = -sizeX / 2f + tileX * (x + 0.5f);
            float cz = -sizeZ / 2f + tileZ * (z + 0.5f);
            PlaceByCenter(floorPrefab, bounds, root.position + new Vector3(cx, 0f, cz), 0f, floor);
        }

        // Tiling at the prefab's native size, starting from one corner, may slightly overhang or fall short
        // of the exact wall footprint if tileX/tileZ doesn't evenly divide sizeX/sizeZ — a minor edge gap/
        // overlap is the expected/acceptable imperfection here, not a bug; nudge in-editor if it's visible.
    }

    // ── Roof (best-effort — see the class doc comment's honest limitation) ─────────────────────────────

    static void BuildRoof(Transform root, float sizeX, float sizeZ, float wallHeight)
    {
        var roof = new GameObject("Roof").transform;
        roof.SetParent(root, false);

        var panelPrefab = Load("SM_Bld_House_Roof_Thatch_01");
        var peakCapPrefab = Load("SM_Bld_House_Roof_Thatch_Peak_Cap_01");
        var gablePrefab = Load("SM_Bld_House_Wall_Peak_01");
        if (panelPrefab == null) { Debug.LogWarning("[TrellisInn] No roof panel prefab found — skipping roof."); return; }

        var panelBounds = MeasureBounds(panelPrefab);
        // Ridge runs along X (the longer of the two room dimensions reads better with a ridge along it,
        // but SegmentsX == SegmentsZ here so this is arbitrary for a square room — change freely).
        float panelLen = Mathf.Max(panelBounds.size.x, panelBounds.size.z);
        int panelCount = Mathf.Max(1, Mathf.CeilToInt(sizeX / panelLen));
        float eaveOutset = 0.3f; // small overhang past the wall face, typical roof convention

        for (int i = 0; i < panelCount; i++)
        {
            float cx = -sizeX / 2f + panelLen * (i + 0.5f);
            // South-facing slope (eave at the south wall, rising toward the center ridge) and the mirrored
            // north-facing slope — placed by the panel's own bounds CENTER at the wall-top edge, trusting
            // the prefab's authored pitch to carry the rest of the shape upward/inward on its own, per the
            // class doc comment's roof caveat.
            PlaceByCenter(panelPrefab, panelBounds,
                root.position + new Vector3(cx, wallHeight, -sizeZ / 2f - eaveOutset), 0f, roof);
            PlaceByCenter(panelPrefab, panelBounds,
                root.position + new Vector3(cx, wallHeight, sizeZ / 2f + eaveOutset), 180f, roof);
        }

        if (peakCapPrefab != null)
        {
            var capBounds = MeasureBounds(peakCapPrefab);
            float capLen = Mathf.Max(capBounds.size.x, capBounds.size.z);
            int capCount = Mathf.Max(1, Mathf.CeilToInt(sizeX / capLen));
            for (int i = 0; i < capCount; i++)
            {
                float cx = -sizeX / 2f + capLen * (i + 0.5f);
                PlaceByCenter(peakCapPrefab, capBounds,
                    root.position + new Vector3(cx, wallHeight + panelBounds.size.y * 0.6f, 0f), 0f, roof);
            }
        }

        if (gablePrefab != null)
        {
            var gableBounds = MeasureBounds(gablePrefab);
            PlaceByCenter(gablePrefab, gableBounds, root.position + new Vector3(-sizeX / 2f, wallHeight, 0f), 90f, roof);
            PlaceByCenter(gablePrefab, gableBounds, root.position + new Vector3(sizeX / 2f, wallHeight, 0f), 270f, roof);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject Load(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(House + name + ".prefab");
        if (prefab == null) Debug.LogWarning($"[TrellisInn] Missing prefab: {House}{name}.prefab");
        return prefab;
    }

    // Measures a prefab's actual rendered bounds by instantiating a throwaway copy at identity transform and
    // reading Renderer.bounds — this is why placement doesn't need to assume Synty's pivot convention.
    static Bounds MeasureBounds(GameObject prefab)
    {
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        temp.transform.localScale = Vector3.one;

        var renderers = temp.GetComponentsInChildren<Renderer>();
        Bounds b = default;
        bool has = false;
        foreach (var r in renderers)
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        Object.DestroyImmediate(temp);
        return has ? b : new Bounds(Vector3.zero, Vector3.one);
    }

    // Places a prefab so its MEASURED bounds center lands at targetCenter, at the given Y rotation —
    // correct regardless of where the prefab's actual pivot sits, since it's derived from the same
    // measurement MeasureBounds already took, not an assumed convention.
    static GameObject PlaceByCenter(GameObject prefab, Bounds localBounds, Vector3 targetCenter, float yRot, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var rot = Quaternion.Euler(0f, yRot, 0f);
        Vector3 rotatedCenterOffset = rot * localBounds.center;
        go.transform.SetPositionAndRotation(targetCenter - rotatedCenterOffset, rot);
        return go;
    }
}
