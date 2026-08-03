using UnityEditor;
using UnityEngine;

/// <summary>
/// Assembles a rectangular room (floor, walls, one door, flat roof cap) from the Synty
/// PolygonPrototype "Simple" modular kit, snapped to a uniform grid. Produces a plain box
/// shell — a starting point to dress with props and re-place pieces by hand, not a finished
/// room. Wall facing/orientation is a best-effort guess at the kit's pivot convention; use
/// "Wall Rotation Offset" to correct it if walls come out running the wrong way. The default
/// kit material is a yellow blockout grid; "Material Variant" swaps in one of the pack's own
/// numbered swatch materials instead (still not a true fantasy-styled texture).
/// </summary>
public class ModularRoomBuilder : EditorWindow
{
    const string KitDir = "Assets/Synty/PolygonPrototype/Prefabs/Buildings/Simple/";
    const string FloorPath = KitDir + "SM_Buildings_Floor_1x1_01.prefab";
    const string WallPath = KitDir + "SM_Buildings_Wall_1x3_01.prefab";
    const string WallDoorPath = KitDir + "SM_Buildings_WallDoor_2x3_01.prefab";
    const string MaterialDir = "Assets/Synty/PolygonPrototype/Materials/";

    enum DoorSide { South, North, West, East }

    string _roomName = "Test Room";
    int _widthUnits = 6;
    int _depthUnits = 5;
    DoorSide _doorSide = DoorSide.South;
    bool _includeRoof = true;
    int _wallRotationOffset;
    int _materialVariant = 1;

