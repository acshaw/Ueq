using UnityEngine;
using UnityEngine.UI;
using TMPro;

// M3.1.1 — the simple front-end screens. Minimal/unstyled (SF6); visual design is 3.1.2/3.1.3.

/// <summary>Title / start page (SF7 stub). Background + logo load from Resources by convention so
/// stylizing (3.1.2) is a drop-in: put PNGs at <c>Resources/UI/Title/TitleBackground</c> +
/// <c>…/TitleLogo</c> (import as Sprite). No code change needed to swap the art.</summary>
public class TitlePanel : ScreenPanel
{
    protected override void Build()
    {
        var bg = Resources.Load<Sprite>("UI/Title/TitleBackground");
        if (bg != null)
        {
            var img = MenuUI.FullScreenImage(Root, "Background", Color.white);
            img.sprite = bg;
            img.type = Image.Type.Simple;
            img.preserveAspect = false; // proper cover-fit is a 3.1.2 styling concern
        }
        else MenuUI.FullScreenImage(Root, "Background", new Color(0.05f, 0.06f, 0.09f, 1f));

        var card = MenuUI.Card(Root, 460, 420);

        var logo = Resources.Load<Sprite>("UI/Title/TitleLogo");
        if (logo != null)
        {
            var go = new GameObject("Logo", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var img = go.AddComponent<Image>();
            img.sprite = logo;
            img.preserveAspect = true;
            img.raycastTarget = false;
            MenuUI.SetPreferredHeight(go, 140);
        }
        else MenuUI.Text(card, "UEQ", 64, TextAlignmentOptions.Center);

        MenuUI.Text(card, "A multiplayer RPG", 20, TextAlignmentOptions.Center);
        MenuUI.Spacer(card, 12);
        MenuUI.Button(card, "Play", () => Manager.GoTo(ClientScreen.Login));
        MenuUI.Button(card, "Quit", Quit);
        MenuUI.Spacer(card, 10);
        MenuUI.Text(card, "— dev —", 14, TextAlignmentOptions.Center);
        MenuUI.Button(card, "Host (dev account)", () => Manager.HostAsDev());
        MenuUI.Button(card, "Server Only", () => Manager.StartServerOnly());
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

/// <summary>Log-in screen (styling → 3.1.3). Reuses the same auth glue the IMGUI LoginUI used.</summary>
public class LoginPanel : ScreenPanel
{
    TMP_InputField _address, _username, _password;
    Selectable[] _fields;
    TextMeshProUGUI _error;

    protected override void Build()
    {
        MenuUI.FullScreenImage(Root, "Dim", new Color(0.05f, 0.06f, 0.09f, 1f));
        var card = MenuUI.Card(Root, 440, 470);

        MenuUI.Text(card, "Log In", 32, TextAlignmentOptions.Center);
        _address  = MenuUI.Input(card, "Server", false);
        _address.text = "localhost";
        _username = MenuUI.Input(card, "Username", false);
        _password = MenuUI.Input(card, "Password", true);
        MenuUI.Spacer(card, 6);
        MenuUI.Button(card, "Log In", DoLogin);
        MenuUI.Button(card, "Create Account", () => Manager.GoTo(ClientScreen.Register));
        MenuUI.Button(card, "Back", () => Manager.GoTo(ClientScreen.Title));
        _error = MenuUI.Text(card, "", 18, TextAlignmentOptions.Center);
        _error.color = MenuUI.ErrorColor;

        _fields = new Selectable[] { _username, _password, _address };
    }

    public override void OnShow() => MenuUI.Focus(_username);

    void DoLogin()
    {
        Manager.SetAddress(_address.text);
        Manager.Connect(_username.text.Trim(), _password.text, register: false);
    }

    void Update()
    {
        if (_error != null) _error.text = UIScreenManager.LastError ?? "";
        MenuUI.HandleFormKeys(_fields, DoLogin);
        if (MenuUI.BackPressed()) Manager.GoTo(ClientScreen.Title);
    }
}

/// <summary>Registration screen (styling → 3.1.3).</summary>
public class RegisterPanel : ScreenPanel
{
    TMP_InputField _username, _password;
    Selectable[] _fields;
    TextMeshProUGUI _error;

    protected override void Build()
    {
        MenuUI.FullScreenImage(Root, "Dim", new Color(0.05f, 0.06f, 0.09f, 1f));
        var card = MenuUI.Card(Root, 440, 420);

        MenuUI.Text(card, "Create Account", 32, TextAlignmentOptions.Center);
        _username = MenuUI.Input(card, "Username", false);
        _password = MenuUI.Input(card, "Password", true);
        MenuUI.Spacer(card, 6);
        MenuUI.Button(card, "Register & Connect", DoRegister);
        MenuUI.Button(card, "Back", () => Manager.GoTo(ClientScreen.Login));
        _error = MenuUI.Text(card, "", 18, TextAlignmentOptions.Center);
        _error.color = MenuUI.ErrorColor;

        _fields = new Selectable[] { _username, _password };
    }

    public override void OnShow() => MenuUI.Focus(_username);

    void DoRegister() => Manager.Connect(_username.text.Trim(), _password.text, register: true);

    void Update()
    {
        if (_error != null) _error.text = UIScreenManager.LastError ?? "";
        MenuUI.HandleFormKeys(_fields, DoRegister);
        if (MenuUI.BackPressed()) Manager.GoTo(ClientScreen.Login);
    }
}

/// <summary>Shown while the client is connecting/authenticating.</summary>
public class ConnectingPanel : ScreenPanel
{
    protected override void Build()
    {
        MenuUI.FullScreenImage(Root, "Dim", new Color(0.05f, 0.06f, 0.09f, 1f));
        var card = MenuUI.Card(Root, 360, 180);
        MenuUI.Text(card, "Connecting…", 28, TextAlignmentOptions.Center);
        MenuUI.Button(card, "Cancel", Cancel);
    }

    void Update()
    {
        if (MenuUI.BackPressed()) Cancel();
    }

    static void Cancel()
    {
        if (Mirror.NetworkClient.active) Mirror.NetworkManager.singleton.StopClient();
    }
}
