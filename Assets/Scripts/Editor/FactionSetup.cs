using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools/Setup Faction Data — run after Tools/Setup Player Scene.
// Creates default ScriptableObjects and wires them into the scene.
public static class FactionSetup
{
    const string SoPath = "Assets/ScriptableObjects/Faction";

    [MenuItem("Tools/Setup Faction Data")]
    public static void Run()
    {
        EnsureDirectory(SoPath);

        var thresholds   = GetOrCreateThresholdTable();
        var faction      = GetOrCreateTestFaction(thresholds);
        var raceDefaults = GetOrCreateRaceDefaults(faction);

        WireSceneObjects(faction, raceDefaults);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FactionSetup] Done. Assets at {SoPath}/");
    }

    // ── ScriptableObject creation ─────────────────────────────────────────────

    static FactionThresholdTable GetOrCreateThresholdTable()
    {
        var path     = $"{SoPath}/DefaultThresholds.asset";
        var existing = AssetDatabase.LoadAssetAtPath<FactionThresholdTable>(path);
        if (existing != null) return existing;

        var table = ScriptableObject.CreateInstance<FactionThresholdTable>();
        table.Thresholds = new List<FactionThreshold>
        {
            new() { Name = "KOS",          MinScore = -10000 },
            new() { Name = "Threatening",  MinScore =   -750 },
            new() { Name = "Dubious",      MinScore =   -500 },
            new() { Name = "Apprehensive", MinScore =   -100 },
            new() { Name = "Indifferent",  MinScore =      0 },
            new() { Name = "Amiable",      MinScore =    100 },
            new() { Name = "Kindly",       MinScore =    500 },
            new() { Name = "Warmly",       MinScore =    750 },
            new() { Name = "Ally",         MinScore =   1100 },
        };
        AssetDatabase.CreateAsset(table, path);
        return table;
    }

    static FactionDefinition GetOrCreateTestFaction(FactionThresholdTable thresholds)
    {
        var path     = $"{SoPath}/QeynosGuards.asset";
        var existing = AssetDatabase.LoadAssetAtPath<FactionDefinition>(path);
        if (existing != null) return existing;

        var faction = ScriptableObject.CreateInstance<FactionDefinition>();
        faction.FactionName      = "Qeynos Guards";
        faction.ThresholdTable   = thresholds;
        faction.AlliedFactions   = new List<FactionDefinition>();
        faction.HostileFactions  = new List<FactionDefinition>();
        AssetDatabase.CreateAsset(faction, path);
        return faction;
    }

    static RaceDefaultsTable GetOrCreateRaceDefaults(FactionDefinition faction)
    {
        var path     = $"{SoPath}/RaceDefaults.asset";
        var existing = AssetDatabase.LoadAssetAtPath<RaceDefaultsTable>(path);
        if (existing != null) return existing;

        var table = ScriptableObject.CreateInstance<RaceDefaultsTable>();
        table.Defaults = new List<RaceDefaultEntry>
        {
            new() { Race = "Human", Faction = faction, Score =      0 }, // Indifferent
            new() { Race = "Troll", Faction = faction, Score =  -5000 }, // KOS
            new() { Race = "Dwarf", Faction = faction, Score =    500 }, // Kindly
        };
        AssetDatabase.CreateAsset(table, path);
        return table;
    }

    // ── Scene wiring ──────────────────────────────────────────────────────────

    static void WireSceneObjects(FactionDefinition faction, RaceDefaultsTable raceDefaults)
    {
        WireEnemy(faction);
        WirePlayer(raceDefaults);
    }

    static void WireEnemy(FactionDefinition faction)
    {
        var enemy = GameObject.Find("Enemy");
        if (enemy == null)
        {
            Debug.LogWarning("[FactionSetup] No Enemy in scene — run Tools/Setup Player Scene first.");
            return;
        }

        var npcFaction = enemy.GetComponent<NpcFaction>() ?? enemy.AddComponent<NpcFaction>();
        var so = new SerializedObject(npcFaction);
        so.FindProperty("faction").objectReferenceValue = faction;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enemy);
    }

    static void WirePlayer(RaceDefaultsTable raceDefaults)
    {
        // Mirror spawns players from the prefab, not the scene object — must modify the prefab.
        const string prefabPath = "Assets/Prefabs/Player.prefab";
        if (!System.IO.File.Exists(prefabPath))
        {
            Debug.LogWarning("[FactionSetup] Player prefab not found at Assets/Prefabs/Player.prefab — run Tools/Setup Player Scene, drag the Player into Assets/Prefabs/, then re-run this.");
            return;
        }

        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        var scores = prefab.GetComponent<PlayerFactionScores>() ?? prefab.AddComponent<PlayerFactionScores>();
        var so     = new SerializedObject(scores);
        so.FindProperty("raceDefaults").objectReferenceValue = raceDefaults;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefab);

        Debug.Log("[FactionSetup] Player prefab updated with RaceDefaultsTable.");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    static void EnsureDirectory(string path)
    {
        var parts   = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
