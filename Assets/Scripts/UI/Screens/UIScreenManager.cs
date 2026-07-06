using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M3.1.1 (SF1/SF2/SF5) — the client screen-flow shell. Builds a menu canvas + the front-end screens
/// (Title → Login/Register → Connecting → Character Select → In-World) and a <see cref="ScreenFader"/>, then
/// each frame derives the target screen from Mirror connection state and runs a fade transition when it
/// changes. Replaces the IMGUI <c>LoginUI</c>/<c>CharacterSelectUI</c> (disabled at runtime, SF3) with uGUI
/// panels that reuse the same Mirror messages + <see cref="AccountAuthenticator"/> hooks — no server change.
/// Spawned by <see cref="UIManager"/>; client-presentation only.
/// </summary>
public class UIScreenManager : MonoBehaviour
{
    public static UIScreenManager Instance { get; private set; }

    Canvas _canvas;
    ScreenFader _fader;
    Camera _menuCamera;
    readonly Dictionary<ClientScreen, ScreenPanel> _panels = new();

    ClientScreen _current = ClientScreen.InWorld;   // sentinel — no panel, so the first show renders Title
    bool _transitioning;

    public static void EnsureExists()
    {
        if (Instance != null || Application.isBatchMode) return;
        var go = new GameObject("UIScreenManager");
        go.AddComponent<UIScreenManager>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        DisableLegacyImgui();
        BuildMenuCamera();
        BuildCanvas();
        BuildPanels();
    }

    void Start()
    {
        // Immediate, un-faded first show so boot lands on the pre-connect screen.
        _current = ComputeTarget();
        SwapTo(_current, fade: false);
    }

    void Update()
    {
        if (_transitioning) return;
        var target = ComputeTarget();
        if (target != _current) StartCoroutine(ShowRoutine(target));
    }

    // ── Screen derivation (SF5) ────────────────────────────────────────────────

