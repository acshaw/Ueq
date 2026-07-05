using Mirror;
using UnityEngine;

/// <summary>
/// Server-side handler for the pre-spawn character-select flow (1.5, decision D6 — dedicated
/// controller on the NetworkManager GameObject). With <c>autoCreatePlayer = false</c>, the player does
/// not spawn on connect; instead the authenticated client requests its character list and then either
/// creates-and-enters a new character or enters an existing one. This controller validates each
/// request (server-authoritative) and spawns the player via <see cref="NetworkServer.AddPlayerForConnection"/>.
///
/// DB work runs off the main thread via 1.2's <see cref="PersistenceService"/>; validation that needs
/// Unity assets (race/class lookup, level math) happens on the marshaled callback.
/// Lifecycle is driven by <see cref="GameNetworkManager"/> (OnServerStarted/OnServerStopped).
/// </summary>
public class CharacterSelectController : MonoBehaviour
{
    readonly CharacterRepository _repo = new CharacterRepository();

    const int NameMin = 2, NameMax = 20;
    public const int MaxCharacters = 8; // slot cap (1.6, decision O1)

    // ── Lifecycle (called from GameNetworkManager) ───────────────────────────────

    public void OnServerStarted()
    {
        NetworkServer.RegisterHandler<CharacterListRequest>(OnListRequest);
        NetworkServer.RegisterHandler<CreateCharacterMessage>(OnCreate);
        NetworkServer.RegisterHandler<EnterWorldMessage>(OnEnter);
        NetworkServer.RegisterHandler<DeleteCharacterMessage>(OnDelete);
        NetworkServer.RegisterHandler<CampMessage>(OnCamp);
    }

    public void OnServerStopped()
    {
        NetworkServer.UnregisterHandler<CharacterListRequest>();
        NetworkServer.UnregisterHandler<CreateCharacterMessage>();
        NetworkServer.UnregisterHandler<EnterWorldMessage>();
        NetworkServer.UnregisterHandler<DeleteCharacterMessage>();
        NetworkServer.UnregisterHandler<CampMessage>();
    }

    static long AccountIdOf(NetworkConnectionToClient conn)
        => conn.authenticationData is AccountSession s ? s.AccountId : 0;

    static bool StillConnected(NetworkConnectionToClient conn)
        => NetworkServer.connections.ContainsValue(conn);

    // ── List ─────────────────────────────────────────────────────────────────────

    void OnListRequest(NetworkConnectionToClient conn, CharacterListRequest _) => SendList(conn);

    void SendList(NetworkConnectionToClient conn)
    {
        long accountId = AccountIdOf(conn);
        if (accountId == 0 || PersistenceService.Instance == null) return;
        PersistenceService.Instance.LoadAsync(c => _repo.ListByAccount(c, accountId),
            rows => BuildAndSendList(conn, rows));
    }

    // Main thread: derive level (touches the Resources XP table) + attach creation options, then send.
    void BuildAndSendList(NetworkConnectionToClient conn, System.Collections.Generic.List<CharacterRepository.CharacterRow> rows)
    {
        if (!StillConnected(conn)) return;

        var entries = new CharacterListEntry[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var row  = rows[i];
            var race = RaceClassRegistry.GetRace(row.Race);
            var cls  = RaceClassRegistry.GetClass(row.Class);
            float mod = (race != null ? race.xpModifier : 1f) * (cls != null ? cls.xpModifier : 1f);
            entries[i] = new CharacterListEntry
            {
                id = row.Id, name = row.Name, gender = row.Gender.ToString(), race = row.Race, cls = row.Class,
                level = PlayerExperience.ComputeLevel(row.TotalXp, mod),
            };
        }

        conn.Send(new CharacterListMessage
        {
            entries       = entries,
            raceOptions   = RaceClassRegistry.AllRaceNames(),   // legacy (retired IMGUI select UI)
            classOptions  = RaceClassRegistry.AllClassNames(),  // legacy (retired IMGUI select UI)
            createOptions = CharacterRosterRegistry.AllOptions(), // 3.1.4 — the gated gender→race→class lineup
            maxSlots      = MaxCharacters,
        });
    }

    // ── Create = enter (D2) ────────────────────────────────────────────────────────

