using Mirror;
using UnityEngine;

/// <summary>
/// 3.1.10 Stage 0 — a mob's body is a plain art prefab loaded at runtime, mirroring <see cref="PlayerModel"/>.
/// One shared networked Enemy prefab; the visual comes from <c>Resources/MobModels/&lt;modelId&gt;</c>, so adding
/// a mob body is "drop a Synty prefab in that folder and name it," not "build a whole networked Mirror prefab."
///
/// <para><b>modelId</b> is resolved server-side from the mob's <see cref="MobDefinition"/> (explicit
/// <c>modelId</c>, else the mob id by convention), synced to clients, and each client instantiates the body
/// under an auto-created <c>ModelRoot</c> — wiring the shared locomotion Animator. The root's placeholder mesh
/// (the primitive cube) is hidden once a body resolves; a missing model leaves the cube visible as an obvious
/// "art not found" marker. The cube's collider stays as the click-to-target volume either way.</para>
/// </summary>
public class MobModel : NetworkBehaviour
{
    [Header("Mount")]
    [Tooltip("Where the body attaches. Auto-created as a child 'ModelRoot' at mountOffset if left empty.")]
    [SerializeField] Transform modelMount;
    [Tooltip("Local offset for the auto-created mount — feet at the base of the 1u placeholder cube.")]
    [SerializeField] Vector3   mountOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Animation")]
    [Tooltip("Optional shared locomotion controller — Synty mob bodies are Humanoid, so they retarget it " +
             "(movement drives idle/walk). Left empty, the body shows in its own/bind pose.")]
    [SerializeField] RuntimeAnimatorController locomotionController;

    [Tooltip("Optional body used when Resources/MobModels/<modelId> is missing (dev safety net).")]
    [SerializeField] GameObject fallbackModel;

    const string ResourceDir = "MobModels/";

    // Server-set from the MobDefinition; synced so clients know which body to build (MobDefinition is
    // server-only, so the id has to ride a SyncVar — same idea as Nameplate's label).
    [SyncVar(hook = nameof(OnModelIdChanged))]
    string _modelId = "";

    GameObject _instance;
    string     _built;

    // SpawnPoint calls MobApplicator.SetDefinition before NetworkServer.Spawn, so the definition is available
    // here. Explicit modelId wins; blank falls back to the mob id (MobRegistry sets def.name = the mob id).
    public override void OnStartServer()
    {
        var def = GetComponent<MobApplicator>()?.Definition;
        if (def != null)
            _modelId = !string.IsNullOrEmpty(def.modelId) ? def.modelId : def.name;
    }

    // Initial SyncVar state is applied before OnStartClient, so the id is ready to build here. A dedicated
    // (headless) server never runs OnStartClient, so it builds no body — nothing to render there.
    public override void OnStartClient()
    {
        if (!string.IsNullOrEmpty(_modelId)) Build(_modelId);
    }

    void OnModelIdChanged(string _, string next)
    {
        if (!string.IsNullOrEmpty(next)) Build(next);
    }

    void Build(string modelId)
    {
        if (_built == modelId) return;
        _built = modelId;

        if (_instance != null) Destroy(_instance);

        var placeholder = GetComponent<MeshRenderer>();

        // Resolution order: MobModelCatalog (referenced in place; optional per-body controller override) →
        // Resources/MobModels/<modelId> convention (zero-setup) → serialized fallback body.
        GameObject prefab;
        RuntimeAnimatorController controller = locomotionController;
        Vector3 offset = Vector3.zero, eulerOffset = Vector3.zero;
        if (MobModelRegistry.TryGet(modelId, out var entry))
        {
            prefab = entry.prefab;
            if (entry.animatorController != null) controller = entry.animatorController; // non-Humanoid rig
            offset      = entry.offset;
            eulerOffset = entry.eulerOffset;
        }
        else
        {
            prefab = Resources.Load<GameObject>(ResourceDir + modelId);
        }
        if (prefab == null) prefab = fallbackModel;

        if (prefab == null)
        {
            Debug.LogWarning($"[MobModel] '{name}': no body for modelId '{modelId}'. Add it to the catalog " +
                             "(Tools/Character/Build Mob Model Catalog, then set the entry's modelId) or drop a " +
                             $"prefab named '{modelId}' in Assets/Resources/{ResourceDir}. Showing the placeholder.");
            if (placeholder != null) placeholder.enabled = true; // keep the cube as a "missing art" marker
            return;
        }

        // Hide the placeholder cube BEFORE building the body, so nothing in the build path (an exception,
        // an early return) can leave the cube floating around the real model. The cube's collider stays as
        // the click-to-target volume; only the renderer is hidden.
        if (placeholder != null)
            placeholder.enabled = false;
        else
            Debug.LogWarning($"[MobModel] '{name}': no MeshRenderer found on this GameObject to hide — the " +
                             "placeholder cube may remain visible. Is MobModel on the Enemy prefab root (the cube)?");

        _instance = CharacterModelFactory.BuildFromPrefab(EnsureMount(), prefab, controller, driveLocomotion: true);
        if (_instance != null)
        {
            _instance.name = $"MobModel_{modelId}";
            if (offset != Vector3.zero) _instance.transform.localPosition = offset;
            if (eulerOffset != Vector3.zero) _instance.transform.localRotation = Quaternion.Euler(eulerOffset);
            FitTargetCollider(_instance);
        }
    }

    // Resize the placeholder's box collider (the click-to-target volume) to fit the built body, so large mobs
    // aren't targeted by a tiny 1u cube offset from the model. Vertical extent comes from the body's rendered
    // bounds; the footprint is a fixed upright value so a T-pose (arms out) on the first frame can't produce a
    // huge wide box. Client-side only (the body only exists on clients) — server range checks use transform
    // distance, not this collider, so it never needs to match server-side.
    void FitTargetCollider(GameObject body)
    {
        var box = GetComponent<BoxCollider>();
        if (box == null || body == null) return;

        var renderers = body.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float height       = Mathf.Max(bounds.size.y, 0.5f);
        float localCenterY = transform.InverseTransformPoint(bounds.center).y;

        box.center = new Vector3(0f, localCenterY, 0f);
        box.size   = new Vector3(0.7f, height, 0.7f);
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
