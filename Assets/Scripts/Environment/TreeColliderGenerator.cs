using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives painted terrain trees real, always-present trunk collision — a plain, NON-networked, deterministic
/// component (added once to a terrain GameObject). <see cref="Awake"/> runs identically on the server and on
/// every client/MPPM virtual player, since they all load the same baked <see cref="TerrainData"/> from the
/// scene — no Mirror sync needed at all.
///
/// Exists because Unity's built-in "Create Tree Colliders" (on <c>TerrainCollider</c>) generates colliders
/// relative to an active rendering CAMERA, which a dedicated server typically doesn't have — so the server's
/// authoritative movement check never actually gets blocked by trees even with that setting on. This sidesteps
/// that entirely with real static <see cref="CapsuleCollider"/>s that always exist, everywhere.
///
/// Deliberately reads <c>TerrainData.treeInstances</c> FRESH every time this fires (scene load / zone entry) and
/// never persists the generated colliders to the scene — so repainting/moving/adding/removing trees with the
/// built-in Paint Trees brush needs ZERO extra steps; the next time the scene loads, collision automatically
/// matches whatever the current painted layout is. The only manual step needed is re-running
/// <c>Tools/Zones/Generate Tree Collider Profiles</c> when a genuinely NEW tree species is registered on the
/// terrain's tree palette (so its radius/trunk-height gets measured) — repainting more of an EXISTING species
/// needs nothing.
///
/// Per-species radius/trunk-height are measured once at edit time (not at runtime — avoids depending on tree
/// meshes being Read/Write Enabled in a real build) and stored in <see cref="profiles"/>, populated by the
/// editor tool above.
/// </summary>
[RequireComponent(typeof(Terrain))]
public class TreeColliderGenerator : MonoBehaviour
{
    [SerializeField] List<TreeColliderProfile> profiles = new List<TreeColliderProfile>();

    public void SetProfiles(List<TreeColliderProfile> newProfiles) => profiles = newProfiles;

    void Awake()
    {
        Generate();
    }

    void Generate()
    {
        var terrain = GetComponent<Terrain>();
        var data = terrain.terrainData;
        var protos = data.treePrototypes;
        var instances = data.treeInstances;
        if (protos == null || protos.Length == 0 || instances.Length == 0) return;

        var byIndex = new TreeColliderProfile?[protos.Length];
        var missing = new HashSet<string>();
        for (int i = 0; i < protos.Length; i++)
        {
            var prefab = protos[i].prefab;
            if (prefab == null) continue;
            TreeColliderProfile? found = null;
            foreach (var p in profiles)
            {
                if (p.prefabName == prefab.name) { found = p; break; }
            }
            byIndex[i] = found;
            if (found == null) missing.Add(prefab.name);
        }
        if (missing.Count > 0)
        {
            Debug.LogWarning($"[TreeColliderGenerator] '{terrain.name}': no collider profile for " +
                              $"{string.Join(", ", missing)} — those trees won't get trunk collision until you " +
                              "re-run Tools/Zones/Generate Tree Collider Profiles in the editor and save the scene.");
        }

        var root = new GameObject("TreeColliders (generated)").transform;
        root.SetParent(transform, false);

        var tPos = transform.position;
        int built = 0;
        for (int i = 0; i < instances.Length; i++)
        {
            var inst = instances[i];
            if (inst.prototypeIndex < 0 || inst.prototypeIndex >= byIndex.Length) continue;
            var profile = byIndex[inst.prototypeIndex];
            if (profile == null) continue;

            float radius = profile.Value.radius * Mathf.Max(inst.widthScale, 0.01f);
            float height = profile.Value.height * Mathf.Max(inst.heightScale, 0.01f);
            if (radius <= 0f || height <= 0f) continue;

            float worldX = tPos.x + inst.position.x * data.size.x;
            float worldY = tPos.y + inst.position.y * data.size.y;
            float worldZ = tPos.z + inst.position.z * data.size.z;

            var go = new GameObject("TrunkCollider");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(worldX, worldY, worldZ);

            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = radius;
            cap.height = Mathf.Max(height, radius * 2f);
            cap.center = new Vector3(0, cap.height * 0.5f, 0);
            built++;
        }

        Debug.Log($"[TreeColliderGenerator] '{terrain.name}': built {built} trunk collider(s) from the current " +
                  "painted tree layout.");
    }
}

[System.Serializable]
public struct TreeColliderProfile
{
    public string prefabName;
    public float radius;
    public float height;
}
