using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Server-only lookup of mobs by id (M2.5). Builds a runtime <see cref="MobDefinition"/> per DB row —
/// the same type the SO path used — so <c>MobApplicator</c> and the systems that read it
/// (Health/EnemyAI/NpcFaction/Corpse/MobKillReward/VendorApplicator/NpcConversation) are unchanged;
/// only the source moved off ScriptableObjects. No client sync (mobs reach clients as spawned objects).
///
/// References resolve by id: prefab → a registered Mirror spawnable (by name); conversation/vendor →
/// their registries (live now); faction/loot → their registries (live at 2.6/2.7, null until then).
/// Populated by <c>ContentLoader</c> after the other content loads.
/// </summary>
public static class MobRegistry
{
    static readonly Dictionary<string, MobDefinition> _byId = new();

    public static void LoadFrom(IEnumerable<MobSnapshot> snapshots)
    {
        _byId.Clear();
        foreach (var s in snapshots)
            if (!string.IsNullOrEmpty(s.MobId))
                _byId[s.MobId] = Build(s);
    }

    public static MobDefinition Get(string mobId)
        => string.IsNullOrEmpty(mobId) ? null : _byId.GetValueOrDefault(mobId);

    public static int Count => _byId.Count;

    static MobDefinition Build(MobSnapshot s)
    {
        var def = ScriptableObject.CreateInstance<MobDefinition>();
        def.name               = s.MobId;
        def.displayName        = s.DisplayName;
        def.mobLevel           = s.MobLevel;
        def.prefab             = ResolvePrefab(s.PrefabAddress, s.MobId);

        def.maxHealth          = s.MaxHealth;
        def.attackDamage       = s.AttackDamage;
        def.attackInterval     = s.AttackInterval;
        def.attackRange        = s.AttackRange;

        def.movementType       = (MovementType)s.MovementType;
        def.moveSpeed          = s.MoveSpeed;
        def.wanderRadius       = s.WanderRadius;
        def.wanderPauseMin     = s.WanderPauseMin;
        def.wanderPauseMax     = s.WanderPauseMax;

        def.perceptionRadius   = s.PerceptionRadius;
        def.baseAggroThreat    = s.BaseAggroThreat;

        def.factionId          = s.FactionId;
        def.faction            = FactionRegistry.Get(s.FactionId);   // null until 2.6
        def.aggroMaxStanding   = s.AggroMaxStanding;
        def.warningMaxStanding = s.WarningMaxStanding;

        def.conversationSetId  = s.ConversationSetId;                // NpcConversation resolves via ConversationRegistry
        def.lootTableId        = s.LootTableId;
        def.lootTable          = LootRegistry.Get(s.LootTableId);    // M2.7 (null if unknown → no drops)
        def.xpReward           = s.XpReward;

        def.vendorId           = s.VendorId;                         // VendorApplicator resolves via VendorRegistry
        def.vendorOpenKeyword  = s.VendorOpenKeyword;

        // 5.1.1/5.1.2/2.12(SK5) — combat pipeline data.
        def.weaponCategory     = (WeaponCategory)s.WeaponCategory;
        def.weaponSkill        = s.WeaponSkill;
        def.combatTable        = new CombatTierTable
        {
            Miss = s.TierMiss, Glancing = s.TierGlancing, Hit = s.TierHit, SolidHit = s.TierSolid,
            GoodHit = s.TierGood, Critical = s.TierCritical, Crippling = s.TierCrippling,
        };
        def.attackIsParryable  = s.AttackIsParryable;
        def.avoidanceAgility   = s.AvoidanceAgility;
        def.avoidanceDexterity = s.AvoidanceDexterity;

        // 5.4 (AG3) — social aggro.
        def.socialAggroEnabled = s.SocialAggroEnabled;
        def.socialAggroRadius  = s.SocialAggroRadius;

        // M2.7.1: faction hits on kill — resolve each faction by id (factions load before mobs).
        def.factionHits = new List<MobDefinition.FactionHit>();
        if (s.FactionHits != null)
            foreach (var h in s.FactionHits)
                def.factionHits.Add(new MobDefinition.FactionHit
                {
                    factionId = h.FactionId,
                    faction   = FactionRegistry.Get(h.FactionId),
                    delta     = h.Delta,
                });
        return def;
    }

    // Mobs share a small set of registered Mirror spawnable prefabs (mostly "Enemy"); a DB
    // prefab_address names one of them. Resolve by GameObject name from the NetworkManager's list.
    static GameObject ResolvePrefab(string address, string mobId)
    {
        if (string.IsNullOrEmpty(address)) return null;
        var nm = NetworkManager.singleton;
        if (nm != null)
            foreach (var p in nm.spawnPrefabs)
                if (p != null && p.name == address)
                    return p;
        Debug.LogWarning($"[Content] Mob '{mobId}' references prefab '{address}' which is not a " +
                         "registered spawnable prefab — it cannot be spawned until it is registered.");
        return null;
    }
}
