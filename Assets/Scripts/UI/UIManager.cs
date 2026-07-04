using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the additive UI layer (1.7). The HUD/menu canvases live in their own <c>UI.unity</c> scene
/// built by <c>Tools/Build UI Scene</c>; this loads it additively at startup and keeps it alive across
/// gameplay/zone scene changes (so the HUD doesn't reload — the M3.5 zone-transition enabler).
///
/// Client-presentation only: a dedicated headless server (<see cref="Application.isBatchMode"/>) skips
/// the load entirely. Panels bind through the static <see cref="LocalPlayer"/> service, so they work
/// regardless of which scene the player object lives in.
/// </summary>
public class UIManager : MonoBehaviour
{
    const string UISceneName = "UI";

    static UIManager _instance;

    [RuntimeInitializeOnLoadMethod] // reset for fast play-mode (no domain reload)
    static void ResetStatics() => _instance = null;

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; } // survive gameplay-scene reloads as one
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (Application.isBatchMode) return; // headless server needs no UI

        var ui = SceneManager.GetSceneByName(UISceneName);
        if (!ui.isLoaded)
            SceneManager.LoadSceneAsync(UISceneName, LoadSceneMode.Additive);

        // 3.1.1 — the client screen-flow shell (Title → Login/Register → Character Select → In-World with
        // fade transitions). Replaces the IMGUI LoginUI / CharacterSelectUI (disabled at runtime).
        UIScreenManager.EnsureExists();
    }
}
