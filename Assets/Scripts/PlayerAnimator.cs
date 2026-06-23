using UnityEngine;

/// <summary>
/// Drives a Humanoid Animator's locomotion blend tree from the character's
/// actual movement, measured purely from world-space position deltas.
///
/// Why this works across the network with zero extra code: the player root's
/// transform is synced to every client by NetworkTransformReliable, so on every
/// machine the character physically moves. Reading that movement locally gives
/// correct animation for BOTH the local player and remote players — no
/// NetworkAnimator, no Commands, no RPCs needed.
///
/// Attach to the visual child that holds the Animator (the Synty character).
/// The blend tree it feeds should use a float parameter named <see cref="speedParam"/>
/// with thresholds at 0 (idle), <see cref="walkSpeed"/> (walk), <see cref="runSpeed"/> (run).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Controller assigned in Awake if the Animator has none. Set this to " +
             "PlayerLocomotion so a lost/stale nested-prefab override can't leave the " +
             "character in a controllerless T-pose.")]
    [SerializeField] RuntimeAnimatorController controller;
    [Tooltip("Float parameter driven by horizontal move speed (metres/second).")]
    [SerializeField] string speedParam = "Speed";
    [Tooltip("Trigger parameter fired once per auto-attack swing.")]
    [SerializeField] string attackTrigger = "Attack";
    [Tooltip("Smoothing time for the Speed parameter. Higher = softer blends.")]
    [SerializeField] float dampTime = 0.1f;
    [Tooltip("Speeds below this read as idle (filters out sync jitter).")]
    [SerializeField] float idleThreshold = 0.05f;

    Animator _animator;
    int _speedHash;
    int _attackHash;
    Vector3 _lastPos;

    void Awake()
    {
        _animator   = GetComponent<Animator>();
        _speedHash  = Animator.StringToHash(speedParam);
        _attackHash = Animator.StringToHash(attackTrigger);

        // Self-heal a missing controller (e.g. the nested-prefab override was lost
        // when the controller asset was recreated) so the character animates instead
        // of T-posing.
        if (_animator.runtimeAnimatorController == null && controller != null)
            _animator.runtimeAnimatorController = controller;

        // Root motion off — movement is driven by the CharacterController on the
        // player root, not by the animation clips.
        _animator.applyRootMotion = false;
        _lastPos = transform.position;
    }

    // Reset baseline whenever re-enabled (e.g. after a respawn teleport) so the
    // teleport delta doesn't register as a one-frame sprint.
    void OnEnable() => _lastPos = transform.position;

    void Update()
    {
        // No controller → SetFloat would throw every frame. Skip quietly; Awake
        // already tried to self-heal and the prefab fix is the real remedy.
        if (_animator.runtimeAnimatorController == null) return;

        Vector3 delta = transform.position - _lastPos;
        delta.y = 0f; // ignore vertical (jump / gravity); locomotion is horizontal
        _lastPos = transform.position;

        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        if (speed < idleThreshold) speed = 0f;

        _animator.SetFloat(_speedHash, speed, dampTime, Time.deltaTime);
    }

    /// <summary>
    /// Fires the auto-attack swing animation. Called locally on every client by
    /// <see cref="PlayerAutoAttack"/>'s ClientRpc, so the swing shows for both the
    /// local player and remote players without a NetworkAnimator.
    /// </summary>
    public void PlayAttack() => _animator.SetTrigger(_attackHash);

    /// <summary>
    /// Fires an arbitrary Animator trigger by name — used by <see cref="PlayerAbilities"/>
    /// to drive per-ability animations (e.g. "Kick"). Guards against triggers the
    /// current controller doesn't declare, so an ability with no matching state
    /// just no-ops instead of spamming "parameter does not exist" warnings.
    /// </summary>
    public void PlayTrigger(string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return;
        foreach (var p in _animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
            {
                _animator.SetTrigger(trigger);
                return;
            }
    }
}
