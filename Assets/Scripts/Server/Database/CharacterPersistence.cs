using Mirror;
using UnityEngine;

/// <summary>
/// Server-only coordinator that makes a player's character survive a restart (1.3). On spawn it loads
/// the character for the connection's authenticated account (1.4 seam) and re-applies it to the live
/// components; saves are driven by <see cref="GameNetworkManager"/> on disconnect/stop (where the
/// ordering relative to the persistence worker's flush is controlled).
///
/// Iron rule (1.2): the worker only ever touches the plain <see cref="CharacterSnapshot"/> — capture
/// happens here on the main thread, and a loaded snapshot is re-applied here on the main thread.
/// </summary>
public class CharacterPersistence : NetworkBehaviour
{
    readonly CharacterRepository _repo = new CharacterRepository();

    long   _accountId;
    long   _characterId;   // which character this player is (1.6 — saves key off this, not the account)
    bool   _loaded;        // load/create has resolved (applied a row, created one, or confirmed none)
    string _characterName = "";

    // Cached siblings (server-only logic; all live on the same player object).
    PlayerExperience    _exp;
    PlayerInventory     _inv;
    PlayerEquipment     _equip;
    PlayerFactionScores _faction;
    PlayerAbilities     _abilities;
    Health              _health;
    PlayerMana          _mana;
    PlayerWeaponSkills  _weaponSkills;
    NetworkedPlayer     _player;
    Nameplate           _nameplate;

    void Awake()
    {
        _exp          = GetComponent<PlayerExperience>();
        _inv          = GetComponent<PlayerInventory>();
        _equip        = GetComponent<PlayerEquipment>();
        _faction      = GetComponent<PlayerFactionScores>();
        _abilities    = GetComponent<PlayerAbilities>();
        _health       = GetComponent<Health>();
        _mana         = GetComponent<PlayerMana>();
        _weaponSkills = GetComponent<PlayerWeaponSkills>();
        _player       = GetComponent<NetworkedPlayer>();
        _nameplate    = GetComponent<Nameplate>();
    }

    // ── Load on spawn ────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        if (connectionToClient?.authenticationData is AccountSession session)
        {
            _accountId = session.AccountId;
        }
        else
        {
            Debug.LogWarning("[Persist] Player has no account session — character will not persist.");
            return;
        }

        if (PersistenceService.Instance == null)
        {
            Debug.LogError("[Persist] PersistenceService missing — character will not load/save.");
            return;
        }

        // `session` is bound + guaranteed non-null here (the else above returns).

        // A pending creation (set by CharacterSelectController) means this is a brand-new character —
        // seed it from the chosen race/class/name instead of loading. Its character_id was assigned at
        // creation (1.6, decision O2); the immediate save fills the row (create = enter).
        if (session.PendingCreation != null)
        {
            var pc = session.PendingCreation;
            session.PendingCreation = null;       // consume so a later respawn doesn't re-create
            _characterId = pc.CharacterId;
            InitializeNewCharacter(pc.Name, pc.Gender, pc.Race, pc.Class);
            return;
        }

