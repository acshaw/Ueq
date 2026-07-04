using System.Collections;
using System.Collections.Generic;
using Mirror;
using Npgsql;
using UnityEngine;

/// <summary>
/// Login handshake (1.4). A client must register or authenticate against the <c>accounts</c> table
/// before the connection is accepted. The credential check is blocking DB I/O, so it runs
/// <b>off the main thread</b> via <see cref="PersistenceService.LoadAsync{T}"/> and resolves the
/// connection on the marshaled callback — Mirror's tick is never blocked during connect.
///
/// On success the authenticated <see cref="AccountSession"/> (account id + in-memory session token)
/// is stashed in <c>conn.authenticationData</c> — the seam 1.3/1.5 read at player spawn.
/// Transport stays unencrypted at this stage (5.3 hardens); credentials cross the wire in clear.
/// </summary>
[AddComponentMenu("Ueq/Account Authenticator")]
public class AccountAuthenticator : NetworkAuthenticator
{
    public static AccountAuthenticator Instance { get; private set; }

    // ── Wire messages ─────────────────────────────────────────────────────────

    public struct AuthRequestMessage : NetworkMessage
    {
        public string username;
        public string password;
        public bool   register;   // true = create account if the name is free
    }

    public struct AuthResponseMessage : NetworkMessage
    {
        public byte   code;
        public string message;
    }

    public enum AuthCode : byte
    {
        Success        = 100,
        BadCredentials = 200,
        UsernameTaken  = 201,
        AlreadyOnline  = 202,
        BadInput       = 203,
        ServerError    = 204,
    }

    // Plain, Unity-free outcome that crosses the worker→main thread boundary.
    struct AuthOutcome
    {
        public AuthCode Code;
        public long     AccountId;
        public string   Username;
    }

    const int UsernameMin = 3, UsernameMax = 32, PasswordMin = 4, PasswordMax = 128;

    // ── Server state ──────────────────────────────────────────────────────────

    readonly AccountRepository _repo = new AccountRepository();
    readonly HashSet<NetworkConnectionToClient> _pending = new();
    readonly Dictionary<long, NetworkConnectionToClient> _onlineByAccount = new();

    // ── Client credentials (set by the login UI before StartClient/StartHost) ──

    [HideInInspector] public string clientUsername = "";
    [HideInInspector] public string clientPassword = "";
    [HideInInspector] public bool   clientRegister = false;

    /// <summary>Last server error/result text — the login UI reads this to show feedback.</summary>
    public static string LastClientMessage { get; private set; } = "";

    /// <summary>Clear the stored feedback text at the start of a new login/register attempt so a stale
    /// message (e.g. a prior "Success") can't linger on the login panel.</summary>
    public static void ClearClientMessage() => LastClientMessage = "";

    void Awake() => Instance = this;

    [RuntimeInitializeOnLoadMethod] // fast playmode without domain reload
    static void ResetStatics()
    {
        Instance = null;
        LastClientMessage = "";
    }

