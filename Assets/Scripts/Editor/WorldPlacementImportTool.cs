using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2.7.3 (Stage B) — the reverse of <see cref="WorldPlacementSyncTool"/>: pulls every <c>world_placements</c>
/// row for a chosen zone into that zone's currently open scene as real, editable GameObjects. Completes the
/// round trip — anything authored outside Unity (a script against the DB, a future web-editor tweak) can
/// always be pulled in for visual editing and pushed back out. Reuses <see cref="PlacementMaterializer"/> —
/// the exact same apply/factory code <see cref="ZoneManager"/> uses at runtime (Stage A) — the only
/// difference is the result is <b>persisted</b> here (the scene is marked dirty for the user to save) rather
/// than thrown away on server stop.
///
/// Reads directly from Postgres via <see cref="Database.OpenEditorConnection"/>, same convention as the
/// sync tool — no dependency on the ASP.NET API.
///
/// Stale-overwrite risk (by design, not solved with locking — see the devplan's Stage B section): if a DB
/// row was hand-edited more recently than this import, re-exporting a since-edited scene copy will clobber
/// that edit. Import fresh before hand-editing something you didn't just place yourself.
/// </summary>
public class WorldPlacementImportWindow : EditorWindow
{
    ZoneCatalog _catalog;
    string[]    _zoneIds;
    int         _selectedIndex;

    [MenuItem("Tools/Zones/Import Placements from Database")]
    static void Open()
    {
        var window = GetWindow<WorldPlacementImportWindow>(true, "Import Placements", true);
        window.minSize = new Vector2(380, 130);
        window.Initialize();
    }

    void Initialize()
    {
        _catalog = Resources.Load<ZoneCatalog>(ZoneCatalog.ResourcePath);
        if (_catalog == null) { _zoneIds = System.Array.Empty<string>(); return; }

        _zoneIds = _catalog.zones
            .Where(z => z != null && !string.IsNullOrEmpty(z.zoneId))
            .Select(z => z.zoneId)
            .ToArray();

        // Default to the active scene's own zone, if it matches one.
        var activeScene = SceneManager.GetActiveScene();
        var match = _catalog.zones.FirstOrDefault(z => z != null && z.sceneName == activeScene.name);
        _selectedIndex = match != null ? System.Array.IndexOf(_zoneIds, match.zoneId) : 0;
        if (_selectedIndex < 0) _selectedIndex = 0;
    }

    void OnGUI()
    {
        if (_zoneIds == null || _zoneIds.Length == 0)
        {
            EditorGUILayout.HelpBox("No ZoneCatalog found — run Tools/Zones/Build Zone Scenes first.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField(
            "Pulls every DB placement for the chosen zone into that zone's currently open scene. " +
            "An object already in the scene (matched by its PlacementId) is refreshed in place, never " +
            "duplicated. The zone's scene must already be open (loaded) in this Editor session.",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();
        _selectedIndex = EditorGUILayout.Popup("Zone", _selectedIndex, _zoneIds);
        EditorGUILayout.Space();

        if (GUILayout.Button("Import"))
            Import(_zoneIds[_selectedIndex]);
    }

    void Import(string zoneId)
    {
        var zoneDef = _catalog.Get(zoneId);
        var scene = SceneManager.GetSceneByName(zoneDef.sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Import Placements",
                $"Zone '{zoneId}'s scene ('{zoneDef.sceneName}') isn't open in this Editor session. " +
                "Open it (or load it additively) first, then try again.", "OK");
            return;
        }

        using var conn = Database.OpenEditorConnection();
        var rows = new WorldPlacementRepository().LoadForZone(conn, zoneId);
        if (rows.Count == 0)
        {
            Debug.Log($"[Placement] No DB placements found for zone '{zoneId}'.");
            return;
        }

        var byId = PlacementMaterializer.IndexScenePlacements(scene);
        var existingIdsBefore = new System.Collections.Generic.HashSet<string>(byId.Keys);

        PlacementMaterializer.SplitPasses(rows, out var pass1, out var pass2);
        PlacementMaterializer.ApplyRows(pass1, byId, scene);
        PlacementMaterializer.ApplyRows(pass2, byId, scene);
        PlacementMaterializer.ResolveReferences(pass2, byId);

        EditorSceneManager.MarkSceneDirty(scene);

        int created   = rows.Count(r => !existingIdsBefore.Contains(r.PlacementId));
        int refreshed = rows.Count - created;
        Debug.Log($"[Placement] Import complete for zone '{zoneId}' — {created} created, {refreshed} refreshed " +
                  "in place. Scene marked dirty — save it to keep these changes.");
    }
}
