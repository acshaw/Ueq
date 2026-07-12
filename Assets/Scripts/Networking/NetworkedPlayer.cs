using Mirror;
using UnityEngine;
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

    Vector2 _moveInput;
    bool _sprint;
    bool _jumpQueued;
    bool _isLooking; // true while RMB held
    bool _rmbIsLoot; // RMB press was consumed by loot — suppress look mode for that press

    Targetable      _currentTarget;
    NetworkIdentity _serverTarget;

    public event System.Action<Targetable> OnTargetChanged;

    Vector3 _lastChatPos;
    const float ChatPosUpdateThreshold = 5f;

    // Readable by server-side components (e.g. PlayerAutoAttack)
    public NetworkIdentity ServerTarget => _serverTarget;

    // Client-side access for HotbarUI to pass target into CmdCastAbility
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

            // 3.1 — sync rotation so REMOTE players visibly turn (the server sets yaw in CmdSendInput; the
            // prefab has syncRotation off). The body transform is yaw-only — pitch lives on the camera
            // holder — so this syncs facing, not the camera. No cost to the owner: the local player's NT is
            // disabled on clients (OnStartLocalPlayer) and the host renders its own transform, so this only
            // affects how OTHER players see this one. (Overrides the prefab's serialized syncRotation:0.)
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

        if (!isServer)
        {
            foreach (var b in GetComponents<Behaviour>())
            {
                if (b.GetType().Name.Contains("NetworkTransform"))
                {
                    b.enabled = false;
                    break;
                }
            }
        }
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
            bool jumpThisFrame = _jumpQueued;
            ApplyMovement();
            CmdSendInput(_moveInput, _yaw, _sprint, jumpThisFrame);
        }
        else if (isServer)
        {
            ApplyMovement();
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

        if (hit == _currentTarget) return; // no change

        _currentTarget?.SetHighlight(false);
        _currentTarget = hit;
        _currentTarget?.SetHighlight(true);

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

    void ApplyMovement()
    {
        if (_cc.isGrounded) _verticalVelocity = -2f;

        if (_jumpQueued && _cc.isGrounded)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _jumpQueued = false;

        _verticalVelocity += gravity * Time.deltaTime;

        float speed = _sprint ? sprintSpeed : moveSpeed;
        Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        _cc.Move((move * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
    }

    // ── Server ────────────────────────────────────────────────────────────────

    [Command]
    void CmdSendInput(Vector2 move, float yaw, bool sprint, bool jump)
    {
        _moveInput = move;
        _yaw       = yaw;
        _sprint    = sprint;
        if (jump && !isLocalPlayer) _jumpQueued = true;

        // 3.1.7 — moving stands a seated player (server-authoritative). Pressing a move key stands you, then
        // ApplyMovement carries you off — no need to block movement while seated.
        if (move.sqrMagnitude > 0.01f) _sitting?.ServerStand();

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

    [Command]
    void CmdSetTarget(NetworkIdentity target) => _serverTarget = target;

    // ── Death handling ────────────────────────────────────────────────────────

    [Server]
    void HandlePlayerDeath(NetworkIdentity attacker)
    {
        var health = GetComponent<Health>();
        if (health.IsImmune) return;
        health.SetImmunity(10f);

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
        if (!string.IsNullOrEmpty(zoneId)) _zoneId = zoneId;
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
    }

    // ── Loot commands ─────────────────────────────────────────────────────────

    const float LootRange = 6f;

    [Command]
    public void CmdTakeLootSlot(NetworkIdentity corpseId, int slotIndex)
    {
        var inv = GetComponent<PlayerInventory>();
        if (TryGetMobCorpse(corpseId, out var mob))
        {
            var slot = mob.PeekSlot(slotIndex);
            if (slot.IsEmpty) return;
            if (inv.AddItem(slot.itemId, slot.quantity, enforceLore: true)) mob.RemoveSlot(slotIndex);
            else SendSystemMsg(AcquireBlockedMsg(inv, slot.itemId));
            return;
        }
        if (TryGetPlayerCorpse(corpseId, out var pc))
        {
            var slot = pc.PeekSlot(slotIndex);
            if (slot.IsEmpty) return;
            if (inv.AddItem(slot.itemId, slot.quantity, enforceLore: true)) pc.RemoveSlot(slotIndex);
            else SendSystemMsg(AcquireBlockedMsg(inv, slot.itemId));
        }
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
        if (TryGetMobCorpse(corpseId, out var mob))
        {
            int c = mob.TakeCopper();
            if (c > 0) GetComponent<PlayerInventory>().AddCurrency(c);
            return;
        }
        if (TryGetPlayerCorpse(corpseId, out var pc))
        {
            int c = pc.TakeCopper();
            if (c > 0) GetComponent<PlayerInventory>().AddCurrency(c);
        }
    }

    [Command]
    public void CmdTakeLootAll(NetworkIdentity corpseId)
    {
        var inv = GetComponent<PlayerInventory>();
        if (TryGetMobCorpse(corpseId, out var mob))  { mob.TakeAll(inv); return; }
        if (TryGetPlayerCorpse(corpseId, out var pc)) { pc.TakeAll(inv); }
    }

    bool TryGetMobCorpse(NetworkIdentity id, out Corpse corpse)
    {
        corpse = id?.GetComponent<Corpse>();
        if (corpse == null || !corpse.IsActive) { corpse = null; return false; }
        if (Vector3.Distance(transform.position, id.transform.position) > LootRange) { corpse = null; return false; }
        return true;
    }

    bool TryGetPlayerCorpse(NetworkIdentity id, out PlayerCorpse corpse)
    {
        corpse = id?.GetComponent<PlayerCorpse>();
        if (corpse == null || !corpse.IsActive)      { corpse = null; return false; }
        if (corpse.Owner != netIdentity)              { corpse = null; return false; }
        if (Vector3.Distance(transform.position, id.transform.position) > LootRange) { corpse = null; return false; }
        return true;
    }

    void SendSystemMsg(string text) =>
        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.System, "System", text), connectionToClient);

    // ── Ability commands ──────────────────────────────────────────────────────

    [Command]
    public void CmdCastAbility(int hotbarSlot, NetworkIdentity target)
        => GetComponent<PlayerAbilities>().TryCast(hotbarSlot, target);

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
