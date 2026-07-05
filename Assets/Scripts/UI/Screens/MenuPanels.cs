using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

// M3.1.2 — the title / start page. Login + registration are INLINE MODES of this screen (approach A): the
// whole pre-connect + connecting experience stays on the title art with no black-screen transition. There
// are no separate Login/Register/Connecting screens; the shell fade is reserved for the real milestone
// (entering Character Select on auth success). Styling of the inline form is 3.1.3.

/// <summary>Title / start page (3.1.2). Left-anchored menu column over a cover-fit art plate with a left
/// legibility scrim; the wordmark is a stylized TMP element (no PNG). Art is a drop-in swap: a Sprite at
/// <c>Resources/UI/Title/TitleBackground</c> becomes the background (else a procedural gradient), and a
/// TMP Font Asset at <c>Resources/UI/Title/TitleFont</c> restyles the wordmark. Login/Register live inline.</summary>
public class TitlePanel : ScreenPanel
{
    enum Mode { Menu, Login, Register }

    static readonly Color Accent    = new Color(0.78f, 0.56f, 0.28f, 0.96f); // primary action
    static readonly Color Muted     = new Color(0.12f, 0.14f, 0.20f, 0.85f); // Quit
    static readonly Color Secondary = new Color(0.18f, 0.20f, 0.26f, 0.72f); // low-weight form buttons
    static readonly Color Neutral   = new Color(0.87f, 0.89f, 0.93f, 0.9f);  // status text

    RectTransform _col;   // left column; child 0 is the persistent wordmark, the rest is the swappable form
    Mode _mode = Mode.Menu;

    TMP_InputField _address, _username, _password;
    Selectable[] _fields;                 // for Tab/Enter nav
    List<Selectable> _interactables;      // disabled while connecting
    TextMeshProUGUI _status;              // error / "Connecting…" line

    protected override void Build()
    {
        // 1. Background — real art (cover-fit) or a procedural gradient fallback.
        var bg = Resources.Load<Sprite>("UI/Title/TitleBackground");
        if (bg != null) MenuUI.CoverBackground(Root, bg);
        else MenuUI.GradientOverlay(Root, "Background",
            new Color(0.03f, 0.04f, 0.07f, 1f), new Color(0.10f, 0.12f, 0.20f, 1f), horizontal: false);

        // 2. Left legibility scrim so the menu/form reads over the lighter parts of the art.
        MenuUI.GradientOverlay(Root, "Scrim",
            new Color(0.02f, 0.03f, 0.05f, 0.80f), new Color(0.02f, 0.03f, 0.05f, 0f), horizontal: true);

        // 3. Left-anchored column: persistent wordmark + swappable form (Menu / Login / Register).
        _col = AnchoredColumn(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 40f),
            380f, TextAnchor.UpperLeft, 10f);
        BuildWordmark(_col);

