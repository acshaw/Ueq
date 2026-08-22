using Mirror;
using UnityEngine;

/// <summary>
/// A player's trained Offense value — feeds Step 1's ATK the same way <see cref="PlayerWeaponSkills"/>
/// does (design doc §2.10-adjacent; <see cref="CombatResolver.BuildCombatant"/>). Unlike WeaponSkill,
/// this is a single value, not split Might/Finesse — a general combat-aptitude stat, not tied to a
/// specific weapon category. Starts at 1, capped at <c>level × CapPerLevel</c> (no flat base, unlike
/// WeaponSkill's <c>level×5+5</c>), earned through use — mirrors SK4's rise-on-swing mechanic exactly.
///
/// Replaces the earlier (2026-08-11) design where Offense was a fixed universal
/// <c>level × OffensePerLevel</c> formula with no persisted state at all — that made Offense
/// indistinguishable from Level itself, not an independent lever a player could train.
/// </summary>
public class PlayerOffense : NetworkBehaviour
{
    // OF-equivalent of SK3 — cap(level) = level × capPerLevel. No flat base term (unlike WeaponSkill's
    // capBase + capPerLevel×(level−1)) — per design intent, Offense caps at exactly level×5, not level×5+5.
    [SerializeField] int _capPerLevel = 5;

    // OF-equivalent of SK4 — flat rise-on-use chance per swing while under cap.
    [SerializeField] float _riseChance = 0.08f;

    [SyncVar] int _offense;

    PlayerExperience _exp;

    public int Value => _offense;

    void Awake() => _exp = GetComponent<PlayerExperience>();

    int Cap => _capPerLevel * Mathf.Max(1, _exp != null ? _exp.Level : 1);

    /// <summary>Called once per swing from the combat resolution path (OF4, mirrors SK4). Server-only.</summary>
    [Server]
    public void RollOffenseUp()
    {
        if (_offense >= Cap) return;
        if (Random.value < _riseChance) _offense++;
    }

    // ── Persistence ──────────────────────────────────────────────────────────────

    [Server]
    public void LoadState(int offense)
    {
        _offense = Mathf.Max(0, offense);
    }
}
