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
    NetworkIdentity     _identity;

    GameObject _instance;
    Gender     _builtGender;
    string     _builtRace;
    string     _builtClass;
    bool       _built;

    void Awake()
    {
        _exp      = GetComponent<PlayerExperience>();
        _faction  = GetComponent<PlayerFactionScores>();
        _identity = GetComponent<NetworkIdentity>();
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

        // Shared recipe (3.1.6) — the create-form preview builds bodies the same way, so the two can't drift.
        // driveLocomotion: true adds PlayerAnimator so movement feeds the blend tree.
        _instance = CharacterModelFactory.Build(EnsureMount(), gender, race, cls,
            locomotionController, driveLocomotion: true, fallback: fallbackModel);

        // Each client builds its OWN copy of every player's body (see class doc comment) — hiding the
        // local player's own instance from rendering here has no effect on what other clients see of them.
        // ShadowsOnly (not disabling the renderer outright) keeps a grounding shadow under the player's
        // feet instead of an invisible-but-still-casts-nothing gap.
        if (_instance != null && _identity != null && _identity.isLocalPlayer)
            HideFromOwnCamera(_instance);
    }

    static void HideFromOwnCamera(GameObject instance)
    {
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
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

    void OnDestroy()
    {
        if (_instance != null) Destroy(_instance);
    }
}
