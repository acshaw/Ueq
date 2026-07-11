using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-usable lookup of mob body art by <c>modelId</c> (3.1.10 Increment B). Loads the single
/// <see cref="MobModelCatalog"/> asset from Resources so clients can build bodies without DB access (mob
/// content is server-only; the body art must be resolvable everywhere the model is shown).
/// </summary>
public static class MobModelRegistry
{
    const string CatalogPath = "MobModelCatalog"; // Assets/Resources/MobModelCatalog.asset

    static Dictionary<string, MobModelCatalog.Entry> _byId;

    static void EnsureLoaded()
    {
        if (_byId != null) return;
        _byId = new Dictionary<string, MobModelCatalog.Entry>();

        var catalog = Resources.Load<MobModelCatalog>(CatalogPath);
        if (catalog == null) return;

        foreach (var e in catalog.entries)
            if (!string.IsNullOrEmpty(e.modelId) && e.prefab != null)
                _byId[e.modelId] = e; // last-wins on duplicate ids (rename in the catalog to disambiguate)
    }

    public static bool TryGet(string modelId, out MobModelCatalog.Entry entry)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(modelId) && _byId.TryGetValue(modelId, out entry)) return true;
        entry = default;
        return false;
    }

    public static IEnumerable<string> AllModelIds()
    {
        EnsureLoaded();
        return _byId.Keys;
    }

    // The editor catalog tool calls this after a rebuild so an open play session doesn't read a stale cache.
    public static void Invalidate() => _byId = null;
}