        // Existing character: load the one the player selected (1.6 — keyed by character id).
        _characterId = session.SelectedCharacterId;
        long characterId = _characterId; // capture for the worker delegate
        PersistenceService.Instance.LoadAsync(conn => _repo.Load(conn, characterId), ApplyOrInitialize);
    }

    // Main thread (1.2 pump). Runs after every sibling's OnStartServer has set defaults.
    void ApplyOrInitialize(CharacterSnapshot snap)
    {
        if (this == null || !isServer) return; // player despawned mid-load

        if (snap == null)
        {
            // Should not happen in the normal create/enter flow — the player reaches here only via
            // EnterWorld, which validated a character exists. Warn loudly; fall back to the prefab
            // _defaultRace/_defaultClass already applied by PlayerExperience.OnStartServer (decision D5).
            Debug.LogWarning(
                $"[Persist] Account #{_accountId} spawned with no pending creation and no saved character — " +
                "using the prefab default race/class fallback. This should not happen in the normal flow.");
            _characterId = 0; // no real row — block saves so we don't UPDATE a nonexistent character
        }
        else
        {
            ApplySnapshot(snap);
        }
        _loaded = true;
    }

    // Brand-new character (decision D2). Its row + character_id already exist (CreateIdentity at
    // creation, 1.6); seed the live components from the chosen race/class/name, keep the Mirror
    // spawn-point position, then save to fill the row with full state.
    [Server]
    void InitializeNewCharacter(string name, Gender gender, string raceName, string className)
    {
        var race = RaceClassRegistry.GetRace(raceName);
        var cls  = RaceClassRegistry.GetClass(className);
        if (cls == null)
            Debug.LogWarning($"[Persist] New character class '{className}' not found in RaceClassRegistry.");

        _exp?.LoadState(0, race, cls);             // base stats, max HP/mana, known abilities + default hotbar
        _exp?.SetGender(gender);                   // 3.1.4 — synced identity for the body model
        _faction?.Initialize(raceName);            // racial faction defaults for the chosen race
        ApplyName(name);
        _health?.ResetToFull();
        if (_mana != null) _mana.SetCurrent(_mana.Max);
        _player?.SetBindPoint(transform.position); // bind at the spawn point; keep current position
        // M3.0 Stage C: a new character spawns at the Mirror start position, which lives in the base
        // (starter) zone. Record it so the first save round-trips the correct zone id.
        if (ZoneManager.Instance != null) _player?.SetZone(ZoneManager.Instance.StarterZoneId);

        _loaded = true;
        Debug.Log($"[Persist] Created character '{name}' ({raceName} {className}) for account #{_accountId}.");
        Save();                                    // immediate save creates the row
        SendMotd();
    }

    // Welcome line on entering the world (1.6.1, D4 — hardcoded for now). Sent after the name is set;
    // the client cleared its chat on the prior camp/despawn, so this lands on a fresh log.
    void SendMotd()
    {
        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.System, "System",
                $"Welcome to Ueq, {_characterName}! Type /help for commands."),
            connectionToClient);
    }

    void ApplyName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        _characterName  = name;
        gameObject.name = name;       // server-side chat identity (e.g. "{name} casts …")
        _nameplate?.SetLabel(name);   // SyncVar → clients see the floating label
    }

    void ApplySnapshot(CharacterSnapshot s)
    {
        // Order matters: XP + race/class first (rebuilds base stats, max HP/mana, known abilities),
        // then equipment bonuses on top, then faction/hotbar/inventory, then current HP/mana after the
        // maxes have settled, then position.
        var race = RaceClassRegistry.GetRace(s.RaceName);
        var cls  = RaceClassRegistry.GetClass(s.ClassName);
        if (cls == null)
            Debug.LogWarning($"[Persist] Class '{s.ClassName}' not found in RaceClassRegistry — derived stats may be wrong.");

        _characterId = s.CharacterId;

        _exp?.LoadState(s.TotalXp, race, cls);
        _exp?.SetGender(s.Gender);                 // 3.1.4 — synced identity for the body model
        _equip?.LoadState(s.Equipment);
        _faction?.LoadState(s.ActualRace, s.ApparentRace, s.FactionScores);
        _abilities?.LoadHotbar(s.Hotbar);
        _inv?.LoadState(s.Inventory, s.Copper, s.Silver, s.Gold, s.Platinum);

        // O6 safety net: current_health <= 0 means an identity row that was never fully initialized
        // (e.g. disconnect between create and first save) — fill to max rather than spawn dead. A live
        // character never logs out at 0 HP (death respawns to full), so this is harmless otherwise.
        if (s.CurrentHealth <= 0) _health?.ResetToFull();
        else                      _health?.SetCurrent(s.CurrentHealth);
        _mana?.SetCurrent(s.CurrentMana);
        _weaponSkills?.LoadState(s.MightSkill, s.FinesseSkill);

        // M3.0 Stage C: place the player into their persisted zone at their saved position. When ZoneManager
        // is active it owns scene assignment + the client's additive scene load + the warp, so we set the
        // bind point separately and route placement through it. Zones-disabled path keeps the single-scene
        // LoadState (sets bind + teleports).
        var pos  = new Vector3(s.PosX, s.PosY, s.PosZ);
        var bind = new Vector3(s.BindX, s.BindY, s.BindZ);
        _player?.SetZone(s.ZoneId);
        if (ZoneManager.Instance != null && connectionToClient != null)
        {
            _player?.SetBindPoint(bind);
            ZoneManager.Instance.ServerPlaceInZone(connectionToClient, s.ZoneId, pos, s.Yaw);
        }
        else
        {
            _player?.LoadState(pos, s.Yaw, bind);
        }
        ApplyName(s.Name);

        Debug.Log($"[Persist] Loaded character for account #{_accountId} (level {_exp?.Level}).");
        SendMotd();
    }

    // ── Save ──────────────────────────────────────────────────────────────────────

    /// <summary>Capture and enqueue a save. No-ops if there's no account, the load hasn't finished
    /// (so we never clobber real data with defaults), or the persistence worker is shutting down.</summary>
    [Server]
    public void Save()
    {
        if (_characterId == 0) return; // no resolved character (e.g. warned fallback) — nothing to save
        if (!_loaded)
        {
            Debug.LogWarning($"[Persist] Skipping save for character #{_characterId} — load not finished (won't clobber).");
            return;
        }

        var svc = PersistenceService.Instance;
        if (svc == null || !svc.IsRunning) return;

        var snap = CaptureSnapshot();
        svc.EnqueueSave(new KeyedDelegateSaveJob("character:" + _characterId, (c, tx) => _repo.Upsert(c, tx, snap)));
    }

    // Main thread: read every sibling into a plain snapshot (the only object the worker touches).
    CharacterSnapshot CaptureSnapshot()
    {
        var s = new CharacterSnapshot { AccountId = _accountId, CharacterId = _characterId, Name = _characterName ?? "" };

        if (_exp != null)
        {
            s.TotalXp   = _exp.TotalXp;
            s.Gender    = _exp.Gender;
            s.RaceName  = _exp.CurrentRace  != null ? _exp.CurrentRace.raceName   : "";
            s.ClassName = _exp.CurrentClass != null ? _exp.CurrentClass.className : "";
        }

        if (_inv != null)
        {
            s.Inventory = _inv.ExportSlots();
            s.Copper    = _inv.Copper;
            s.Silver    = _inv.Silver;
            s.Gold      = _inv.Gold;
            s.Platinum  = _inv.Platinum;
        }

        if (_equip != null)     s.Equipment     = _equip.ExportSlots();
        if (_abilities != null) s.Hotbar        = _abilities.ExportHotbar();
        if (_faction != null)
        {
            s.FactionScores = _faction.ExportScores();
            s.ActualRace    = _faction.ActualRace;
            s.ApparentRace  = _faction.ApparentRace;
        }

        if (_health != null) s.CurrentHealth = _health.Current;
        if (_mana   != null) s.CurrentMana   = _mana.Current;
        if (_weaponSkills != null)
        {
            s.MightSkill   = _weaponSkills.Might;
            s.FinesseSkill = _weaponSkills.Finesse;
        }

        var pos  = transform.position;
        var bind = _player != null ? _player.BindPoint : pos;
        s.PosX = pos.x;  s.PosY = pos.y;  s.PosZ = pos.z;
        s.Yaw  = _player != null ? _player.Yaw : transform.eulerAngles.y;
        s.BindX = bind.x; s.BindY = bind.y; s.BindZ = bind.z;

        if (_player != null) s.ZoneId = _player.CurrentZoneId;

        return s;
    }
}