    void OnCreate(NetworkConnectionToClient conn, CreateCharacterMessage msg)
    {
        long accountId = AccountIdOf(conn);
        if (accountId == 0 || PersistenceService.Instance == null) { Reject(conn, "Not logged in."); return; }

        string name = (msg.name ?? "").Trim();
        if (name.Length < NameMin || name.Length > NameMax || !IsValidName(name))
        {
            Reject(conn, $"Name must be {NameMin}–{NameMax} letters.");
            return;
        }

        // Race/class lookup needs Unity assets → validate on the main thread (here) before going off-thread.
        var race = RaceClassRegistry.GetRace(msg.race);
        var cls  = RaceClassRegistry.GetClass(msg.cls);
        if (race == null || cls == null) { Reject(conn, "Invalid race or class."); return; }

        // 3.1.4 — the roster is the availability authority: the chosen gender/race/class must be a legal,
        // authored combination (gates out e.g. a male-human Cleric or a female Dwarf). Default an
        // empty/garbled gender to Male so an old/legacy client can't wedge the parse.
        if (!System.Enum.TryParse(msg.gender, out Gender gender)) gender = Gender.Male;
        if (!CharacterRosterRegistry.IsValid(gender, race.raceName, cls.className))
        {
            Reject(conn, "That gender/race/class combination isn't available.");
            return;
        }

        string nameLower = name.ToLowerInvariant();
        string raceName  = race.raceName;
        string className = cls.className;

        // One off-thread op: enforce the slot cap (O1) + unique name (D4), then create the identity
        // row (O2) and return its id. The name-unique index is the real guard — catch its violation
        // and surface a friendly error rather than letting LoadAsync swallow the exception.
        PersistenceService.Instance.LoadAsync(
            c =>
            {
                try
                {
                    if (_repo.CountByAccount(c, accountId) >= MaxCharacters)
                        return new CreateResult { Error = "All character slots are full." };
                    if (_repo.NameExists(c, nameLower))
                        return new CreateResult { Error = "That name is already taken." };
                    long id = _repo.CreateIdentity(c, accountId, name, gender.ToString(), raceName, className);
                    return new CreateResult { CharacterId = id };
                }
                catch (System.Exception)
                {
                    return new CreateResult { Error = "That name is already taken." };
                }
            },
            result =>
            {
                if (!StillConnected(conn)) return;
                if (result.Error != null) { Reject(conn, result.Error); return; }

                if (conn.authenticationData is AccountSession session)
                    session.PendingCreation = new PendingCharacter
                    {
                        CharacterId = result.CharacterId,
                        Name = name, Gender = gender, Race = raceName, Class = className,
                    };
                SpawnPlayer(conn);
            });
    }

    struct CreateResult { public long CharacterId; public string Error; }

    // ── Enter existing ─────────────────────────────────────────────────────────────

    void OnEnter(NetworkConnectionToClient conn, EnterWorldMessage msg)
    {
        long accountId = AccountIdOf(conn);
        if (accountId == 0 || PersistenceService.Instance == null) { Reject(conn, "Not logged in."); return; }

        PersistenceService.Instance.LoadAsync(c => _repo.ListByAccount(c, accountId),
            rows =>
            {
                if (!StillConnected(conn)) return;
                bool owns = false;
                foreach (var r in rows) if (r.Id == msg.characterId) { owns = true; break; }
                if (!owns) { Reject(conn, "Character not found."); return; }

                if (conn.authenticationData is AccountSession session)
                    session.SelectedCharacterId = msg.characterId; // recorded; 1.5 loads by account
                SpawnPlayer(conn);
            });
    }

    // ── Delete (D7) ──────────────────────────────────────────────────────────────

    void OnDelete(NetworkConnectionToClient conn, DeleteCharacterMessage msg)
    {
        long accountId = AccountIdOf(conn);
        if (accountId == 0 || PersistenceService.Instance == null) return;

        long characterId = msg.characterId;
        // Delete (account-guarded) then re-list in one off-thread op so the refreshed list reflects the
        // deletion (avoids a write-queue vs. read race). One-off user action, fine on the load path.
        PersistenceService.Instance.LoadAsync(
            c => { _repo.DeleteById(c, accountId, characterId); return _repo.ListByAccount(c, accountId); },
            rows => BuildAndSendList(conn, rows));
    }

    // ── Camp: save + despawn + back to character select (O4) ─────────────────────

    void OnCamp(NetworkConnectionToClient conn, CampMessage _)
    {
        var player = conn.identity;
        if (player == null) return;

        // Authoritative combat re-check (1.6.1) — never trust the client's countdown.
        var combat = player.GetComponent<CombatState>();
        if (combat != null && combat.InCombat)
        {
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.System, "System", "You can't camp while in combat."), conn);
            return;
        }

        player.GetComponent<CharacterPersistence>()?.Save(); // persist before despawn
        if (conn.authenticationData is AccountSession session)
            session.SelectedCharacterId = 0;

        // Destroy the player object and free the connection's player slot; the client's localPlayer
        // goes null → CharacterSelectUI reappears and re-requests the roster.
        NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
        Debug.Log($"[CharSelect] Account #{AccountIdOf(conn)} camped to character select.");
    }

    // ── Spawn ──────────────────────────────────────────────────────────────────────

    void SpawnPlayer(NetworkConnectionToClient conn)
    {
        var nm = NetworkManager.singleton;
        if (nm == null || nm.playerPrefab == null)
        {
            Debug.LogError("[CharSelect] No NetworkManager/playerPrefab — cannot spawn player.");
            Reject(conn, "Server error.");
            return;
        }

        Transform start = nm.GetStartPosition();
        GameObject player = start != null
            ? Instantiate(nm.playerPrefab, start.position, start.rotation)
            : Instantiate(nm.playerPrefab);

        NetworkServer.AddPlayerForConnection(conn, player);
        Debug.Log($"[CharSelect] Spawned player for account #{AccountIdOf(conn)}.");
    }

    static void Reject(NetworkConnectionToClient conn, string error)
        => conn.Send(new CharacterActionResult { ok = false, error = error });

    static bool IsValidName(string s)
    {
        foreach (char c in s) if (!char.IsLetter(c)) return false;
        return true;
    }
}
