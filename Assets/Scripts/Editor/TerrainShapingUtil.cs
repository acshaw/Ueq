using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3.5 — small, proven-stable primitives shared by the per-zone terrain builders (<see cref="TerrainZoneSetup"/>
/// for Creslin's Field, <see cref="ThornwoodTerrainSetup"/> for Thornwood). Only new code uses this — the
/// existing, hard-won Creslin's Field pipeline (see the 2026-07-06 heightmap/alpha-smoothness saga in
/// CLAUDE.md) is left untouched rather than retrofitted, to avoid regressing something already verified.
/// </summary>
public static class TerrainShapingUtil
{
    /// <summary>Fractal Perlin noise in [0,1]. <paramref name="features"/> = cycles across the terrain.</summary>
    public static float Fbm(float u, float v, float features, int octaves, float seed = 137.31f)
    {
        float sum = 0f, amp = 1f, freq = features, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum  += amp * Mathf.PerlinNoise(u * freq + seed, v * freq + seed);
            norm += amp;
            amp  *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    /// <summary>GLSL-style smoothstep (handles e0 &gt; e1 for descending ramps).</summary>
    public static float SStep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    public static List<GameObject> LoadAll(string folder, params string[] names)
    {
        var list = new List<GameObject>();
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(folder + n + ".prefab");
            if (p != null) list.Add(p);
        }
        return list;
    }

    public static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
    }

    /// <summary>Measures the renderer bounds of an already-instantiated GameObject (not a probe-and-destroy —
    /// callers that only have a prefab asset should instantiate first).</summary>
    public static void MeasureFootprint(GameObject liveInstance, out Vector3 size, out Vector3 center)
    {
        var renderers = liveInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { size = new Vector3(4, 0, 4); center = Vector3.zero; return; }
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        size = b.size; center = b.center;
    }

    public static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    public static void SetEnum(SerializedObject so, string prop, string enumName)
    {
        var p = so.FindProperty(prop);
        if (p == null) return;
        int idx = Array.IndexOf(p.enumNames, enumName);
        if (idx >= 0) p.enumValueIndex = idx;
    }

    /// <summary>World height on a terrain built by the corner/size convention the zone terrain builders share.</summary>
    public static float SurfaceY(Terrain terrain, Vector3 corner, float fieldWidth, float fieldLength, float worldX, float worldZ)
    {
        float u = Mathf.Clamp01((worldX - corner.x) / fieldWidth);
        float v = Mathf.Clamp01((worldZ - corner.z) / fieldLength);
        return corner.y + terrain.terrainData.GetInterpolatedHeight(u, v);
    }

    /// <summary>Adds/configures a NavMeshSurface on <paramref name="host"/> (whole-scene collect, physics
    /// colliders — the terrain's own collider + every prop's collider), bakes it, and persists the resulting
    /// NavMeshData to <paramref name="navAssetPath"/> so an ADDITIVELY-LOADED zone scene reloads with a live
    /// navmesh (the scripted BuildNavMesh() call alone leaves the data in memory only — the exact bug fixed
    /// for the flat zone scaffolds in 3.0.1/<see cref="ZoneSetup"/>). Creslin's Field's terrain doesn't need
    /// this (it's the always-loaded base scene), which is why <see cref="TerrainZoneSetup"/> doesn't call it.</summary>
    public static void BakeAndPersistTerrainNavMesh(GameObject host, string navAssetPath)
    {
        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType == null)
        {
            Debug.LogWarning("[TerrainShaping] AI Navigation package missing — add a NavMeshSurface to the terrain + bake manually.");
            return;
        }

        var surface = host.GetComponent(surfaceType) ?? host.AddComponent(surfaceType);
        var so = new SerializedObject(surface);
        SetEnum(so, "m_UseGeometry", "PhysicsColliders"); // TerrainCollider + every prop collider
        SetEnum(so, "m_CollectObjects", "All");           // whole scene: terrain + Synty props (which carve/exclude)
        so.ApplyModifiedPropertiesWithoutUndo();

        var build = surfaceType.GetMethod("BuildNavMesh");
        if (build != null) build.Invoke(surface, null);

        var surfSo  = new SerializedObject(surface);
        var dataRef = surfSo.FindProperty("m_NavMeshData");
        var data    = dataRef?.objectReferenceValue;
        if (data != null)
        {
            if (!AssetDatabase.Contains(data))
            {
                AssetDatabase.DeleteAsset(navAssetPath);
                AssetDatabase.CreateAsset(data, navAssetPath);
            }
            dataRef.objectReferenceValue = data;
            surfSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[TerrainShaping] Navmesh baked + persisted -> {navAssetPath}.");
        }
        else Debug.LogWarning("[TerrainShaping] BuildNavMesh produced no data — bake this terrain's NavMeshSurface manually, then save.");
    }
}
