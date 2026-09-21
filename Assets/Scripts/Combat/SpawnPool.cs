using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 8.4 (SP1) — a coordination marker: several <see cref="SpawnPoint"/>s can reference one pool so that at
/// most one of them ever has a live spawn at a time (the classic case: a boss/named encounter with 2-3
/// possible spots in a room, only one occupied — killing it lets a *different* spot potentially pop next,
/// not necessarily the same one). Holds no authored config, only runtime claim state — a single "who's
/// holding this" reference is enough (the actual live mob(s) are already tracked by the claiming
/// SpawnPoint's own `_live` list, so there's no need to duplicate that here).
/// </summary>
public class SpawnPool : MonoBehaviour, IWorldPlacement
{
    [Header("World Placement Sync (2.7.3)")]
    [Tooltip("GUID assigned once when this object is placed. Never hand-edit.")]
    [SerializeField] string placementId = "";

    // 8.4 (SP2) — server-only, not persisted: which SpawnPoint currently holds the claim, if any.
    SpawnPoint _claimedBy;

    /// <summary>Server-only: true if the pool was free (now claimed by <paramref name="who"/>) or already
    /// held by <paramref name="who"/> itself (idempotent re-claim — harmless). False if a *different*
    /// SpawnPoint currently holds it — normal contention, not an error.</summary>
    public bool TryClaim(SpawnPoint who)
    {
        if (_claimedBy != null && _claimedBy != who) return false;
        _claimedBy = who;
        return true;
    }

    /// <summary>Server-only, idempotent — only actually releases if <paramref name="who"/> is the current
    /// holder. Called from both the normal death path (SpawnPoint.OnMemberDied) and SpawnPoint's own
    /// self-healing prune (ActivationCheck) — a mob destroyed via a path that skips the normal death event
    /// would otherwise leave the pool claimed forever, since nothing else would ever call this.</summary>
    public void Release(SpawnPoint who)
    {
        if (_claimedBy == who) _claimedBy = null;
    }

    // ── World Placement Sync (2.7.3, Stage A) ──────────────────────────────────
    // No authored config — this is a pure coordination object referenced by SpawnPoint, not a placed/
    // configured thing in its own right. CapturePlacementData/ApplyPlacementData are deliberately empty
    // (IWorldPlacement still requires implementing them, so this marker participates in sync/materialize
    // identically to every other type — an empty payload is a valid, intentional case, not an oversight).

    public string PlacementId => placementId;
    public string MarkerType  => "SpawnPool";
    public void   SetPlacementId(string id) => placementId = id;

    public JObject CapturePlacementData() => new();
    public void    ApplyPlacementData(JObject data) { }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(placementId))
            placementId = System.Guid.NewGuid().ToString();
    }
#endif
}