    [MenuItem("Tools/Zones/Build Modular Room")]
    static void Open() => GetWindow<ModularRoomBuilder>("Modular Room Builder");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Assembles a rectangular room from PolygonPrototype's Simple modular kit at the " +
            "current Scene view pivot. Produces a plain box shell — dress it with props and " +
            "nudge pieces by hand afterward. If walls come out facing/running the wrong way " +
            "(e.g. two sides look like thin edge-on slivers), try Wall Rotation Offset = 90 " +
            "and rebuild.",
            MessageType.Info);

        _roomName = EditorGUILayout.TextField("Room Name", _roomName);
        _widthUnits = EditorGUILayout.IntSlider("Width (units)", _widthUnits, 3, 20);
        _depthUnits = EditorGUILayout.IntSlider("Depth (units)", _depthUnits, 3, 20);
        _doorSide = (DoorSide)EditorGUILayout.EnumPopup("Door Side", _doorSide);
        _includeRoof = EditorGUILayout.Toggle("Include Roof (flat cap)", _includeRoof);
        _wallRotationOffset = EditorGUILayout.IntPopup("Wall Rotation Offset", _wallRotationOffset,
            new[] { "0", "90", "180", "270" }, new[] { 0, 90, 180, 270 });
        _materialVariant = EditorGUILayout.IntSlider("Material Variant (1-10)", _materialVariant, 1, 10);
        EditorGUILayout.HelpBox(
            "The kit's default material is a yellow blockout grid, by design. This swaps in one " +
            "of the pack's own numbered swatch materials instead (still not a true fantasy-cottage " +
            "texture — that needs a real modular fantasy building kit, which isn't in your owned " +
            "assets yet). Cycle the number and rebuild to see which variant reads best.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Position the Scene view over open, flat ground first — the room is built at the " +
            "Scene view pivot, snapped down onto whatever's beneath it.", MessageType.None);
        if (GUILayout.Button("Build At Scene View Pivot"))
            Build();
    }

    void Build()
    {
        var floor = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath);
        var wall = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
        var wallDoor = AssetDatabase.LoadAssetAtPath<GameObject>(WallDoorPath);
        if (floor == null || wall == null || wallDoor == null)
        {
            Debug.LogError("[ModularRoomBuilder] One or more kit prefabs not found at the expected paths under " + KitDir);
            return;
        }

        string materialPath = $"{MaterialDir}PolygonPrototype_{_materialVariant:D2}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
            Debug.LogWarning($"[ModularRoomBuilder] Material variant not found at {materialPath} — pieces will keep the default yellow grid material.");

        Vector3 floorSize = MeasureBounds(floor);
        Vector3 wallSize = MeasureBounds(wall);
        Vector3 doorSize = MeasureBounds(wallDoor);

        float unit = (floorSize.x + floorSize.z) * 0.5f;
        if (Mathf.Abs(floorSize.x - floorSize.z) > 0.05f)
            Debug.LogWarning($"[ModularRoomBuilder] Floor tile isn't square ({floorSize.x:F2} x {floorSize.z:F2}) — using the average as grid unit; expect minor gaps.");

        float wallSpan = Mathf.Max(wallSize.x, wallSize.z);
        float doorSpan = Mathf.Max(doorSize.x, doorSize.z);
        float wallHeight = wallSize.y;
        if (Mathf.Abs(wallSpan - unit) > 0.05f)
            Debug.LogWarning($"[ModularRoomBuilder] Wall segment span ({wallSpan:F2}) doesn't match the floor grid unit ({unit:F2}) — walls may not perfectly meet floor edges.");

        int doorSlots = Mathf.Clamp(Mathf.RoundToInt(doorSpan / unit), 1,
            (_doorSide == DoorSide.South || _doorSide == DoorSide.North) ? _widthUnits : _depthUnits);
        if ((_doorSide == DoorSide.South || _doorSide == DoorSide.North) && _widthUnits < doorSlots ||
            (_doorSide == DoorSide.West || _doorSide == DoorSide.East) && _depthUnits < doorSlots)
        {
            Debug.LogError("[ModularRoomBuilder] Chosen side is too short to fit a door piece — widen the room or pick another side.");
            return;
        }

        Vector3 pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        Vector3 origin = ResolveGround(pivot);

        var root = new GameObject(_roomName);
        root.transform.position = origin;
        Undo.RegisterCreatedObjectUndo(root, "Build Modular Room");

        var floorParent = new GameObject("Floor").transform;
        floorParent.SetParent(root.transform, false);
        var wallParent = new GameObject("Walls").transform;
        wallParent.SetParent(root.transform, false);

        for (int x = 0; x < _widthUnits; x++)
            for (int z = 0; z < _depthUnits; z++)
            {
                Vector3 pos = origin + new Vector3((x + 0.5f) * unit, 0f, (z + 0.5f) * unit);
                ApplyMaterial(Instantiate(floor, pos, Quaternion.identity, floorParent), material);
            }

        Quaternion offsetRot = Quaternion.Euler(0, _wallRotationOffset, 0);

        BuildWallRow(wallParent, wall, wallDoor, material, origin, unit, doorSlots, _widthUnits, Vector3.right,
            Vector3.zero, Quaternion.identity * offsetRot, _doorSide == DoorSide.South);

        BuildWallRow(wallParent, wall, wallDoor, material, origin, unit, doorSlots, _widthUnits, Vector3.right,
            new Vector3(0, 0, _depthUnits * unit), Quaternion.Euler(0, 180, 0) * offsetRot, _doorSide == DoorSide.North);

        BuildWallRow(wallParent, wall, wallDoor, material, origin, unit, doorSlots, _depthUnits, Vector3.forward,
            Vector3.zero, Quaternion.Euler(0, -90, 0) * offsetRot, _doorSide == DoorSide.West);

        BuildWallRow(wallParent, wall, wallDoor, material, origin, unit, doorSlots, _depthUnits, Vector3.forward,
            new Vector3(_widthUnits * unit, 0, 0), Quaternion.Euler(0, 90, 0) * offsetRot, _doorSide == DoorSide.East);

        if (_includeRoof)
        {
            var roofParent = new GameObject("Roof").transform;
            roofParent.SetParent(root.transform, false);
            for (int x = 0; x < _widthUnits; x++)
                for (int z = 0; z < _depthUnits; z++)
                {
                    Vector3 pos = origin + new Vector3((x + 0.5f) * unit, wallHeight, (z + 0.5f) * unit);
                    ApplyMaterial(Instantiate(floor, pos, Quaternion.Euler(180, 0, 0), roofParent), material);
                }
        }

        Selection.activeGameObject = root;
        Debug.Log($"[ModularRoomBuilder] Built '{_roomName}' — {_widthUnits}x{_depthUnits} units " +
                   $"(~{_widthUnits * unit:F1}m x {_depthUnits * unit:F1}m), door on {_doorSide}, at {origin}. " +
                   "Walk in and check facing/rotation before dressing it.");
    }

    static void BuildWallRow(Transform parent, GameObject wallPrefab, GameObject doorPrefab, Material material,
        Vector3 origin, float unit, int doorSlots, int sideLengthUnits, Vector3 axis,
        Vector3 edgeOffset, Quaternion rotation, bool hasDoor)
    {
        int doorStart = hasDoor ? (sideLengthUnits - doorSlots) / 2 : -1;

        for (int i = 0; i < sideLengthUnits; i++)
        {
            if (hasDoor && i == doorStart)
            {
                Vector3 center = origin + edgeOffset + axis * ((doorStart + doorSlots * 0.5f) * unit);
                ApplyMaterial(Instantiate(doorPrefab, center, rotation, parent), material);
                i += doorSlots - 1;
                continue;
            }
            Vector3 pos = origin + edgeOffset + axis * ((i + 0.5f) * unit);
            ApplyMaterial(Instantiate(wallPrefab, pos, rotation, parent), material);
        }
    }

    static void ApplyMaterial(GameObject instance, Material material)
    {
        if (material == null) return;
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            renderer.sharedMaterials = mats;
        }
    }

    static Vector3 MeasureBounds(GameObject prefab)
    {
        var probe = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        var renderers = probe.GetComponentsInChildren<Renderer>();
        Vector3 size;
        if (renderers.Length == 0)
        {
            size = Vector3.one;
        }
        else
        {
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            size = b.size;
        }
        DestroyImmediate(probe);
        return size;
    }

    static Vector3 ResolveGround(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 200f, Vector3.down, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return point;
    }
}
