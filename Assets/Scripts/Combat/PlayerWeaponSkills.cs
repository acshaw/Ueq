using Mirror;
using UnityEngine;

/// <summary>
/// 2.12 — a player's two weapon-proficiency values (Might/Finesse), feeding Step 1's Skill Differential
/// modifier (design doc §2.8). Earned through use (SK4), unlike <see cref="CharacterStats"/>' derived
/// stats — this is genuinely persisted state, like XP.
/// </summary>
public class PlayerWeaponSkills : NetworkBehaviour
{
    // SK3 — skillCap(level) = base + perLevel × (level − 1). Tunable, not locked by the design doc.
    [SerializeField] int _capBase     = 5;
    [SerializeField] int _capPerLevel = 5;

    // SK4 — flat rise-on-use chance per swing while under cap. Not specified by the design doc — a
    // simplest-defensible first pass, tune or replace with a decay-as-you-approach-cap curve later.
    [SerializeField] float _riseChance = 0.08f;

    [SyncVar] int _might;
    [SyncVar] int _finesse;

    PlayerExperience _exp;

    public int Might   => _might;
    public int Finesse => _finesse;

    void Awake() => _exp = GetComponent<PlayerExperience>();

    public int For(WeaponCategory cat) => cat == WeaponCategory.Might ? _might : _finesse;

    int Cap => _capBase + _capPerLevel * Mathf.Max(0, (_exp != null ? _exp.Level : 1) - 1);

    /// <summary>Called once per swing from the combat resolution path (SK4). Server-only.</summary>
    [Server]
    public void RollSkillUp(WeaponCategory cat)
    {
        int cap = Cap;
        if (cat == WeaponCategory.Might)
        {
            if (_might >= cap) return;
            if (Random.value < _riseChance) _might++;
        }
        else
        {
            if (_finesse >= cap) return;
            if (Random.value < _riseChance) _finesse++;
        }
    }

    // ── Persistence (SK2) ────────────────────────────────────────────────────────

    [Server]
    public void LoadState(int might, int finesse)
    {
        _might   = Mathf.Max(0, might);
        _finesse = Mathf.Max(0, finesse);
    }
}
