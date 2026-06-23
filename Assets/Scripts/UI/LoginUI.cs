#if MIRROR
using Mirror;
#endif
using UnityEngine;

/// <summary>
/// Pre-connect login/register panel (1.4). Collects credentials, hands them to the
/// <see cref="AccountAuthenticator"/>, and starts the client — the server runs the async credential
/// check during the connect handshake. IMGUI to match the existing dev <see cref="GameNetworkHUD"/>
/// (which now only renders the connected status/stop). Host mode auto-fills the seeded dev account
/// (decision O5) so testing stays one click.
/// </summary>
public class LoginUI : MonoBehaviour
{
#if MIRROR
    string _address  = "localhost";
    string _username = "";
    string _password = "";

    const float PanelW = 230f;

    void OnGUI()
    {
        var nm = NetworkManager.singleton;
        if (nm == null) return;

        // Only the pre-connect state; once connected, GameNetworkHUD takes over.
        if (NetworkServer.active || NetworkClient.active) return;

        GUILayout.BeginArea(new Rect(Screen.width - PanelW - 8, 8, PanelW, 400));
        GUILayout.Label("<b>Ueq — Login</b>");

        GUILayout.Label("Server");
        _address          = GUILayout.TextField(_address);
        nm.networkAddress = _address;

        GUILayout.Space(4);
        GUILayout.Label("Username");
        _username = GUILayout.TextField(_username);
        GUILayout.Label("Password");
        _password = GUILayout.PasswordField(_password, '*');

        GUILayout.Space(6);
        if (GUILayout.Button("Login"))    Connect(register: false);
        if (GUILayout.Button("Register")) Connect(register: true);

        GUILayout.Space(6);
        if (GUILayout.Button("Host (dev account)")) HostAsDev();
        if (GUILayout.Button("Server Only"))        nm.StartServer();

        if (!string.IsNullOrEmpty(AccountAuthenticator.LastClientMessage))
        {
            GUILayout.Space(6);
            GUILayout.Label($"<color=#ff8080>{AccountAuthenticator.LastClientMessage}</color>");
        }

        GUILayout.EndArea();
    }

    void Connect(bool register)
    {
        if (!ApplyCredentials(_username, _password, register)) return;
        NetworkManager.singleton.StartClient();
    }

    // Host runs server + local client; the local client still authenticates, using the seeded dev account.
    void HostAsDev()
    {
        if (!ApplyCredentials(DatabaseSeeder.DevUsername, DatabaseSeeder.DevPassword, register: false)) return;
        _username = DatabaseSeeder.DevUsername;
        _password = DatabaseSeeder.DevPassword;
        NetworkManager.singleton.StartHost();
    }

    bool ApplyCredentials(string username, string password, bool register)
    {
        if (NetworkManager.singleton.authenticator is not AccountAuthenticator auth)
        {
            Debug.LogError("[Login] NetworkManager has no AccountAuthenticator assigned.");
            return false;
        }
        auth.clientUsername = username;
        auth.clientPassword = password;
        auth.clientRegister = register;
        return true;
    }
#endif
}