        // 4. Dev controls — small, dim, tucked bottom-left (dev-only; won't ship).
        var dev = AnchoredColumn(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 24f),
            240f, TextAnchor.LowerLeft, 6f);
        var devLabel = MenuUI.Text(dev, "dev", 13, TextAlignmentOptions.Left);
        devLabel.color = new Color(0.72f, 0.74f, 0.78f, 0.6f);
        var devColor = new Color(0.14f, 0.16f, 0.22f, 0.8f);
        _address = MenuUI.Input(dev, "Server", false);   // dev-only; player login is username/password (L6)
        _address.text = "localhost";
        MenuUI.Button(dev, "Host (dev account)", () => Manager.HostAsDev(), devColor, 15, 30);
        MenuUI.Button(dev, "Server Only", () => Manager.StartServerOnly(), devColor, 15, 30);

        // 5. Version / build label, bottom-right.
        var ver = CornerText($"v{Application.version}", new Vector2(1f, 0f), new Vector2(-20f, 18f));
        ver.color = new Color(0.86f, 0.88f, 0.92f, 0.55f);

        SetMode(Mode.Menu);
    }

    void BuildWordmark(Transform parent)
    {
        var word = MenuUI.Text(parent, "UEQ", 104, TextAlignmentOptions.Left);
        word.characterSpacing = 8;
        // Drop-in fantasy wordmark font: a TMP Font Asset at Resources/UI/Title/TitleFont overrides the
        // default (else TMP default + faux-bold). No code change needed to swap the font.
        var titleFont = Resources.Load<TMP_FontAsset>("UI/Title/TitleFont");
        if (titleFont != null) word.font = titleFont;
        else word.fontStyle = FontStyles.Bold;
        word.enableVertexGradient = true;
        word.colorGradient = new VertexGradient(
            new Color(1.00f, 0.94f, 0.78f), new Color(1.00f, 0.94f, 0.78f),   // top (warm cream)
            new Color(0.92f, 0.71f, 0.40f), new Color(0.92f, 0.71f, 0.40f));  // bottom (gold)
        MenuUI.SetPreferredHeight(word.gameObject, 120);
        MenuUI.AddSoftShadow(word, new Color(0f, 0f, 0f, 0.9f), new Vector2(0.6f, -0.6f), 0.35f);
    }

    // ── Inline mode switching (no screen change, no fade) ────────────────────────

    void SetMode(Mode m)
    {
        _mode = m;
        // Clear everything below the persistent wordmark (child 0), then rebuild the requested form.
        for (int i = _col.childCount - 1; i >= 1; i--)
        {
            var ch = _col.GetChild(i);
            ch.SetParent(null, false);
            Destroy(ch.gameObject);
        }
        _username = _password = null; // _address is persistent (lives in the dev cluster), not rebuilt per mode
        _fields = null;
        _status = null;

        switch (m)
        {
            case Mode.Menu:     BuildMenu();     break;
            case Mode.Login:    BuildLogin();    break;
            case Mode.Register: BuildRegister(); break;
        }

        // Everything selectable below the wordmark (buttons + inputs) — toggled off while connecting.
        _interactables = new List<Selectable>(_col.GetComponentsInChildren<Selectable>(true));
    }

    void BuildMenu()
    {
        MenuUI.Spacer(_col, 18);
        MenuUI.Button(_col, "Play", () => SetMode(Mode.Login), Accent, 24, 52);
        MenuUI.Button(_col, "Quit", Quit, Muted, 22, 48);
    }

    void BuildLogin()
    {
        Heading("Sign In");
        _username = MenuUI.Input(_col, "Username", false);
        _password = MenuUI.Input(_col, "Password", true);
        MenuUI.Spacer(_col, 8);
        MenuUI.Button(_col, "Log In", DoLogin, Accent, 22, 48);                         // primary
        MenuUI.Button(_col, "Create Account", () => SetMode(Mode.Register), Secondary, 18, 40);
        MenuUI.Button(_col, "Back", () => SetMode(Mode.Menu), Secondary, 18, 40);
        _status = MenuUI.Text(_col, "", 18, TextAlignmentOptions.Left);
        _fields = new Selectable[] { _username, _password };
        MenuUI.Focus(_username);
    }

    void BuildRegister()
    {
        Heading("Create Account");
        _username = MenuUI.Input(_col, "Username", false);
        _password = MenuUI.Input(_col, "Password", true);
        MenuUI.Spacer(_col, 8);
        MenuUI.Button(_col, "Register & Connect", DoRegister, Accent, 22, 48);          // primary
        MenuUI.Button(_col, "Back", () => SetMode(Mode.Login), Secondary, 18, 40);
        _status = MenuUI.Text(_col, "", 18, TextAlignmentOptions.Left);
        _fields = new Selectable[] { _username, _password };
        MenuUI.Focus(_username);
    }

    void Heading(string text)
    {
        var h = MenuUI.Text(_col, text, 30, TextAlignmentOptions.Left);
        h.fontStyle = FontStyles.Bold;
        h.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        MenuUI.AddSoftShadow(h, new Color(0f, 0f, 0f, 0.8f), new Vector2(0.4f, -0.4f), 0.3f);
    }

    // Title (re)appears only on boot or after logout — always present the clean menu. During the login /
    // connecting dance the panel never hides (ComputeTarget keeps us on Title), so the mode persists and
    // a rejection's error stays visible without restoring it here.
    public override void OnShow() => SetMode(Mode.Menu);

    void Update()
    {
        if (_mode == Mode.Menu) return;

        bool connecting = NetworkClient.active; // started connecting, not yet authenticated (else → CharacterSelect)

        if (_status != null)
        {
            _status.text  = connecting ? "Connecting…" : (UIScreenManager.LastError ?? "");
            _status.color = connecting ? Neutral : MenuUI.ErrorColor;
        }
        if (_interactables != null)
            foreach (var s in _interactables) if (s != null) s.interactable = !connecting;

        if (MenuUI.BackPressed())
        {
            if (connecting) NetworkManager.singleton.StopClient();          // cancel a connect in progress
            else SetMode(_mode == Mode.Register ? Mode.Login : Mode.Menu);  // back out a mode
            return;
        }

        if (!connecting)
        {
            System.Action submit = _mode == Mode.Register ? (System.Action)DoRegister : DoLogin;
            MenuUI.HandleFormKeys(_fields, submit);
        }
    }

    void DoLogin()
    {
        if (NetworkClient.active) return; // already connecting
        if (_address != null) Manager.SetAddress(_address.text);
        Manager.Connect(_username.text.Trim(), _password.text, register: false);
    }

    void DoRegister()
    {
        if (NetworkClient.active) return;
        if (_address != null) Manager.SetAddress(_address.text);
        Manager.Connect(_username.text.Trim(), _password.text, register: true);
    }

    // ── Layout helpers ───────────────────────────────────────────────────────────

    RectTransform AnchoredColumn(Vector2 anchor, Vector2 pivot, Vector2 pos, float width,
                                 TextAnchor align, float spacing)
    {
        var go = new GameObject("Column", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(Root, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 0f);

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = align;
        vlg.spacing = spacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fit = go.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    TextMeshProUGUI CornerText(string s, Vector2 corner, Vector2 pos)
    {
        var go = new GameObject("Corner", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(Root, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300f, 24f);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = s;
        t.fontSize = 14;
        t.raycastTarget = false;
        t.alignment = corner.x > 0.5f ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        return t;
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
