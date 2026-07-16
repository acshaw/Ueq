using UnityEngine;

/// <summary>
/// DM1 — tunable tier→damage-percentage table + variance band for Step 3 (Damage Application). Only
/// Miss (0%) and SolidHit (100%, the reference point) are locked by the design doc §4.1 — the rest are
/// explicit "approximate, TBD" placeholders. Ships as a Resources-loaded asset so they're editable
/// without a recompile (same convention as <see cref="XpTableDefinition"/>); works with zero authoring
/// via the in-memory fallback below.
/// </summary>
[CreateAssetMenu(menuName = "Ueq/Combat Tier Damage Config")]
public class CombatTierDamageConfig : ScriptableObject
{
    [Range(0f, 3f)] public float missPercent      = 0f;
    [Range(0f, 3f)] public float glancingPercent  = 0.25f;
    [Range(0f, 3f)] public float hitPercent       = 0.60f;
    [Range(0f, 3f)] public float solidHitPercent  = 1.00f; // locked reference point (§4.1)
    [Range(0f, 3f)] public float goodHitPercent   = 1.10f;
    [Range(0f, 3f)] public float criticalPercent  = 1.25f;
    [Range(0f, 3f)] public float cripplingPercent = 1.50f;

    [Range(0f, 0.5f)] public float variance = 0.125f; // ± band applied per swing (§4.1 "meaningful spread")

    public float PercentFor(HitTier tier) => tier switch
    {
        HitTier.Miss      => missPercent,
        HitTier.Glancing  => glancingPercent,
        HitTier.Hit       => hitPercent,
        HitTier.SolidHit  => solidHitPercent,
        HitTier.GoodHit   => goodHitPercent,
        HitTier.Critical  => criticalPercent,
        HitTier.Crippling => cripplingPercent,
        _ => 0f,
    };

    static CombatTierDamageConfig _cache;

    /// <summary>Resources asset if authored (create one via the Create Asset menu at
    /// <c>Resources/CombatTierDamageConfig.asset</c> to tune in the Inspector); otherwise an in-memory
    /// instance carrying the placeholder defaults above, so the pipeline works with zero setup.</summary>
    public static CombatTierDamageConfig Active
    {
        get
        {
            if (_cache != null) return _cache;
            _cache = Resources.Load<CombatTierDamageConfig>("CombatTierDamageConfig");
            if (_cache == null) _cache = CreateInstance<CombatTierDamageConfig>();
            return _cache;
        }
    }
}
