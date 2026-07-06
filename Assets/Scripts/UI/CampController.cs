using Mirror;
using UnityEngine;
using System.Collections;

/// <summary>
/// Client-side camp flow (1.6.1, decisions D1/D2). Both the HUD "Camp" button and the <c>/camp</c> chat
/// command route here. Runs a cancelable countdown — refused if in combat, cancelled if the player
/// moves or enters combat — and sends <see cref="CampMessage"/> on completion (the server re-checks
/// combat authoritatively before despawning). Lives on the NetworkManager GameObject.
/// </summary>
public class CampController : MonoBehaviour
{
    public static CampController Instance { get; private set; }

    const float CampSeconds    = 10f;  // D1
    const float MoveCancelDist = 0.3f; // metres of drift that count as "moved"

    Coroutine _camping;

    void Awake()     => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void RequestCamp()
    {
        var p = LocalPlayer.Current;
        if (p == null) return;
        if (_camping != null) return; // already counting down

        var combat = p.GetComponent<CombatState>();
        if (combat != null && combat.InCombat)
        {
            ChatUI.AddMessage("You can't camp while in combat.");
            return;
        }

        // 3.1.8 CP6 — sitting is required to camp (EQ-style). Moving or taking a hit stands the player
        // (3.1.7), so this interlocks with the move/combat cancels below.
        var sitting = p.GetComponent<PlayerSitting>();
        if (sitting == null || !sitting.IsSitting)
        {
            ChatUI.AddMessage("You must be sitting to camp.");
            return;
        }

        _camping = StartCoroutine(CampRoutine(p, combat, sitting));
    }

    IEnumerator CampRoutine(NetworkedPlayer p, CombatState combat, PlayerSitting sitting)
    {
        Vector3 start = p.transform.position;
        ChatUI.AddMessage($"Camping… {Mathf.CeilToInt(CampSeconds)}");

        float t = 0f;
        int lastShown = Mathf.CeilToInt(CampSeconds);
        while (t < CampSeconds)
        {
            t += Time.deltaTime;

            if (p == null) { _camping = null; yield break; } // despawned mid-camp
            if (Vector3.Distance(p.transform.position, start) > MoveCancelDist)
            {
                Cancel("Camp cancelled — you moved.");
                yield break;
            }
            if (combat != null && combat.InCombat)
            {
                Cancel("Camp cancelled — you're in combat.");
                yield break;
            }
            if (sitting == null || !sitting.IsSitting)
            {
                Cancel("Camp cancelled — you stood up.");
                yield break;
            }

            int now = Mathf.CeilToInt(CampSeconds - t);
            if (now != lastShown && now > 0)
            {
                lastShown = now;
                ChatUI.AddMessage($"Camping… {now}");
            }
            yield return null;
        }

        _camping = null;

        // 3.1.8 CP1 — route the despawn through the shell's scripted exit so the fade covers the player-pop /
        // camera gap (fade to black → send CampMessage under black → reveal on Character Select). A refused
        // camp (server combat re-check) recovers via the ExitWorld timeout. Fallback to a raw send if the shell
        // isn't present (shouldn't happen on a client that can camp).
        if (UIScreenManager.Instance != null)
            UIScreenManager.Instance.ExitWorld(() => NetworkClient.Send(new CampMessage()));
        else
            NetworkClient.Send(new CampMessage());
    }

    void Cancel(string reason)
    {
        _camping = null;
        ChatUI.AddMessage(reason);
    }
}
