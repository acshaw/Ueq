using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 6.2 diagnostic — a new production character on the real Linux Dedicated Server fell through
/// the world. <c>CharacterSelectController.SpawnPlayer</c> uses <c>NetworkManager.GetStartPosition()</c>,
/// which picks among ALL <see cref="NetworkStartPosition"/> objects in the loaded scene(s). Audits
/// every one in SampleScene for whether it's actually sitting on solid ground.
/// </summary>
public static class StartPositionAudit
{
    [MenuItem("Tools/Zones/Audit Start Positions")]
    static void Audit()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var points = Object.FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None);
        Debug.Log($"[StartPositionAudit] Found {points.Length} NetworkStartPosition object(s) in {scene.name}.");

        foreach (var p in points)
        {
            var pos = p.transform.position;
            string path = GetPath(p.transform);
            if (Physics.Raycast(pos + Vector3.up * 500f, Vector3.down, out var hit, 2000f))
            {
                float gap = pos.y - hit.point.y;
                string flag = Mathf.Abs(gap) > 5f ? "  <<< SUSPECT" : "";
                Debug.Log($"[StartPositionAudit] {path} @ {pos} — ground at y={hit.point.y:F1} (gap {gap:F1}) hit={hit.collider.name}{flag}");
            }
            else
            {
                Debug.LogWarning($"[StartPositionAudit] {path} @ {pos} — NO GROUND FOUND BELOW (raycast 2000u)  <<< SUSPECT");
            }
        }
    }

    // 6.2 fix: deletes every stale duplicate "SpawnPoint" NetworkStartPosition object (the bug
    // this file's Audit() diagnosed — see SceneSetup.CreateNetworkManager's fix comment), keeping
    // any differently-named (hand-placed, e.g. "StartSpawnPoint") one untouched. Confirms exactly
    // one properly-grounded NetworkStartPosition remains, then saves the scene.
    [MenuItem("Tools/Zones/Clean Duplicate Start Positions")]
    static void CleanDuplicates()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var points = Object.FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None);
        int removed = 0;
        foreach (var p in points)
        {
            if (p.gameObject.name == "SpawnPoint")
            {
                Object.DestroyImmediate(p.gameObject);
                removed++;
            }
        }
        Debug.Log($"[StartPositionAudit] Removed {removed} duplicate 'SpawnPoint' object(s).");

        var remaining = Object.FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None);
        Debug.Log($"[StartPositionAudit] {remaining.Length} NetworkStartPosition object(s) remain:");
        foreach (var p in remaining)
            Debug.Log($"[StartPositionAudit]   {GetPath(p.transform)} @ {p.transform.position}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[StartPositionAudit] Scene saved.");
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
