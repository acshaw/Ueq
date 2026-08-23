using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 2.7.3 (Stage A) — implemented by every scene-placed marker type that participates in world placement
/// sync (currently <see cref="SpawnPoint"/>, <see cref="PatrolRoute"/>, <see cref="WanderRegion"/>).
/// <see cref="CapturePlacementData"/>/<see cref="ApplyPlacementData"/> are inverses of each other and are
/// the single definition of "what this marker's data means" — used identically whether the destination is
/// a DB row (export), a live ephemeral server instance (materialize), or a persisted Editor scene object
/// (import, Stage B). A brand-new marker type adopts this system by implementing this interface plus a
/// matching <see cref="IPlacementFactory"/> — no other code needs to change (WP1).
/// </summary>
public interface IWorldPlacement
{
    /// <summary>GUID, assigned once (see each implementer's <c>OnValidate</c>), stable for the object's
    /// lifetime. The sole key used everywhere in this system — never the GameObject name, never position.</summary>
    string PlacementId { get; }

    /// <summary>Discriminator stored in <c>world_placements.marker_type</c> — e.g. "SpawnPoint".</summary>
    string MarkerType { get; }

    /// <summary>This object's current config (not position/rotation — the caller owns those) as JSON.</summary>
    JObject CapturePlacementData();

    /// <summary>Configure this object's config fields from JSON (the inverse of <see cref="CapturePlacementData"/>).
    /// Never touches position/rotation — callers set those separately from the row's own columns.</summary>
    void ApplyPlacementData(JObject data);

    /// <summary>Explicitly set the placement id. Only ever called by a materialize/import path building a
    /// brand-new object from a known DB row id — <c>OnValidate</c> (the hand-placement path) only fires
    /// from Editor/Inspector interaction, never from a runtime <c>AddComponent</c> call, so a freshly
    /// factory-created object would otherwise be left with an empty id.</summary>
    void SetPlacementId(string id);
}

/// <summary>
/// 2.7.3 (Stage A, WP3) — implemented by placement types that reference *other* placements by id
/// (currently only <see cref="SpawnPoint"/>, which may point at a <see cref="PatrolRoute"/> and/or a
/// <see cref="WanderRegion"/>). A Unity object reference has no meaning across a DB round-trip, so
/// cross-references are stored as <see cref="IWorldPlacement.PlacementId"/> strings in
/// <see cref="IWorldPlacement.CapturePlacementData"/>'s JSON and resolved back to real component
/// references in a second pass, after every placement for a zone has been indexed/materialized.
/// </summary>
public interface IReferencesOtherPlacements
{
    /// <summary>Resolve this object's stored placement-id references against every placement now known for
    /// the zone (both scene-baked and freshly materialized). Missing ids are logged and left unresolved
    /// rather than throwing — a dangling reference degrades to "no patrol/no wander region", not a crash.</summary>
    void ResolveReferences(IReadOnlyDictionary<string, GameObject> byPlacementId);
}
