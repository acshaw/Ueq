using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// Movement runs on both local client (smooth camera) and server (authority for other clients).
// The server's position is what NetworkTransform broadcasts to remote players.
// On a pure client (not host) we disable NetworkTransform receiving so local CharacterController
// isn't overwritten by server snapshots.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
public class NetworkedPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraHolder;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float sprintSpeed = 9f;
    [SerializeField] float jumpHeight = 1.5f;
    [SerializeField] float gravity = -20f;

    [Header("Look")]
    [SerializeField] float lookSensitivity = 0.15f;
    [SerializeField] float maxPitch = 80f;

    CharacterController _cc;
    float _pitch;
    float _yaw;
    float _verticalVelocity;

    // Input state written by local client, read by ApplyMovement on both client and server
    Vector2 _moveInput;
    bool _sprint;
    bool _jumpQueued;

    void Awake() => _cc = GetComponent<CharacterController>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        SetCameraActive(true);

        // Pure client: stop NetworkTransform from overwriting our locally-predicted position.
        // The host keeps it enabled so the server's position still broadcasts to other clients.
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
            CollectInput();
            ApplyLook();
            bool jumpThisFrame = _jumpQueued; // capture before ApplyMovement consumes it
            ApplyMovement();
            CmdSendInput(_moveInput, _yaw, _sprint, jumpThisFrame);
        }
        else if (isServer)
        {
            // Remote player on server: driven entirely by received Commands
            ApplyMovement();
        }
    }

    // ── Input (local client only) ─────────────────────────────────────────────

    void CollectInput()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * lookSensitivity;
            _pitch  = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -maxPitch, maxPitch);
        }

        _moveInput = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f)
        );
        _sprint = kb.leftShiftKey.isPressed;
        if (kb.spaceKey.wasPressedThisFrame) _jumpQueued = true;
    }

    void ApplyLook()
    {
        transform.eulerAngles = new Vector3(0f, _yaw, 0f);
        if (cameraHolder) cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    // ── Movement (runs on local client AND server) ────────────────────────────

    void ApplyMovement()
    {
        if (_cc.isGrounded) _verticalVelocity = -2f;

        if (_jumpQueued && _cc.isGrounded)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _jumpQueued = false; // always consume — prevents sticky jump if not grounded when flag arrives

        _verticalVelocity += gravity * Time.deltaTime;

        float speed = _sprint ? sprintSpeed : moveSpeed;
        Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        if (move.sqrMagnitude > 1f) move.Normalize();

        _cc.Move((move * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
    }

    // ── Server: receive input ─────────────────────────────────────────────────

    [Command]
    void CmdSendInput(Vector2 move, float yaw, bool sprint, bool jump)
    {
        _moveInput = move;
        _yaw       = yaw;
        _sprint    = sprint;
        if (jump && !isLocalPlayer) _jumpQueued = true; // host already processed jump locally

        // Keep server transform orientation in sync so movement direction is correct
        transform.eulerAngles = new Vector3(0f, _yaw, 0f);
    }

    // ── Combat stubs ──────────────────────────────────────────────────────────

    [Command]
    public void CmdAttack(NetworkIdentity target)
    {
        // TODO: validate range, line-of-sight, cooldown
        // target.GetComponent<Health>()?.TakeDamage(attackDamage, netIdentity);
    }
}
