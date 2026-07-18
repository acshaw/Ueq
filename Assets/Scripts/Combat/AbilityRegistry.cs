using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime lookup of ability definitions by <c>abilityId</c>. Source of truth is Postgres (M2.9): the
/// server populates this from the DB at startup (<c>ContentLoader</c>) and clients populate it from the
/// catalog synced over Mirror (<c>ContentCatalog</c>) — abilities need client sync because
/// <see cref="HotbarUI"/> reads this registry to label hotbar slots, unlike the server-only content
/// types (conversations/vendors/mobs/factions/loot). The <see cref="Get"/> API is unchanged from the
/// pre-2.9 Resources-backed version — only where the data comes from moved.
/// </summary>
public class AbilityRegistry : MonoBehaviour
{
    public static AbilityRegistry Instance { get; private set; }

    readonly Dictionary<string, AbilityDefinition> _abilities = new();

    void Awake() => Instance = this;

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Register(AbilityDefinition def)
    {
        if (def != null && !string.IsNullOrEmpty(def.abilityId))
            _abilities[def.abilityId] = def;
    }

    public AbilityDefinition Get(string abilityId)
        => string.IsNullOrEmpty(abilityId) ? null : _abilities.GetValueOrDefault(abilityId);

    /// <summary>
    /// Replace the registry contents from DB-backed snapshots — building a runtime
    /// <see cref="AbilityDefinition"/> instance per row (mirrors <c>ItemRegistry.LoadFrom</c>). Used by
    /// the server (from the DB) and by clients (from the synced catalog), so both sides build
    /// identical definitions.
    /// </summary>
    public void LoadFrom(IEnumerable<AbilitySnapshot> snapshots)
    {
        _abilities.Clear();
        foreach (var s in snapshots)
            Register(Build(s));
    }

    static AbilityDefinition Build(AbilitySnapshot s)
    {
        var def = ScriptableObject.CreateInstance<AbilityDefinition>();
        def.name          = string.IsNullOrEmpty(s.DisplayName) ? s.AbilityId : s.DisplayName;
        def.abilityId     = s.AbilityId;
        def.displayName   = s.DisplayName;
        def.description   = s.Description;
        def.targetingType = (AbilityTargetType)s.TargetingType;
        def.range          = s.Range;
        def.castTime       = s.CastTime;
        def.manaCost       = s.ManaCost;
        def.animTrigger    = s.AnimTrigger;

        def.tags = new List<AbilityTag>();
        foreach (var t in s.Tags ?? new List<AbilityTagRefSnapshot>())
            def.tags.Add(BuildTag(t.TagId, t.DisplayName));

        def.cooldownLinks = new List<CooldownLink>();
        foreach (var l in s.CooldownLinks ?? new List<AbilityCooldownLinkSnapshot>())
            def.cooldownLinks.Add(new CooldownLink { tag = BuildTag(l.TagId, l.TagDisplayName), duration = l.Duration });

        def.effects = new List<AbilityEffect>();
        foreach (var e in s.Effects ?? new List<AbilityEffectSnapshot>())
        {
            var fx = BuildEffect(e);
            if (fx != null) def.effects.Add(fx);
        }

        return def;
    }

    static AbilityTag BuildTag(string tagId, string displayName)
    {
        var tag = ScriptableObject.CreateInstance<AbilityTag>();
        tag.tagId      = tagId;
        tag.displayName = displayName;
        return tag;
    }

    static AbilityEffect BuildEffect(AbilityEffectSnapshot e)
    {
        switch (e.EffectType)
        {
            case "damage":
            {
                var fx = ScriptableObject.CreateInstance<DamageEffect>();
                fx.baseDamage    = e.BaseAmount;
                fx.scalingStat   = (ScalingStatType)e.ScalingStat;
                fx.scalingFactor = e.ScalingFactor;
                return fx;
            }
            case "heal":
            {
                var fx = ScriptableObject.CreateInstance<HealEffect>();
                fx.baseHeal      = e.BaseAmount;
                fx.scalingStat   = (ScalingStatType)e.ScalingStat;
                fx.scalingFactor = e.ScalingFactor;
                return fx;
            }
            default:
                Debug.LogWarning($"[AbilityRegistry] Unknown effect_type '{e.EffectType}' — skipped.");
                return null;
        }
    }
}