    ClientScreen ComputeTarget()
    {
        if (NetworkClient.active && NetworkClient.localPlayer != null) return ClientScreen.InWorld;
        if (NetworkClient.active && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
            return ClientScreen.CharacterSelect;
        // Disconnected OR connecting-not-yet-authenticated: the Title hosts login/register/connecting inline,
        // so the pre-connect flow never leaves the title art (no fade until auth success).
        return ClientScreen.Title;
    }

    // ── Transitions ────────────────────────────────────────────────────────────

    IEnumerator ShowRoutine(ClientScreen target)
    {
        _transitioning = true;
        yield return _fader.Transition(() => SwapTo(target, fade: true));
        _transitioning = false;
    }

    const float ExitTimeout = 2f; // CP4 — max wait for the despawn before assuming a refused camp

    /// <summary>3.1.8 CP1 — scripted exit-to-menu that covers a server-side teardown with a fade: fade to black
    /// FIRST, run <paramref name="teardown"/> (e.g. send CampMessage) under black, wait for Mirror to leave
    /// In-World, then swap + reveal on the resolved screen. So the player-pop / camera gap never shows. If the
    /// despawn never lands within <see cref="ExitTimeout"/> the action was refused server-side (CP4) → reveal
    /// back to In-World instead of stranding the screen black.</summary>
    public void ExitWorld(System.Action teardown)
    {
        if (_transitioning) return;
        StartCoroutine(ExitRoutine(teardown));
    }

    IEnumerator ExitRoutine(System.Action teardown)
    {
        _transitioning = true;
        yield return _fader.Cover();          // cover the world before anything despawns

        teardown?.Invoke();                    // e.g. NetworkClient.Send(new CampMessage()) — despawns under black

        float t = 0f;
        var target = ComputeTarget();
        while (target == ClientScreen.InWorld && t < ExitTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
            target = ComputeTarget();
        }

        SwapTo(target, fade: true);            // CharacterSelect on success, or back to InWorld if refused
        yield return _fader.Reveal();
        _transitioning = false;
    }

    void SwapTo(ClientScreen target, bool fade)
    {
        if (_panels.TryGetValue(_current, out var cur) && cur != null)
        {
            cur.OnHide();
            cur.gameObject.SetActive(false);
        }
        _current = target;
        if (_panels.TryGetValue(_current, out var next) && next != null)
        {
            next.gameObject.SetActive(true);
            next.OnShow();
        }

        // In-world the player's own camera renders; elsewhere the menu camera backs the shell (and stops
        // Unity's "No cameras rendering" placeholder appearing while no gameplay camera exists).
        if (_menuCamera != null) _menuCamera.gameObject.SetActive(_current != ClientScreen.InWorld);
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    void BuildMenuCamera()
    {
        var go = new GameObject("MenuCamera");
        go.transform.SetParent(transform, false);
        _menuCamera = go.AddComponent<Camera>();
        _menuCamera.clearFlags = CameraClearFlags.SolidColor;
        _menuCamera.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
        _menuCamera.cullingMask = 0;   // renders no scene geometry — it only clears + satisfies the "a camera exists" check
        _menuCamera.depth = -100;      // below any gameplay camera
    }

    void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100; // above the in-world HUD

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
    }

    void BuildPanels()
    {
        CreatePanel<TitlePanel>(ClientScreen.Title);   // hosts login/register/connecting inline (3.1.2)
        CreatePanel<CharacterSelectPanel>(ClientScreen.CharacterSelect);
        // InWorld has no panel — all menu panels hide, the HUD shows through.

        var faderRt = MenuUI.FullScreen(_canvas.transform, "ScreenFader");
        _fader = faderRt.gameObject.AddComponent<ScreenFader>();
        faderRt.SetAsLastSibling(); // render on top of every panel + the HUD during a transition
    }

    void CreatePanel<T>(ClientScreen screen) where T : ScreenPanel
    {
        var rt = MenuUI.FullScreen(_canvas.transform, screen + "Panel");
        var panel = rt.gameObject.AddComponent<T>();
        panel.Init(this);
        _panels[screen] = panel;
    }

    // ── Mirror / auth glue (reused from the retired LoginUI) ─────────────────────

    public void SetAddress(string address)
    {
        if (NetworkManager.singleton != null && !string.IsNullOrWhiteSpace(address))
            NetworkManager.singleton.networkAddress = address;
    }

    public bool Connect(string username, string password, bool register)
    {
        if (!ApplyCredentials(username, password, register)) return false;
        NetworkManager.singleton.StartClient();
        return true;
    }

    public void HostAsDev()
    {
        if (!ApplyCredentials(DatabaseSeeder.DevUsername, DatabaseSeeder.DevPassword, register: false)) return;
        NetworkManager.singleton.StartHost();
    }

    public void StartServerOnly()
    {
        if (NetworkManager.singleton != null) NetworkManager.singleton.StartServer();
    }

    /// <summary>Drop the connection and return to the Title screen (Log Out from character select).</summary>
    public void Disconnect()
    {
        var nm = NetworkManager.singleton;
        if (nm == null) return;
        if (NetworkServer.active && NetworkClient.active) nm.StopHost();      // dev host
        else if (NetworkClient.active)                    nm.StopClient();
    }

    bool ApplyCredentials(string username, string password, bool register)
    {
        if (NetworkManager.singleton == null) { Debug.LogError("[Shell] No NetworkManager."); return false; }
        if (NetworkManager.singleton.authenticator is not AccountAuthenticator auth)
        {
            Debug.LogError("[Shell] NetworkManager has no AccountAuthenticator assigned.");
            return false;
        }
        AccountAuthenticator.ClearClientMessage(); // drop any stale feedback before a fresh attempt
        auth.clientUsername = username;
        auth.clientPassword = password;
        auth.clientRegister = register;
        return true;
    }

    public static string LastError => AccountAuthenticator.LastClientMessage;

    // Disable the pre-1.7 IMGUI front-end so it doesn't render alongside the shell (SF3). Kept as files;
    // removed from the builder later. GameNetworkHUD (dev Stop) is intentionally left alone.
    static void DisableLegacyImgui()
    {
        foreach (var c in FindObjectsByType<LoginUI>(FindObjectsSortMode.None)) c.enabled = false;
        foreach (var c in FindObjectsByType<CharacterSelectUI>(FindObjectsSortMode.None)) c.enabled = false;
    }
}
