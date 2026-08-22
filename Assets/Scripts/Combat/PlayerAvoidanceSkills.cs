using Mirror;
using UnityEngine;

/// <summary>
/// A player's Avoidance-side trained values (2026-08-13 follow-up to 5.1.5). Mirrors
/// <see cref="PlayerWeaponSkills"/>/<see cref="PlayerOffense"/> exactly, but bundles four related
/// values instead of one or two:
///
/// - <see cref="Defense"/> — mirrors <see cref="PlayerOffense"/> exactly (starts at 1, capped at
///   level × 5, no flat base). Combines with Agility into <c>AvoidanceBase</c>
///   (<see cref="CombatResolver.BuildCombatant"/>), which feeds Dodge only.
/// - <see cref="Dodge"/> — mirrors <see cref="PlayerWeaponSkills"/>'s per-skill shape (starts at 1,
///   capped at level×5+5). Added on top of AvoidanceBase — Dodge works even before this is trained,
///   since AvoidanceBase alone (Agility + Defense) already gives a nonzero chance. An innate/reflexive
///   check.
/// - <see cref="Parry"/> / <see cref="Riposte"/> — same cap shape as Dodge, but stand ALONE with no
///   AvoidanceBase contribution — a genuinely untrained character cannot Parry or Riposte at all
///   (~0% at value 0, the avoidance curve's floor). Fixes the pre-existing bug where Parry and Riposte
///   were mechanically identical (both read the same DEX-derived value).
///
/// All four train the same way weapon skill does — a flat per-attempt chance to rise while under cap —
/// but on the DEFENDER's side: rolled by whichever script resolves an attack against this player
/// (<see cref="PlayerAutoAttack"/> when attacked by another player, <c>EnemyAI</c> when attacked by a
/// mob), not by this component itself.
/// </summary>
public class PlayerAvoidanceSkills : NetworkBehaviour
{
    // Defense cap(level) = level × capPerLevel — mirrors PlayerOffense's cap shape exactly.
    [SerializeField] int _defenseCapPerLevel = 5;

    // Dodge/Parry/Riposte cap(level) = capBase + capPerLevel × (level−1) — mirrors PlayerWeaponSkills'
    // SK3 cap shape exactly (level×5+5 with these constants).
    [SerializeField] int _skillCapBase     = 10;
    [SerializeField] int _skillCapPerLevel = 5;

    [SerializeField] float _riseChance = 0.08f;

    [SyncVar] int _defense;
    [SyncVar] int _dodge;
    [SyncVar] int _parry;
    [SyncVar] int _riposte;

    PlayerExperience _exp;

    public int Defense => _defense;
    public int Dodge   => _dodge;
    public int Parry   => _parry;
    public int Riposte => _riposte;

    void Awake() => _exp = GetComponent<PlayerExperience>();

    int Level => _exp != null ? _exp.Level : 1;

    int DefenseCap => _defenseCapPerLevel * Mathf.Max(1, Level);
    int SkillCap    => _skillCapBase + _skillCapPerLevel * Mathf.Max(0, Level - 1);

    [Server] public void RollDefenseUp() { if (_defense < DefenseCap && Random.value < _riseChance) _defense++; }
    [Server] public void RollDodgeUp()   { if (_dodge   < SkillCap   && Random.value < _riseChance) _dodge++; }
    [Server] public void RollParryUp()   { if (_parry   < SkillCap   && Random.value < _riseChance) _parry++; }
    [Server] public void RollRiposteUp() { if (_riposte < SkillCap   && Random.value < _riseChance) _riposte++; }

    // ── Persistence ──────────────────────────────────────────────────────────────

    [Server]
    public void LoadState(int defense, int dodge, int parry, int riposte)
    {
        _defense = Mathf.Max(0, defense);
        _dodge   = Mathf.Max(0, dodge);
        _parry   = Mathf.Max(0, parry);
        _riposte = Mathf.Max(0, riposte);
    }
}
