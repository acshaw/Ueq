#if MIRROR
using Mirror;
#endif
using UnityEngine;

// Connected-state network HUD (status + Stop) drawn at the top-right of the screen.
// Pre-connect login/connect is handled by LoginUI (1.4); this only renders once a
// server or client is active so the two panels never overlap.
public class GameNetworkHUD : MonoBehaviour
{
#if MIRROR
    const float PanelW = 210f;

    void OnGUI()
    {
        var nm = NetworkManager.singleton;
        if (nm == null) return;
        if (!NetworkServer.active && !NetworkClient.active) return; // LoginUI owns this state

        GUILayout.BeginArea(new Rect(Screen.width - PanelW - 8, 8, PanelW, 400));

        if      (NetworkServer.active && NetworkClient.active) GUILayout.Label($"Host: {nm.networkAddress}");
        else if (NetworkServer.active)                         GUILayout.Label("Server only");
        else                                                   GUILayout.Label($"Client: {nm.networkAddress}");

        // Camp back to character select — routes through the countdown (1.6.1).
        if (NetworkClient.localPlayer != null && GUILayout.Button("Camp to Character Select"))
            CampController.Instance?.RequestCamp();

        if (GUILayout.Button("Stop"))
        {
            if (NetworkServer.active && NetworkClient.active) nm.StopHost();
            else if (NetworkServer.active)                    nm.StopServer();
            else                                              nm.StopClient();
        }

        GUILayout.EndArea();
    }
#endif
}