    // ── Server ─────────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        _pending.Clear();
        _onlineByAccount.Clear();
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequest, false);
    }

    public override void OnStopServer() => NetworkServer.UnregisterHandler<AuthRequestMessage>();

    public override void OnServerAuthenticate(NetworkConnectionToClient conn) { /* wait for the request */ }

    void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        if (_pending.Contains(conn)) return; // already processing this connection

        string username = (msg.username ?? "").Trim();
        string password = msg.password ?? "";

        if (username.Length < UsernameMin || username.Length > UsernameMax ||
            password.Length < PasswordMin || password.Length > PasswordMax)
        {
            SendResponse(conn, AuthCode.BadInput, "Invalid username or password length.");
            DelayedReject(conn);
            return;
        }

        if (PersistenceService.Instance == null)
        {
            SendResponse(conn, AuthCode.ServerError, "Server not ready.");
            DelayedReject(conn);
            return;
        }

        _pending.Add(conn);
        string normalized = username.ToLowerInvariant();
        bool register = msg.register;

        // Run the credential check off the main thread; resolve on the marshaled callback.
        PersistenceService.Instance.LoadAsync(
            conn2 => Authenticate(conn2, normalized, password, register),
            outcome => ResolveAuth(conn, outcome));
    }

    // Worker thread: only plain data + Npgsql, never the Mirror connection.
    AuthOutcome Authenticate(NpgsqlConnection conn, string username, string password, bool register)
    {
        if (register)
        {
            string hash = PasswordHasher.Hash(password);
            long? id = _repo.TryRegister(conn, username, hash);
            return id == null
                ? new AuthOutcome { Code = AuthCode.UsernameTaken }
                : new AuthOutcome { Code = AuthCode.Success, AccountId = id.Value, Username = username };
        }

        var found = _repo.FindByUsername(conn, username);
        if (found == null || !PasswordHasher.Verify(password, found.Value.hash))
            return new AuthOutcome { Code = AuthCode.BadCredentials };

        _repo.TouchLogin(conn, found.Value.id);
        return new AuthOutcome { Code = AuthCode.Success, AccountId = found.Value.id, Username = username };
    }

    // Main thread.
    void ResolveAuth(NetworkConnectionToClient conn, AuthOutcome outcome)
    {
        _pending.Remove(conn);
        if (!NetworkServer.connections.ContainsValue(conn)) return; // dropped mid-lookup

        if (outcome.Code != AuthCode.Success)
        {
            SendResponse(conn, outcome.Code, MessageFor(outcome.Code));
            DelayedReject(conn);
            return;
        }

        // Enforce single login per account (decision O4 — reject the newcomer). Self-heal against a stale
        // entry whose connection already dropped (a disconnect that didn't clean up), so a genuine re-login
        // isn't wrongly blocked as "already online".
        if (_onlineByAccount.TryGetValue(outcome.AccountId, out var existing))
        {
            bool stillOnline = existing != null && NetworkServer.connections.ContainsValue(existing);
            if (stillOnline)
            {
                SendResponse(conn, AuthCode.AlreadyOnline, "That account is already online.");
                DelayedReject(conn);
                return;
            }
            _onlineByAccount.Remove(outcome.AccountId); // previous session is gone — reclaim it
        }

        _onlineByAccount[outcome.AccountId] = conn;
        conn.authenticationData = new AccountSession
        {
            AccountId = outcome.AccountId,
            Username  = outcome.Username,
            Token     = System.Guid.NewGuid().ToString("N"),
        };

        SendResponse(conn, AuthCode.Success, "Success");
        ServerAccept(conn);
    }

    /// <summary>Called by <c>GameNetworkManager.OnServerDisconnect</c> to free single-login state.</summary>
    public void HandleServerDisconnect(NetworkConnectionToClient conn)
    {
        _pending.Remove(conn);
        if (conn.authenticationData is AccountSession s)
            _onlineByAccount.Remove(s.AccountId);
    }

    void SendResponse(NetworkConnectionToClient conn, AuthCode code, string message)
        => conn.Send(new AuthResponseMessage { code = (byte)code, message = message });

    // Reject after a short delay so the response message is delivered first.
    void DelayedReject(NetworkConnectionToClient conn)
    {
        conn.isAuthenticated = false;
        StartCoroutine(RejectAfter(conn, 0.75f));
    }

    IEnumerator RejectAfter(NetworkConnectionToClient conn, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ServerReject(conn);
    }

    static string MessageFor(AuthCode code) => code switch
    {
        AuthCode.BadCredentials => "Invalid username or password.",
        AuthCode.UsernameTaken  => "That username is already taken.",
        AuthCode.AlreadyOnline  => "That account is already online.",
        AuthCode.ServerError    => "Server error. Try again.",
        _                       => "Login failed.",
    };

    // ── Client ─────────────────────────────────────────────────────────────────

    public override void OnStartClient()
        => NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponse, false);

    public override void OnStopClient() => NetworkClient.UnregisterHandler<AuthResponseMessage>();

    public override void OnClientAuthenticate()
        => NetworkClient.Send(new AuthRequestMessage
        {
            username = clientUsername,
            password = clientPassword,
            register = clientRegister,
        });

    void OnAuthResponse(AuthResponseMessage msg)
    {
        if (msg.code == (byte)AuthCode.Success)
        {
            LastClientMessage = ""; // nothing to show — we're headed in-world
            ClientAccept();
        }
        else
        {
            LastClientMessage = msg.message;
            Debug.LogWarning($"[Auth] Login rejected: {msg.message}");
            // Don't tear the client down here — the server owns the rejection and disconnects us shortly
            // (DelayedReject → ServerReject). Calling StopHost/StopClient synchronously from inside this
            // message handler corrupted the client so the *next* StartClient couldn't authenticate.
        }
    }
}

/// <summary>
/// Authenticated session attached to <c>conn.authenticationData</c> on the server. The token is
/// in-memory only (decision O3). <see cref="AccountId"/> is the identity seam 1.3/1.5 consume.
/// </summary>
public sealed class AccountSession
{
    public long   AccountId;
    public string Username;
    public string Token;

    // ── Character-select state (1.5) ─────────────────────────────────────────────
    // Set by CharacterSelectController before it spawns the player; read by CharacterPersistence
    // on the spawned player's OnStartServer.
    public long             SelectedCharacterId; // existing character chosen to enter (1.6 keys load off this)
    public PendingCharacter PendingCreation;     // non-null = create-and-enter a brand-new character
}

/// <summary>A not-yet-spawned character choice, carried on the connection between the create request
/// and the player spawn (decision D2: create = enter).</summary>
public sealed class PendingCharacter
{
    public long   CharacterId; // assigned at creation (1.6, decision O2) so saves key off it
    public string Name;
    public string Race;
    public string Class;
}
