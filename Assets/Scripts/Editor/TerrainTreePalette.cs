using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 — registers the Synty tree prefabs as TreePrototypes on the ZoneTerrain so you can use Unity's built-in
/// <b>Paint Trees</b> brush to draw trees directly onto the terrain (GPU-instanced — cheap for thousands).
/// Menu: <c>Tools/Zones/Add Synty Trees to Terrain</c>.
///
/// After running: select the terrain → Terrain component → <b>Paint Trees</b> tab → pick a tree → brush to draw
/// (tune Brush Size / Tree Density / Width+Height). <c>Terrain → Mass Place Trees</c> fills the whole zone at once.
///
/// Colliders/navmesh caveat: terrain-painted trees create their colliders at RUNTIME, so a bake-time
/// NavMeshSurface may not carve them — mobs could path through painted trees. Use painting for visual density;
/// for boundary trees that MUST block movement + navmesh, keep using <c>Tools/Zones/Scatter Props</c> (those are
/// real GameObjects with colliders). Enable "Enable Tree Colliders" in Terrain Settings for player collision.
/// </summary>
public static class TerrainTreePalette
{
    const string AdvEnv = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";

    static readonly string[] TreeNames =
    {
        "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05", "SM_Env_Tree_06",
        "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03",
    };

    [MenuItem("Tools/Zones/Add Synty Trees to Terrain")]
    public static void AddTrees()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("[TreePalette] No Terrain found — run Tools/Zones/Build Terrain Zone first.");
            return;
        }

        var protos = new List<TreePrototype>();
        foreach (var n in TreeNames)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdvEnv + n + ".prefab");
            if (prefab != null) protos.Add(new TreePrototype { prefab = prefab });
            else Debug.LogWarning($"[TreePalette] Missing tree prefab: {n}");
        }
        if (protos.Count == 0) { Debug.LogWarning("[TreePalette] No tree prefabs loaded."); return; }

        terrain.terrainData.treePrototypes = protos.ToArray();
        terrain.terrainData.RefreshPrototypes();

        Selection.activeGameObject = terrain.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TreePalette] Registered {protos.Count} Synty tree(s) on '{terrain.name}'. NEXT: with the terrain " +
                  "selected → Terrain component → Paint Trees → pick a tree + brush to draw (or Terrain → Mass Place " +
                  "Trees for the whole zone). For collision: Terrain Settings → Enable Tree Colliders. Note: painted " +
                  "trees don't reliably carve the navmesh (runtime colliders) — use Tools/Zones/Scatter Props for " +
                  "boundary trees that must block mobs.");
    }
}
