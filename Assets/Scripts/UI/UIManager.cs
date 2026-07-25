using UnityEngine;

/// <summary>
/// Instantiates the HUD/menu layer at startup and keeps it alive across gameplay/zone scene
/// changes (so the HUD doesn't reload — the M3.5 zone-transition enabler).
///
/// 5.10 finding: this used to be a separate additively-loaded UI.unity scene, but that reliably
/// crashed a real standalone build ("... is corrupted!", native "Position out of bounds!" crash,
/// immediately on boot) — reproduced regardless of AV, build cache, graphics API, or scene
/// content, and splitting the panels across two scenes just moved the identical crash to a
/// different, previously-untouched scene. That pointed at a genuine engine-level bug in
/// packing/loading multiple scenes in this environment, not anything in our own content. Every
/// other asset type in this project already loads successfully via Resources.Load + Instantiate
/// (items, abilities, mob/character bodies) — none of which have ever hit this bug — so the HUD
/// is now a prefab (<c>Resources/UI/HudRoot</c>, built by <c>Tools/Build UI Scene</c>) instantiated
/// directly here instead.
///
/// Client-presentation only: a dedicated headless server (<see cref="Application.isBatchMode"/>) skips
/// the instantiate entirely. Panels bind through the static <see cref="LocalPlayer"/> service, so they
/// work regardless of which scene the player object lives in.
/// </summary>
public class UIManager : MonoBehaviour
{
    const string HudPrefabResourcePath = "UI/HudRoot";

    static UIManager _instance;
    static GameObject _hudInstance;

    [RuntimeInitializeOnLoadMethod] // reset for fast play-mode (no domain reload)
    static void ResetStatics()
    {
        _instance    = null;
        _hudInstance = null;
    }

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; } // survive gameplay-scene reloads as one
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (Application.isBatchMode) return; // headless server needs no UI

        if (_hudInstance == null)
        {
            var prefab = Resources.Load<GameObject>(HudPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] No HUD prefab found at Resources/{HudPrefabResourcePath} — " +
                                "run Tools/Build UI Scene.");
            }
            else
            {
                _hudInstance = Instantiate(prefab);
                _hudInstance.name = "HudRoot";
                DontDestroyOnLoad(_hudInstance);
            }
        }

        // 3.1.1 — the client screen-flow shell (Title → Login/Register → Character Select → In-World with
        // fade transitions). Replaces the IMGUI LoginUI / CharacterSelectUI (disabled at runtime).
        UIScreenManager.EnsureExists();
    }
}
