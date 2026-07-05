using Mirror;
using UnityEngine;

/// <summary>
/// Shows the body that matches a character's identity (3.1.4, RM4). Replaces the Player prefab's single
/// hard-parented Synty child: at runtime it reads the synced (gender, race, class) — gender + class from
/// <see cref="PlayerExperience"/>, race from <see cref="PlayerFactionScores"/> — looks the tuple up in the
/// <see cref="CharacterRosterRegistry"/>, and instantiates that Synty body under a mount point, wiring the
/// locomotion Animator + <see cref="PlayerAnimator"/> onto it.
///
/// Runs on every client (each peer builds its own visual for every player; skipped on a headless server).
/// Rebuilds only when the tuple changes — the identity SyncVars arrive a moment after spawn, so this
/// polls until they resolve rather than depending on a single hook firing at the right time.
/// </summary>
public class PlayerModel : MonoBehaviour
{
    [Header("Mount")]
    [Tooltip("Where the body attaches. Auto-created as a child 'ModelRoot' at mountOffset if left empty.")]
    [SerializeField] Transform modelMount;
    [Tooltip("Local offset for the auto-created mount (feet at the capsule base).")]
    [SerializeField] Vector3   mountOffset = new Vector3(0f, -1f, 0f);

    [Header("Animation")]
    [Tooltip("PlayerLocomotion.controller — assigned to the instantiated body's Animator.")]
    [SerializeField] RuntimeAnimatorController locomotionController;

    [Tooltip("Optional body used when the (gender,race,class) tuple has no roster model (dev safety net).")]
    [SerializeField] GameObject fallbackModel;

    PlayerExperience    _exp;
    PlayerFactionScores _faction;

    GameObject _instance;
    Gender     _builtGender;
    string     _builtRace;
    string     _builtClass;
    bool       _built;

    void Awake()
    {
        _exp     = GetComponent<PlayerExperience>();
        _faction = GetComponent<PlayerFactionScores>();
    }

    void Update()
    {
        // Visual only — a dedicated (non-client) server has nothing to render.
        if (!NetworkClient.active) return;

        Gender gender = _exp != null ? _exp.Gender : Gender.Male;
        string race   = _faction != null ? _faction.ActualRace : "";
        string cls    = _exp != null ? _exp.ClassName : "";

        // Wait for a resolved identity before the first build (class name arrives via SyncVar just after spawn).
        if (string.IsNullOrEmpty(race) || string.IsNullOrEmpty(cls)) return;

        if (_built && gender == _builtGender && race == _builtRace && cls == _builtClass) return;

        Rebuild(gender, race, cls);
    }

    void Rebuild(Gender gender, string race, string cls)
    {
        _builtGender = gender; _builtRace = race; _builtClass = cls; _built = true;

        if (_instance != null) Destroy(_instance);

        var prefab = CharacterRosterRegistry.GetModel(gender, race, cls) ?? fallbackModel;
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerModel] No body model for {gender}/{race}/{cls} and no fallback assigned.");
            return;
        }

        var mount = EnsureMount();
        _instance = Instantiate(prefab, mount);
        _instance.transform.localPosition = Vector3.zero;
        _instance.transform.localRotation = Quaternion.identity;
        _instance.name = $"Model_{gender}_{race}_{cls}";

        WireAnimator(_instance);
    }

    Transform EnsureMount()
    {
        if (modelMount != null) return modelMount;
        var go = new GameObject("ModelRoot");
        modelMount = go.transform;
        modelMount.SetParent(transform, false);
        modelMount.localPosition = mountOffset;
        return modelMount;
    }

    // Set the controller BEFORE adding PlayerAnimator so its Awake sees a live controller (no T-pose).
    void WireAnimator(GameObject instance)
    {
        var anim = instance.GetComponentInChildren<Animator>();
        if (anim == null) return; // a prop-only model with no rig — nothing to drive
        if (locomotionController != null) anim.runtimeAnimatorController = locomotionController;
        else Debug.LogWarning("[PlayerModel] No locomotion controller assigned — the body will not animate.");
        anim.applyRootMotion = false;
        if (anim.GetComponent<PlayerAnimator>() == null) anim.gameObject.AddComponent<PlayerAnimator>();
    }

    void OnDestroy()
    {
        if (_instance != null) Destroy(_instance);
    }
}
