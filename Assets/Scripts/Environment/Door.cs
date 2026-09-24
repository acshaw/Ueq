using Mirror;
using UnityEngine;

/// <summary>
/// Click-to-interact swinging door (LMB — the project reserves RMB for camera look, so LMB stays the
/// button for clicking things in the world, same as click-to-target; wired from
/// NetworkedPlayer.TryTarget/CmdInteractDoor). Lives on the door frame/wall root (e.g. Synty's
/// SM_Bld_House_Wall_Door_01) — the actual door LEAF is a separate child transform whose pivot sits at
/// the hinge edge (Synty's own mesh convention), so this rotates that child, not the object Door itself
/// sits on. Server-authoritative: the SyncVar flip is the only thing sent over the network; every
/// client (including the one who clicked) animates the swing locally off the hook, so a few frames of
/// per-client swing-timing drift is the trade for not needing a NetworkTransform on the leaf.
/// </summary>
public class Door : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("The child transform that actually swings. Auto-resolved in Awake by name suffix " +
             "(\"_Door\") if left unassigned, matching Synty's own child-naming convention.")]
    [SerializeField] Transform doorLeaf;

    [Header("Swing")]
    [SerializeField] float closedYaw = 0f;
    [SerializeField] float openYaw = 100f;
    [SerializeField] float swingSpeed = 180f; // degrees/second

    [SyncVar(hook = nameof(OnOpenChanged))]
    bool _isOpen;

    Quaternion _targetLocalRot = Quaternion.identity;
    Collider _leafCollider;

    public bool IsOpen => _isOpen;

    void Awake() => ResolveLeaf();

    // 2026-09-22 — every effect of opening the door was gated behind `doorLeaf != null`, resolved
    // exactly once in Awake() by an immediate-children-only search. On at least the dedicated-server
    // process that lookup silently came back empty (confirmed: _isOpen flipped correctly server-side,
    // but the leaf never rotated and its collider's isTrigger never flipped — both effects share this
    // one gate) and there was no retry and no error, so the door just permanently stopped responding
    // with zero diagnostic trail. Hardened instead of re-chasing the exact original miss: searches the
    // full subtree (not just immediate children), and is called defensively from every place that
    // depends on doorLeaf — a one-time failed lookup can no longer be a permanent one.
    void ResolveLeaf()
    {
        if (doorLeaf == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t != transform && t.name.EndsWith("_Door")) { doorLeaf = t; break; }
            }
            if (doorLeaf == null)
            {
                Debug.LogError($"[Door] {name}: no descendant named '*_Door' found — this door cannot " +
                                "open. Check the prefab hierarchy or assign Door Leaf manually.");
                return;
            }
        }
        if (_leafCollider == null) _leafCollider = doorLeaf.GetComponent<Collider>();
    }

    public override void OnStartServer() => ApplyCurrentState();
    public override void OnStartClient() => ApplyCurrentState();

    // Snaps straight to the correct pose/collider state instead of animating in from closed — covers
    // both the "already-open door on a newly joined client" case and (via OnStartServer, which
    // OnStartClient never fires for on a dedicated server) the server's own initial state.
    void ApplyCurrentState()
    {
        ApplyOpenState(_isOpen);
        if (doorLeaf != null) doorLeaf.localRotation = _targetLocalRot; // snap, don't animate, on (re)join
    }

    void Update()
    {
        if (doorLeaf == null) ResolveLeaf();
        if (doorLeaf == null) return;
        doorLeaf.localRotation = Quaternion.RotateTowards(doorLeaf.localRotation, _targetLocalRot, swingSpeed * Time.deltaTime);
    }

    // The leaf becomes a TRIGGER the instant the door opens rather than once the swing finishes — a
    // player walking in right as they click would otherwise be shoved around by the still-animating
    // SOLID collider for the ~0.5s swing (rubber-banding, found 2026-09-21). Trigger, not `enabled =
    // false`: a disabled collider also stops being hittable by the click raycast, which is how the
    // door got un-clickable once opened (found right after). A trigger is ignored by
    // CharacterController.Move() (so it never blocks) but Physics.Raycast still hits it by default (so
    // it stays clickable to close again). The swing itself stays purely cosmetic either way.
    //
    // 2026-09-22 — confirmed via two independent fresh diagnostic passes that this hook never fires on
    // the dedicated-server process for a value IT set itself (_isOpen flips correctly, readable via
    // IsOpen, but this method body never executes there — no log, no effect). Whatever the exact cause,
    // ServerToggle() below no longer depends on the hook to update the server's own copy; it applies
    // the state directly and immediately, same as this method does for every client (confirmed working
    // there). This method still exists for clients/host, which do receive it correctly.
    void OnOpenChanged(bool _, bool isOpen) => ApplyOpenState(isOpen);

    void ApplyOpenState(bool isOpen)
    {
        ResolveLeaf();
        _targetLocalRot = Quaternion.Euler(0f, isOpen ? openYaw : closedYaw, 0f);
        if (_leafCollider != null) _leafCollider.isTrigger = isOpen;
    }

    [Server]
    public void ServerToggle()
    {
        _isOpen = !_isOpen;
        ApplyOpenState(_isOpen); // don't rely on the hook firing for the server's own instance — see above
    }
}
