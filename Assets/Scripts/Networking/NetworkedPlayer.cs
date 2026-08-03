using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetworkedPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] Transform  cameraHolder;
    [SerializeField] GameObject playerCorpsePrefab;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 1f;
    [SerializeField] float sprintSpeed = 3f;
    [SerializeField] float jumpHeight = 5f;
    [SerializeField] float gravity = -12f;

    [Header("Look")]
    [SerializeField] float lookSensitivity = 0.15f;
    [SerializeField] float maxPitch = 80f;
    [Tooltip("How far the camera lowers when seated, in metres (feel of the sit/stand height change).")]
    [SerializeField] float sitCameraDrop = 0.85f;
    [Tooltip("Camera height ease speed for sit/stand (higher = snappier).")]
    [SerializeField] float sitCameraLerp = 8f;

    CharacterController _cc;
    Camera _cam;
    PlayerSitting _sitting;
    float _pitch;
    float _yaw;
    float _verticalVelocity;
    float _camStandY;          // captured standing camera height; the seated pose eases below it
    bool  _camStandYCaptured;

    Vector3 _bindPoint;
    string  _zoneId = ZoneCatalog.DefaultStarterZoneId; // M3.0 — server-side current zone

    // 5.4 (AG4) — reverse-index: which mobs currently have this player threat-listed. Populated by
    // EnemyAI.AddThreat; drained (each mob told to flip this player's entry to Dead/Zoned) at the two real
    // departure points below. Server-only, never synced. Disconnect needs no entry here — Mirror destroys
    // the character on disconnect, and every read of a threat entry's identity is already null-safe against
    // that, so nothing needs to proactively push a status change for it.
    readonly System.Collections.Generic.HashSet<EnemyAI> _threateningMobs = new();

    Vector2 _moveInput;
    bool _sprint;
    bool _jumpQueued;
    bool _isLooking; // true while RMB held
    bool _rmbIsLoot; // RMB press was consumed by loot or consider (5.4) — suppress look mode for that press

    // ── 4.2 — client-side prediction + reconciliation ──────────────────────────
    [Header("Prediction")]
    [Tooltip("Reconciliation snaps + replays when the client's predicted position differs from the server's " +
             "acked position by more than this (metres). Placeholder — tune from real MPPM/RTT testing.")]
    [SerializeField] float reconciliationTolerance = 0.15f;

    uint _inputSeq;
    readonly System.Collections.Generic.List<PredictedInput> _pendingInputs = new();

    // Server-side: last input this object has simulated. Not consumed anywhere yet — Mirror's reliable
    // channel already delivers Commands in order — kept as the hook PR5 calls for, e.g. a future
    // out-of-order/replay rejection check, without inventing that check speculatively now.
    uint   _lastProcessedSeq;
    double _lastAckTime; // server-side: AccurateInterval bookkeeping for RpcAckMovement throttling

    struct PredictedInput
    {
        public PlayerInputCmd cmd;
        public Vector3 resultingPosition;
        public float   resultingVerticalVelocity;
    }

    Targetable      _currentTarget;
    NetworkIdentity _serverTarget;

    public event System.Action<Targetable> OnTargetChanged;

    Vector3 _lastChatPos;
    const float ChatPosUpdateThreshold = 5f;

    // Readable by server-side components (e.g. PlayerAutoAttack)
    public NetworkIdentity ServerTarget => _serverTarget;

    // Client-side access for PlayerConsider to pass a raycast/click target into CmdConsider. Ability
    // casts deliberately do NOT use this (see CmdCastAbility) — F-key group-targeting (5.3) only ever
    // updates ServerTarget, not this client-side Targetable-based property, so a Cleric targeting a
    // groupmate via F2 could never heal them if casting still trusted this instead of the server's own
    // authoritative target.
    public NetworkIdentity CurrentTargetIdentity =>
        _currentTarget != null ? _currentTarget.GetComponentInParent<NetworkIdentity>() : null;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _sitting = GetComponent<PlayerSitting>();

        // 3.0 — zones are authored at distinct world-space offsets, so the player transform must sync in
        // world space (the spike's lever for the owner-side offset on cross-scene teleports). For an
        // unparented player local==world, so this is a no-op in the single-scene case.
        var nt = GetComponent<NetworkTransformBase>();
        if (nt != null)
        {
            nt.coordinateSpace = CoordinateSpace.World;

            // 3.1 — sync rotation so REMOTE OBSERVERS visibly turn (the server sets yaw in CmdSendInput;
            // the prefab has syncRotation off). The body transform is yaw-only — pitch lives on the camera
            // holder — so this syncs facing, not the camera. (Overrides the prefab's serialized
            // syncRotation:0.) The owner itself never consumes these snapshots (stock NetworkTransform
            // behavior, restored in 4.2) — its own rotation is always driven locally by ApplyLook.
            nt.syncRotation = true;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        // Nameplate label is set from the character's name by CharacterPersistence (1.5) — no hardcoded
        // "Player" here, or it would clobber a freshly-created character's name (component order).
        _lastChatPos = transform.position;
        _bindPoint   = transform.position;
        ChatManager.Instance?.RegisterPlayer(connectionToClient, transform.position);
        var health = GetComponent<Health>();
        health.OnDied -= HandlePlayerDeath; // ensure exactly one subscription even if OnStartServer fires twice
        health.OnDied += HandlePlayerDeath;
    }

    public override void OnStopServer()
    {
        GetComponent<Health>().OnDied -= HandlePlayerDeath;
        ChatManager.Instance?.UnregisterPlayer(connectionToClient);
    }

    public override void OnStartLocalPlayer()
    {
        LocalPlayer.Set(this); // 1.7 — single binding seam the HUD subscribes to

        _cam = GetComponentInChildren<Camera>(true);
        if (_cam) _cam.gameObject.SetActive(true);

        // Cursor free by default — RMB to look
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 4.2 — the owner predicts locally again (Update()) and reconciles against the server's ack
        // (RpcAckMovement). Its NetworkTransform is back to stock Mirror behavior, which already skips
        // applying incoming snapshots to the owning connection — nothing to disable here.
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer) SetCameraActive(false);
    }

    public override void OnStopClient()
    {
        // Local player went away (camp / disconnect / host restart) — drop the HUD binding (1.7).
        if (LocalPlayer.Current == this) LocalPlayer.Clear();
    }

    void SetCameraActive(bool active)
    {
        var cam = GetComponentInChildren<Camera>(true);
        if (cam) cam.gameObject.SetActive(active);
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    // 4.2 — client-side prediction + reconciliation. The owner simulates its own movement locally,
    // immediately (instant visual feedback, no round-trip wait), then sends the same input to the server as
    // a sequence-numbered PlayerInputCmd. The server runs the IDENTICAL SimulateMovement step (see below)
    // synchronously inside CmdSendInput — it's no longer driven by a per-Update() server loop like 4.1 — and
    // periodically acks its result back to the owner (RpcAckMovement), which snaps + replays its buffered
    // inputs if its own prediction disagreed (see PredictedInput/_pendingInputs). Movement is no longer run
    // from `if (isServer)` in Update() at all; it only ever runs from an explicit SimulateMovement call
    // (prediction, server authority, or reconciliation replay), all sharing this one code path so they can't
    // drift apart on their own.
    //
    // Host is a special case: isLocalPlayer and isServer are both true on the SAME object instance (no
    // separate client/server copies like a remote connection has), and CmdSendInput's Command handler runs
    // synchronously against that same instance when called locally. So host must NOT also predict locally
    // here — doing so would apply SimulateMovement twice to the same transform/CC in one frame. Host sees
    // its own movement immediately anyway (zero self-RTT), so it doesn't need prediction.
    void Update()
    {
        if (isLocalPlayer)
        {
            if (_currentTarget != null && !_currentTarget)
            {
                _currentTarget = null;
                OnTargetChanged?.Invoke(null);
                CmdSetTarget(null);
            }

            CollectInput();
            ApplyLook();

            _inputSeq++;
            var cmd = new PlayerInputCmd
            {
                move   = _moveInput,
                yaw    = _yaw,
                sprint = _sprint,
                jump   = _jumpQueued,
                seq    = _inputSeq,
                dt     = Time.deltaTime,
            };
            _jumpQueued = false; // packaged into this frame's cmd; consumed synchronously either way below

            if (!isServer)
            {
                SimulateMovement(cmd.move, cmd.yaw, cmd.sprint, cmd.jump, cmd.dt);
                _pendingInputs.Add(new PredictedInput
                {
                    cmd = cmd,
                    resultingPosition = transform.position,
                    resultingVerticalVelocity = _verticalVelocity,
                });
            }

            CmdSendInput(cmd);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void CollectInput()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        bool chatOpen = ChatUI.IsOpen;
        bool lmbHeld  = mouse.leftButton.isPressed;
        bool rmbHeld  = mouse.rightButton.isPressed;
        bool bothHeld = lmbHeld && rmbHeld;

        // ── RMB press — loot raycast takes priority over look mode ───────────
        if (mouse.rightButton.wasPressedThisFrame && _cam != null && !chatOpen)
        {
            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit rmbHit, 100f))
            {
                var hitCorpse = rmbHit.collider.GetComponentInParent<Corpse>();
                if (hitCorpse != null && hitCorpse.IsActive)
                {
                    _rmbIsLoot = true;
                    LootUI.Open(hitCorpse);
                }
                else
                {
                    var hitPlayerCorpse = rmbHit.collider.GetComponentInParent<PlayerCorpse>();
                    if (hitPlayerCorpse != null && hitPlayerCorpse.IsActive && hitPlayerCorpse.Owner == netIdentity)
                    {
                        _rmbIsLoot = true;
                        LootUI.Open(hitPlayerCorpse);
                    }
                    else
                    {
                        // 5.4 (AG1) — right-click a live (non-corpse) target considers it. Purely
                        // additive: RMB on a live mob did nothing before this.
                        var hitTargetable = rmbHit.collider.GetComponentInParent<Targetable>();
                        var considerNi = hitTargetable != null ? hitTargetable.GetComponentInParent<NetworkIdentity>() : null;
                        if (considerNi != null)
                        {
                            _rmbIsLoot = true; // reuse the same look-mode suppression flag
                            GetComponent<PlayerConsider>()?.CmdConsider(considerNi);
                        }
                    }
                }
            }
        }
        if (mouse.rightButton.wasReleasedThisFrame)
            _rmbIsLoot = false;

        // ── Cursor / look mode (RMB) — cursor hidden whenever RMB is held ────
        _isLooking       = rmbHeld && !_rmbIsLoot;
        Cursor.lockState = _isLooking ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !_isLooking;

        // ── Mouse look (RMB held only) — runs even with chat open ────────────
        if (_isLooking)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * lookSensitivity;
            _pitch  = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -maxPitch, maxPitch);
        }

        // ── Both mouse buttons = move forward (mouse-driven, chat-safe) ──────
        if (chatOpen)
        {
            _moveInput = bothHeld ? Vector2.up : Vector2.zero;
            _sprint    = false;
            if (!_isLooking && !bothHeld && mouse.leftButton.wasPressedThisFrame)
                TryTarget();
            return;
        }

        // ── Targeting (LMB, cursor free, not both-held) ───────────────────────
        if (!_isLooking && !bothHeld && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            TryTarget();

        // ── Movement keys ─────────────────────────────────────────────────────
        _moveInput = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f)
        );

        // Both mouse buttons contribute forward on top of keyboard
        if (bothHeld) _moveInput.y = Mathf.Max(_moveInput.y, 1f);

        _sprint = kb.leftShiftKey.isPressed;
        if (kb.spaceKey.wasPressedThisFrame) _jumpQueued = true;
    }

    // ── Targeting ─────────────────────────────────────────────────────────────

    void TryTarget()
    {
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Targetable hit = Physics.Raycast(ray, out RaycastHit info, 100f)
            ? info.collider.GetComponentInParent<Targetable>()
            : null;

        ApplyTarget(hit);
    }

    /// <summary>5.4 follow-up — players now carry Targetable too (a Cleric needs to be able to select a
    /// groupmate to heal them), so F-key group-targeting (PartyFrameUI) can route through this exact same
    /// path — highlight, TargetFrame, server sync — instead of a separate, narrower mechanism.</summary>
    public void SetTargetByIdentity(NetworkIdentity target)
        => ApplyTarget(target != null ? target.GetComponent<Targetable>() : null);

    void ApplyTarget(Targetable hit)
    {
        if (hit == _currentTarget) return; // no change

        _currentTarget?.SetHighlight(false);
        _currentTarget = hit;
        if (_currentTarget != null)
        {
            // Players are never "hostile" — no PvP exists — so they get a distinct friendly highlight
            // instead of the same red used for enemies/NPCs.
            bool hostile = _currentTarget.GetComponentInParent<NetworkedPlayer>() == null;
            _currentTarget.SetHighlight(true, hostile);
        }

        OnTargetChanged?.Invoke(_currentTarget);

        var ni = hit != null ? hit.GetComponentInParent<NetworkIdentity>() : null;
        CmdSetTarget(ni);

        if (hit != null)
            hit.GetComponentInParent<NpcEventDispatcher>()?.DispatchTargeted(netIdentity);
    }

    static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    // ── Look ──────────────────────────────────────────────────────────────────

    void ApplyLook()
    {
        // Camera height eases down when seated / back up when standing so the player feels the height change.
        if (cameraHolder)
        {
            if (!_camStandYCaptured) { _camStandY = cameraHolder.localPosition.y; _camStandYCaptured = true; }
            bool seated  = _sitting != null && _sitting.IsSitting;
            float targetY = _camStandY - (seated ? sitCameraDrop : 0f);
            var lp = cameraHolder.localPosition;
            lp.y = Mathf.Lerp(lp.y, targetY, Mathf.Clamp01(sitCameraLerp * Time.deltaTime));
            cameraHolder.localPosition = lp;
        }

        // Seated (3.1.7): the body stays put; free-look rotates only the camera. The camera holder is a child
        // of the body, so its world yaw = bodyYaw + localYaw — offset the holder by (_yaw − bodyYaw) so the
        // camera still tracks _yaw while the body is frozen. On standing, ApplyLook resumes driving the body
        // to _yaw and the holder offset returns to 0, so the view doesn't jump (the body turns to face it).
        if (_sitting != null && _sitting.IsSitting)
        {
            float holderYaw = cameraHolder ? Mathf.DeltaAngle(transform.eulerAngles.y, _yaw) : 0f;
            if (cameraHolder) cameraHolder.localEulerAngles = new Vector3(_pitch, holderYaw, 0f);
            return;
        }

        transform.eulerAngles = new Vector3(0f, _yaw, 0f);
        if (cameraHolder) cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    // 4.2 (PR2) — the single deterministic movement step, shared by local prediction, server authority, and
    // reconciliation replay. Operates only on its parameters plus the persistent `_verticalVelocity`/CC
    // state — no `Time.deltaTime` or mutable `_moveInput`/`_yaw`/`_sprint`/`_jumpQueued` reads — so replaying
    // several historical inputs back-to-back in one frame reproduces exactly what happened originally.
    // Direction is computed from the given `yaw` directly (not `transform.right`/`forward`), since replay
    // doesn't touch `transform.rotation` at each step (only `ApplyLook`/CmdSendInput's explicit rotation
    // writes do) — using the live transform rotation here would make replay depend on side effects instead
    // of its own parameters.
    //
    // 4.2 hardening — CC.Move()'s own collision resolution against ANOTHER CharacterController isn't a pure
    // function of net displacement: it's sensitive to the exact per-call step timing (e.g. `isGrounded`
    // flickering on contact nudges the vertical component just enough to let one capsule ride over the
    // other's rounded cap), and the client/server processes don't tick in lockstep, so the two independent
    // simulations can occasionally disagree at the contact instant even with identical inputs (found
    // 2026-07-13 testing 4.2: server let a push-through happen that the client's own prediction blocked).
    // ResolvePlayerOverlap() runs an explicit post-move de-penetration check every step, on every caller
    // (prediction/server/replay alike) — even if one step's sweep lets a sliver of interpenetration slip
    // through, the very next call pushes it back out, converging reliably instead of relying purely on
    // CC.Move()'s implicit (and here, unreliable) contact resolution.
    void SimulateMovement(Vector2 move, float yaw, bool sprint, bool jump, float dt)
    {
        if (_cc.isGrounded) _verticalVelocity = -2f;

        if (jump && _cc.isGrounded)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _verticalVelocity += gravity * dt;

        float speed = sprint ? sprintSpeed : moveSpeed;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        _cc.Move((dir * speed + Vector3.up * _verticalVelocity) * dt);

        ResolvePlayerOverlap();
    }

    // Explicit post-move de-penetration against other players' CharacterControllers (see SimulateMovement's
    // hardening note above). Unity's CharacterController IS a Collider under the hood, so ComputePenetration
    // works on it directly. Scoped to other NetworkedPlayers specifically (not mobs/geometry — CC.Move()'s
    // own sweep already handles static world collision fine; this is only patching the CC-vs-CC contact
    // case that proved unreliable).
    void ResolvePlayerOverlap()
    {
        Vector3 center = transform.position + _cc.center;
        var hits = Physics.OverlapSphere(center, _cc.radius + _cc.skinWidth + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit == _cc) continue;
            var otherCc = hit as CharacterController;
            if (otherCc == null) continue;
            if (otherCc.GetComponent<NetworkedPlayer>() == null) continue; // players only, not mobs

            if (Physics.ComputePenetration(
                    _cc, transform.position, transform.rotation,
                    otherCc, otherCc.transform.position, otherCc.transform.rotation,
                    out Vector3 pushDir, out float pushDist) && pushDist > 0f)
            {
                transform.position += pushDir * pushDist;
            }
        }
    }

    // ── Server ────────────────────────────────────────────────────────────────

    // 4.2 (PR3/PR5) — server processing. Runs SimulateMovement synchronously against this input the moment
    // it arrives, using the CLIENT-reported dt (not the server's own frame delta) — that's what makes the
    // owner's replay-on-correction reproduce the same result the server got. No longer driven by a
    // per-Update() server loop (contrast 4.1) — each Command IS a discrete simulation step now.
    [Command]
    void CmdSendInput(PlayerInputCmd cmd)
    {
        SimulateMovement(cmd.move, cmd.yaw, cmd.sprint, cmd.jump, cmd.dt);
        _lastProcessedSeq = cmd.seq;
        _yaw = cmd.yaw; // authoritative facing — read by respawn/unstuck/bind elsewhere in this class

        // 3.1.7 — moving stands a seated player (server-authoritative). Pressing a move key stands you, then
        // SimulateMovement carries you off — no need to block movement while seated.
        if (cmd.move.sqrMagnitude > 0.01f) _sitting?.ServerStand();

        // Body yaw follows look only while standing; a seated player's body stays frozen (free-look moves the
        // camera, not the body) — this is the rotation observers see, so it must match the local ApplyLook.
        if (_sitting == null || !_sitting.IsSitting)
            transform.eulerAngles = new Vector3(0f, _yaw, 0f);

        if (ChatManager.Instance != null &&
            Vector3.Distance(transform.position, _lastChatPos) > ChatPosUpdateThreshold)
        {
            ChatManager.Instance.UpdatePosition(connectionToClient, transform.position);
            _lastChatPos = transform.position;
        }

        // Ack back to the owner only, throttled to Mirror's own broadcast cadence rather than every single
        // Command — observers already get position via the ordinary NetworkTransform sync path, this ack is
        // purely for the owner's own reconciliation.
        if (connectionToClient != null &&
            AccurateInterval.Elapsed(NetworkTime.time, NetworkServer.sendInterval, ref _lastAckTime))
        {
            RpcAckMovement(connectionToClient, cmd.seq, transform.position, _yaw, _verticalVelocity);
        }
    }

    // 4.2 (PR6) — reconciliation. Discards buffered inputs the server has now processed; if the client's own
    // recorded prediction for the acked sequence disagrees with the server's result by more than
    // `reconciliationTolerance`, snap to the server's state and replay every input still pending past that
    // point (in order, each with its own recorded dt) to catch back up to "now." A visible pop on a large
    // correction is accepted for this pass — smoothing is a follow-up if testing shows it's actually
    // noticeable, not something to design speculatively now.
    //
    // Host never buffers anything (see Update()), so `matchIndex` is always -1 there and this is a no-op.
    [TargetRpc]
    void RpcAckMovement(NetworkConnectionToClient target, uint seq, Vector3 position, float yaw, float verticalVelocity)
    {
        int matchIndex = -1;
        for (int i = 0; i < _pendingInputs.Count; i++)
            if (_pendingInputs[i].cmd.seq == seq) { matchIndex = i; break; }

        if (matchIndex >= 0)
        {
            var predicted = _pendingInputs[matchIndex];
            if (Vector3.Distance(predicted.resultingPosition, position) > reconciliationTolerance)
            {
                transform.position = position;
                _verticalVelocity  = verticalVelocity;

                for (int i = matchIndex + 1; i < _pendingInputs.Count; i++)
                {
                    var pending = _pendingInputs[i];
                    SimulateMovement(pending.cmd.move, pending.cmd.yaw, pending.cmd.sprint, pending.cmd.jump, pending.cmd.dt);
                    pending.resultingPosition         = transform.position;
                    pending.resultingVerticalVelocity = _verticalVelocity;
                    _pendingInputs[i] = pending;
                }
            }
        }

        _pendingInputs.RemoveAll(p => p.cmd.seq <= seq);
    }

    // 4.2 (PR9) — any hard server-side position override (teleport/respawn/load) invalidates the owner's
    // buffered prediction; replaying a stale pre-teleport input on top of the new position would send the
    // player flying in a nonsense direction. Call alongside every such override.
    [TargetRpc]
    void TargetRpcResetPrediction(NetworkConnectionToClient target)
    {
        _pendingInputs.Clear();
        _verticalVelocity = 0f;
    }

    [Command]
    public void CmdSendChat(ChatChannel channel, string target, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var msg = new ChatMessage(channel, gameObject.name, text);

        if (channel == ChatChannel.Whisper)
        {
            var targetConn = ChatManager.Instance?.FindConnectionByName(target);
            if (targetConn != null)
            {
                ChatManager.Instance.SendDirect(msg, targetConn);
                ChatManager.Instance.SendDirect(
                    new ChatMessage(ChatChannel.Whisper, $"To {target}", text), connectionToClient);
            }
            else
            {
                ChatManager.Instance?.SendDirect(
                    new ChatMessage(ChatChannel.System, "System", $"Player '{target}' not found."),
                    connectionToClient);
            }
            return;
        }

        ChatManager.Instance?.SendArea(msg, transform.position);

        // NPCs hear /say — dispatch to all conversations within their own hearing ranges
        if (channel == ChatChannel.Say)
        {
            foreach (var conv in FindObjectsByType<NpcConversation>())
                conv.HearMessage(netIdentity, text);
        }
    }

    // Public so PartyFrameUI (5.3, GP11) can drive the same server-side target from an F-key press, not
    // just a mouse click — it does NOT touch the client-side Targetable/highlight/TargetFrame system (see
    // PartyFrameUI's own doc comment for why), only the server-authoritative combat target.
    [Command]
    public void CmdSetTarget(NetworkIdentity target) => _serverTarget = target;

    // ── Threat reverse-index (5.4, AG4) ─────────────────────────────────────────

    [Server] public void RegisterThreateningMob(EnemyAI mob)   => _threateningMobs.Add(mob);
    [Server] public void UnregisterThreateningMob(EnemyAI mob) => _threateningMobs.Remove(mob);

    /// <summary>Tell every mob that currently has this player threat-listed to flip their entry's status,
    /// then forget them — the reverse-index's only job is finding who to notify at the moment of
    /// departure. Null-checks each mob (Unity's overridden equality) since a mob could itself have been
    /// destroyed since registering without unregistering (harmless either way, just a defensive guard).</summary>
    [Server]
    void MarkDepartedFromThreatLists(EnemyAI.ThreatStatus status)
    {
        foreach (var mob in _threateningMobs)
            if (mob != null) mob.MarkThreatStatus(netIdentity, status);
        _threateningMobs.Clear();
    }

    // ── Death handling ────────────────────────────────────────────────────────

    [Server]
    void HandlePlayerDeath(NetworkIdentity attacker)
    {
        var health = GetComponent<Health>();
        if (health.IsImmune) return;
        health.SetImmunity(10f);

        // 5.4 (AG4) — the character survives (respawns below), so unlike a destroyed-on-disconnect
        // identity this needs an explicit push: tell every mob that had this player threat-listed to stop
        // treating them as a live threat, while preserving their damage for 5.3's kill-credit purposes.
        MarkDepartedFromThreatLists(EnemyAI.ThreatStatus.Dead);

        var inv = GetComponent<PlayerInventory>();
        var exp = GetComponent<PlayerExperience>();

        var items = new System.Collections.Generic.List<InventorySlot>();
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            var slot = inv.Slots[i];
            if (!slot.IsEmpty) items.Add(slot);
        }
        int copper = inv.TotalCopperValue;
        int xpLoss = exp?.DeathXpLoss() ?? 0;

        if (playerCorpsePrefab != null)
        {
            var corpseObj = Instantiate(playerCorpsePrefab, transform.position, Quaternion.identity);

            // M3.0 Stage C: drop the corpse in the player's zone scene, not the active (base) scene, so it's
            // observed by players in that zone. Instantiate defaults to the active scene.
            if (gameObject.scene.IsValid() && corpseObj.scene != gameObject.scene)
                SceneManager.MoveGameObjectToScene(corpseObj, gameObject.scene);

            corpseObj.GetComponent<PlayerCorpse>().Prepare(netIdentity, items, copper, xpLoss, gameObject.name);
            NetworkServer.Spawn(corpseObj);
        }

        inv.ClearAll();

        if (xpLoss > 0)
        {
            exp?.RemoveXp(xpLoss);
            SendSystemMsg($"You have lost {xpLoss} experience.");
        }

        SendSystemMsg("You have died.");
        health.ResetToFull();

        // Respawn (moves the server-side transform immediately so mobs can't re-aggro and re-kill the
        // player at the death location). M3.0 Stage C: route through ZoneManager so the scene assignment
        // stays correct across a cross-zone death. Bind points are authored in the starter zone's coords;
        // a cross-zone bind isn't tracked yet, so a death in a non-starter zone respawns at that zone's
        // default entry (3.0.1 will add proper per-zone bind points).
        var zm = ZoneManager.Instance;
        if (zm != null && connectionToClient != null)
        {
            Vector3 respawnPos = _bindPoint;
            float   respawnYaw = _yaw;
            if (_zoneId != zm.StarterZoneId)
            {
                var entry = zm.EntryTransform(_zoneId, "default");
                if (entry != null) { respawnPos = entry.position; respawnYaw = entry.eulerAngles.y; }
            }
            zm.ServerPlaceInZone(connectionToClient, _zoneId, respawnPos, respawnYaw);
        }
        else
        {
            var cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.position = _bindPoint;
            cc.enabled = true;
            TargetRpcRespawn(connectionToClient, _bindPoint);
            if (connectionToClient != null) TargetRpcResetPrediction(connectionToClient); // 4.2 (PR9)
        }
    }

    [TargetRpc]
    void TargetRpcRespawn(NetworkConnectionToClient target, Vector3 position)
    {
        var cc = GetComponent<CharacterController>();
        cc.enabled = false;
        transform.position = position;
        cc.enabled = true;
    }

    // Call from spells/abilities to update the player's bind point
    [Server]
    public void SetBindPoint(Vector3 position) => _bindPoint = position;

    // ── Zones (3.0) ─────────────────────────────────────────────────────────────

    /// <summary>Which zone this player currently stands in (server-authoritative). Read by chat/spawn/
    /// persistence; set by ZoneManager on spawn + transition.</summary>
    public string CurrentZoneId => _zoneId;

    [Server]
    public void SetZone(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId) || zoneId == _zoneId) return; // no real transition
        // 5.4 (AG4) — same reasoning as HandlePlayerDeath: the identity survives a zone move, so mobs that
        // had this player threat-listed need an explicit push, not just a reactive null-check.
        MarkDepartedFromThreatLists(EnemyAI.ThreatStatus.Zoned);
        _zoneId = zoneId;
    }

    /// <summary>Server-authoritative teleport used by zone transitions, respawn, and character-select
    /// spawn. Uses <c>NetworkTransform.ServerTeleport</c> — never a raw position set — so delta
    /// compression / interpolation don't drift observers (2.0 spike finding). CC toggled so it doesn't
    /// fight the move.</summary>
    [Server]
    public void ServerWarpTo(Vector3 position, float yaw)
    {
        _yaw = yaw;
        _verticalVelocity = 0f; // arrive fresh — don't carry accumulated fall/jump velocity into the warp
        var rot = Quaternion.Euler(0f, yaw, 0f);

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(position, rot);
        if (cc != null) cc.enabled = true;

        var nt = GetComponent<NetworkTransformBase>();
        if (nt != null) nt.ServerTeleport(position, rot);
        else            TargetRpcRespawn(connectionToClient, position);

        if (connectionToClient != null) TargetRpcResetPrediction(connectionToClient); // 4.2 (PR9)
    }

    /// <summary>`/unstuck` — pull a stuck/falling player to a safe spot: the current zone's default entry
    /// (3.0), or the bind point if zones aren't active / the zone has no default entry. Gated out of
    /// combat so it can't be used as an escape.</summary>
    [Command]
    public void CmdUnstuck()
    {
        var combat = GetComponent<CombatState>();
        if (combat != null && combat.InCombat)
        {
            SendSystemMsg("You can't use /unstuck while in combat.");
            return;
        }

        Vector3 dest = _bindPoint;
        float   yaw  = _yaw;

        var entry = ZoneManager.Instance?.EntryTransform(_zoneId, "default");
        if (entry != null) { dest = entry.position; yaw = entry.eulerAngles.y; }

        ServerWarpTo(dest, yaw);
        ChatManager.Instance?.UpdatePosition(connectionToClient, dest);
        SendSystemMsg("You feel a tug as you are pulled back to safety.");
    }

    // ── Travel (dev/testing convenience) ────────────────────────────────────────

    // Named fast-travel points for manual testing — zone portals, the mob spawner area, and the village
    // hub. X/Z only; Y is resolved fresh on each use via ground/navmesh snap (ResolveGroundPosition,
    // mirrors SpawnPoint.ResolveSpawnPosition) so these stay correct as zone terrain changes — e.g. once
    // 3.5 replaces Thornwood's current flat scaffold with real terrain, `thornwood`/`grukmar` keep working
    // without a code update.
    static readonly (string name, string zoneId, float x, float z)[] TravelPoints =
    {
        ("creslins",  ZoneCatalog.DefaultStarterZoneId, 0f,      1395f),    // near the Thornwood portal
        ("thornwood", "thornwood",                      5000f,   -8f),     // near both its portals
        ("grukmar",   "grukmars_deep",                  10000f,  -8f),     // near the portal back to Thornwood
        ("village",   ZoneCatalog.DefaultStarterZoneId, 200.41f, 1175.3f), // Trellis hub
        ("mobs",      ZoneCatalog.DefaultStarterZoneId, 25f,     35f),     // wildlife encounter spawn point
        ("crossroads",ZoneCatalog.DefaultStarterZoneId, -112.27f, 814.77f),// SM_Prop_Sign_03
    };

    /// <summary>`/travel &lt;name&gt;` — dev/testing fast travel to a named point (zone portals, the mob
    /// spawner area, the village hub). Gated out of combat like `/unstuck` so it can't be used as an
    /// escape. Unknown/blank name lists the options via chat instead of warping.</summary>
    [Command]
    public void CmdTravel(string destination)
    {
        var combat = GetComponent<CombatState>();
        if (combat != null && combat.InCombat)
        {
            SendSystemMsg("You can't use /travel while in combat.");
            return;
        }

        destination = (destination ?? "").Trim().ToLowerInvariant();
        foreach (var p in TravelPoints)
        {
            if (p.name != destination) continue;

            if (ZoneManager.Instance == null)
            {
                SendSystemMsg("Zones aren't active — /travel is unavailable.");
                return;
            }

            Vector3 pos = ResolveGroundPosition(p.x, p.z);

            // Same-zone hop: warp directly (mirrors /unstuck) instead of routing through
            // ZoneManager.ServerPlaceInZone — its client scene-swap messaging assumes a genuine zone
            // CHANGE, and a redundant additive-load for an already-loaded non-base zone isn't a path
            // that's ever been exercised.
            if (p.zoneId == _zoneId)
            {
                ServerWarpTo(pos, _yaw);
                ChatManager.Instance?.UpdatePosition(connectionToClient, pos);
            }
            else
            {
                ZoneManager.Instance.ServerPlaceInZone(connectionToClient, p.zoneId, pos, _yaw);
            }

            SendSystemMsg($"You travel to {p.name}.");
            return;
        }

        string names = string.Join(", ", System.Array.ConvertAll(TravelPoints, t => t.name));
        SendSystemMsg($"Unknown travel point '{destination}'. Options: {names}.");
    }

    // Raycast down onto the ground + snap to the nearest navmesh point, given only X/Z — same pattern as
    // SpawnPoint.ResolveSpawnPosition. A single shared physics scene + a single global navmesh spans every
    // zone at its own world offset (3.0's zone architecture), so this resolves correctly regardless of
    // which zone the X/Z falls in — no per-zone special-casing needed.
    static Vector3 ResolveGroundPosition(float x, float z)
    {
        Vector3 origin = new Vector3(x, 1000f, z);
        Vector3 pos    = new Vector3(x, 0f, z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;
        if (NavMesh.SamplePosition(pos, out var navHit, 15f, NavMesh.AllAreas))
            pos = navHit.position;
        return pos;
    }

    // ── Persistence (1.3) ───────────────────────────────────────────────────────

    public Vector3 BindPoint => _bindPoint;
    public float   Yaw       => transform.eulerAngles.y;

    /// <summary>Restore position/yaw + bind point from a loaded snapshot. Uses the same CC-safe
    /// teleport pattern as respawn so the CharacterController doesn't fight the move, and snaps the
    /// owning client too.</summary>
    [Server]
    public void LoadState(Vector3 position, float yaw, Vector3 bindPoint)
    {
        _bindPoint = bindPoint;
        var cc = GetComponent<CharacterController>();
        cc.enabled = false;
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cc.enabled = true;
        TargetRpcRespawn(connectionToClient, position);
        if (connectionToClient != null) TargetRpcResetPrediction(connectionToClient); // 4.2 (PR9)
    }

    // ── Loot commands ─────────────────────────────────────────────────────────

    const float LootRange = 6f;

    // Root-causing a real bug (2026-07-30): Loot All / per-slot Take could silently no-op — the corpse
    // resolution helpers below returned a bare bool with no record of WHY, so every denial path (corpse
    // gone, not eligible, out of range) looked identical to the player: nothing happens, no message, the
    // loot window never closes. Every Cmd below now reports a specific reason instead of failing silently.
    enum LootDenyReason { None, NotFound, NotEligible, OutOfRange }

    [Command]
    public void CmdTakeLootSlot(NetworkIdentity corpseId, int slotIndex)
    {
        var inv = GetComponent<PlayerInventory>();
        if (TryGetMobCorpse(corpseId, out var mob, out var mobReason))
        {
            var slot = mob.PeekSlot(slotIndex);
            if (slot.IsEmpty) return;
            if (inv.AddItem(slot.itemId, slot.quantity, enforceLore: true)) mob.RemoveSlot(slotIndex);
            else SendSystemMsg(AcquireBlockedMsg(inv, slot.itemId));
            return;
        }
        if (TryGetPlayerCorpse(corpseId, out var pc, out var pcReason))
        {
            var slot = pc.PeekSlot(slotIndex);
            if (slot.IsEmpty) return;
            if (inv.AddItem(slot.itemId, slot.quantity, enforceLore: true)) pc.RemoveSlot(slotIndex);
            else SendSystemMsg(AcquireBlockedMsg(inv, slot.itemId));
            return;
        }
        ReportLootDenied(corpseId, mobReason, pcReason);
    }

    // 3.2.1: the right refusal line for a blocked acquire — LORE dupe vs a plain full inventory.
    static string AcquireBlockedMsg(PlayerInventory inv, string itemId)
    {
        var def = ItemRegistry.Instance?.Get(itemId);
        return def != null && def.lore && inv.AlreadyHolds(itemId)
            ? $"You can only carry one {def.displayName}."
            : "Inventory is full.";
    }

    [Command]
    public void CmdTakeLootCopper(NetworkIdentity corpseId)
    {
        if (TryGetMobCorpse(corpseId, out var mob, out var mobReason))
        {
            int c = mob.TakeCopper();
            if (c > 0) GetComponent<PlayerInventory>().AddCurrency(c);
            return;
        }
        if (TryGetPlayerCorpse(corpseId, out var pc, out var pcReason))
        {
            int c = pc.TakeCopper();
            if (c > 0) GetComponent<PlayerInventory>().AddCurrency(c);
            return;
        }
        ReportLootDenied(corpseId, mobReason, pcReason);
    }

    [Command]
    public void CmdTakeLootAll(NetworkIdentity corpseId)
    {
        var inv = GetComponent<PlayerInventory>();
        if (TryGetMobCorpse(corpseId, out var mob, out var mobReason))
        {
            int blocked = mob.TakeAll(inv);
            if (blocked > 0) SendSystemMsg(BlockedCountMsg(blocked));
            return;
        }
        if (TryGetPlayerCorpse(corpseId, out var pc, out var pcReason))
        {
            int blocked = pc.TakeAll(inv);
            if (blocked > 0) SendSystemMsg(BlockedCountMsg(blocked));
            return;
        }
        ReportLootDenied(corpseId, mobReason, pcReason);
    }

    static string BlockedCountMsg(int blocked) => blocked == 1
        ? "One item could not be taken (inventory full or already held)."
        : $"{blocked} items could not be taken (inventory full or already held).";

    // Whichever corpse component actually exists on the target is the authoritative reason — a "NotFound"
    // from the OTHER helper (checking for a component that was never there in the first place) is noise.
    void ReportLootDenied(NetworkIdentity id, LootDenyReason mobReason, LootDenyReason pcReason)
    {
        bool isMob = id != null && id.GetComponent<Corpse>() != null;
        var reason = isMob ? mobReason : pcReason;
        string msg = reason switch
        {
            LootDenyReason.NotEligible => "You don't have looting rights to that corpse.",
            LootDenyReason.OutOfRange  => "You are too far away to loot that.",
            _                          => "That corpse is no longer there.",
        };
        Debug.Log($"[Loot] {name} denied looting {(id != null ? id.name : "null")} — reason: {reason} (isMob={isMob})");
        SendSystemMsg(msg);
    }

    bool TryGetMobCorpse(NetworkIdentity id, out Corpse corpse, out LootDenyReason reason)
    {
        corpse = id?.GetComponent<Corpse>();
        if (corpse == null || !corpse.IsActive) { corpse = null; reason = LootDenyReason.NotFound; return false; }
        // 5.3 (GP5) — exclusive to the group that dealt the majority of this mob's damage (snapshotted at
        // death); Corpse.CanLoot falls back to "anyone" if that resolution never ran (e.g. no MobKillReward).
        if (!corpse.CanLoot(netIdentity)) { corpse = null; reason = LootDenyReason.NotEligible; return false; }
        if (Vector3.Distance(transform.position, id.transform.position) > LootRange) { corpse = null; reason = LootDenyReason.OutOfRange; return false; }
        reason = LootDenyReason.None;
        return true;
    }

    bool TryGetPlayerCorpse(NetworkIdentity id, out PlayerCorpse corpse, out LootDenyReason reason)
    {
        corpse = id?.GetComponent<PlayerCorpse>();
        if (corpse == null || !corpse.IsActive)      { corpse = null; reason = LootDenyReason.NotFound; return false; }
        if (corpse.Owner != netIdentity)              { corpse = null; reason = LootDenyReason.NotEligible; return false; }
        if (Vector3.Distance(transform.position, id.transform.position) > LootRange) { corpse = null; reason = LootDenyReason.OutOfRange; return false; }
        reason = LootDenyReason.None;
        return true;
    }

    void SendSystemMsg(string text) =>
        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.System, "System", text), connectionToClient);

    // ── Ability commands ──────────────────────────────────────────────────────

    // No client-supplied target parameter, deliberately — casting reads the server's own
    // authoritative ServerTarget (set by CmdSetTarget, from either click-targeting or F-key group-
    // targeting) instead of trusting whatever NetworkIdentity a client claims. Also what makes F-key
    // group-targeting actually usable for heals/buffs (a groupmate isn't Targetable, so the client-side
    // CurrentTargetIdentity used elsewhere can never resolve to one).
    [Command]
    public void CmdCastAbility(int hotbarSlot)
        => GetComponent<PlayerAbilities>().TryCast(hotbarSlot, _serverTarget);

    [Command]
    public void CmdSetHotbarSlot(int slot, string abilityId)
        => GetComponent<PlayerAbilities>().SetHotbarSlot(slot, abilityId);

    // ── Equipment commands ────────────────────────────────────────────────────

    [Command]
    public void CmdEquipItem(int inventorySlotIndex)
    {
        var inv   = GetComponent<PlayerInventory>();
        var equip = GetComponent<PlayerEquipment>();
        var slot  = inv.Slots[inventorySlotIndex];
        if (slot.IsEmpty) return;
        var def = ItemRegistry.Instance?.Get(slot.itemId);
        if (def == null || !def.isEquippable) return;
        if (equip.TryEquip(def.equipSlot, slot.itemId, inv))
            SendSystemMsg($"You equip {def.displayName}.");
        else
            SendSystemMsg("Cannot equip that item.");
    }

    [Command]
    public void CmdUnequipItem(int equipSlotIndex)
    {
        var inv    = GetComponent<PlayerInventory>();
        var equip  = GetComponent<PlayerEquipment>();
        var slot   = (EquipSlot)equipSlotIndex;
        string itemId = equip.GetItemId(slot);
        var def    = ItemRegistry.Instance?.Get(itemId);
        string label = def != null ? def.displayName : itemId;
        if (equip.TryUnequip(slot, inv))
            SendSystemMsg($"You unequip {label}.");
        else
            SendSystemMsg("Cannot unequip that item.");
    }

    // ── Vendor commands ───────────────────────────────────────────────────────

    [Command]
    public void CmdBuyItem(NetworkIdentity vendorNetId, string itemId)
    {
        var vendor = vendorNetId?.GetComponent<VendorApplicator>();
        if (vendor == null || !vendor.HasItem(itemId)) return;
        var def = ItemRegistry.Instance?.Get(itemId);
        if (def == null) return;
        var inv = GetComponent<PlayerInventory>();
        // 3.2.1: check LORE + space BEFORE charging, so a refused buy never spends coin.
        if (!inv.CanAcquire(itemId))
        { SendSystemMsg(AcquireBlockedMsg(inv, itemId)); return; }
        if (def.buyPrice > 0 && !inv.SpendCurrency(def.buyPrice))
        { SendSystemMsg("You cannot afford that."); return; }
        inv.AddItem(itemId, 1, enforceLore: true);
        SendSystemMsg($"You buy {def.displayName} for {CurrencyUtil.Format(def.buyPrice)}.");
    }

    [Command]
    public void CmdSellItem(NetworkIdentity vendorNetId, int inventorySlotIndex)
    {
        var vendor = vendorNetId?.GetComponent<VendorApplicator>();
        if (vendor == null) return;
        var inv = GetComponent<PlayerInventory>();
        if ((uint)inventorySlotIndex >= (uint)inv.Slots.Count) return;
        var slot = inv.Slots[inventorySlotIndex];
        if (slot.IsEmpty) return;
        var def = ItemRegistry.Instance?.Get(slot.itemId);
        if (def == null || def.sellPrice <= 0) { SendSystemMsg("That item has no value."); return; }
        inv.RemoveItem(slot.itemId, 1);
        inv.AddCurrency(def.sellPrice);
        SendSystemMsg($"You sell {def.displayName} for {CurrencyUtil.Format(def.sellPrice)}.");
    }

    // ── Inventory commands ────────────────────────────────────────────────────

    [Command]
    public void CmdMoveInventorySlot(int fromIndex, int toIndex)
        => GetComponent<PlayerInventory>().MoveSlot(fromIndex, toIndex);

    [Command]
    public void CmdDropInventoryItem(int slotIndex)
    {
        var inv  = GetComponent<PlayerInventory>();
        var slot = inv.Slots[slotIndex];
        if (slot.IsEmpty) return;

        var def   = ItemRegistry.Instance?.Get(slot.itemId);
        string name = def != null ? def.displayName : slot.itemId;
        inv.DropItem(slotIndex);

        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.System, "System", $"You drop {name}."),
            connectionToClient);
    }
}
