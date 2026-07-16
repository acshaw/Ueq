using UnityEngine;

// Eventide combat pipeline §2.3 — ordered low to high. Crippling is class-passive-unlock only; no
// passive system exists yet, so nothing currently grants weight into it (CombatResolver treats it as
// always-zero-gain, per the design doc's own "pre-unlock those points redistribute into Critical").
public enum HitTier { Miss, Glancing, Hit, SolidHit, GoodHit, Critical, Crippling }

/// <summary>
/// Weighted hit-tier distribution (design doc §2.2). Raw weight units, matching the doc's own tables
/// directly (they sum to 340) — nothing here enforces that total; modifiers transfer weight between
/// tiers and the resolver sums whatever is present at roll time.
/// </summary>
[System.Serializable]
public struct CombatTierTable
{
    public float Miss, Glancing, Hit, SolidHit, GoodHit, Critical, Crippling;

    public static readonly HitTier[] Order =
    {
        HitTier.Miss, HitTier.Glancing, HitTier.Hit, HitTier.SolidHit,
        HitTier.GoodHit, HitTier.Critical, HitTier.Crippling,
    };

    public float Get(HitTier t) => t switch
    {
        HitTier.Miss      => Miss,
        HitTier.Glancing  => Glancing,
        HitTier.Hit       => Hit,
        HitTier.SolidHit  => SolidHit,
        HitTier.GoodHit   => GoodHit,
        HitTier.Critical  => Critical,
        HitTier.Crippling => Crippling,
        _ => 0f,
    };

    public void Set(HitTier t, float value)
    {
        switch (t)
        {
            case HitTier.Miss:      Miss = value;      break;
            case HitTier.Glancing:  Glancing = value;  break;
            case HitTier.Hit:       Hit = value;       break;
            case HitTier.SolidHit:  SolidHit = value;  break;
            case HitTier.GoodHit:   GoodHit = value;   break;
            case HitTier.Critical:  Critical = value;  break;
            case HitTier.Crippling: Crippling = value; break;
        }
    }

    public void Add(HitTier t, float delta) => Set(t, Mathf.Max(0f, Get(t) + delta));

    public float Total => Miss + Glancing + Hit + SolidHit + GoodHit + Critical + Crippling;

    public static CombatTierTable Lerp(CombatTierTable a, CombatTierTable b, float t) => new()
    {
        Miss      = Mathf.Lerp(a.Miss, b.Miss, t),
        Glancing  = Mathf.Lerp(a.Glancing, b.Glancing, t),
        Hit       = Mathf.Lerp(a.Hit, b.Hit, t),
        SolidHit  = Mathf.Lerp(a.SolidHit, b.SolidHit, t),
        GoodHit   = Mathf.Lerp(a.GoodHit, b.GoodHit, t),
        Critical  = Mathf.Lerp(a.Critical, b.Critical, t),
        Crippling = Mathf.Lerp(a.Crippling, b.Crippling, t),
    };

    // Design doc §2.5 — Warrior Level 1 starting table. Used as the default for newly authored mobs
    // (HR5) and as a last-resort fallback when a class has no table configured.
    public static CombatTierTable WarriorLevel1 => new()
    {
        Miss = 17.5f, Glancing = 40f, Hit = 30f, SolidHit = 10f, GoodHit = 2.5f, Critical = 0f, Crippling = 0f,
    };
}
