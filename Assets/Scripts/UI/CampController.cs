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
        _camping = StartCoroutine(CampRoutine(p, combat));
    }

    IEnumerator CampRoutine(NetworkedPlayer p, CombatState combat)
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

            int now = Mathf.CeilToInt(CampSeconds - t);
            if (now != lastShown && now > 0)
            {
                lastShown = now;
                ChatUI.AddMessage($"Camping… {now}");
            }
            yield return null;
        }

        _camping = null;
        NetworkClient.Send(new CampMessage());
    }

    void Cancel(string reason)
    {
        _camping = null;
        ChatUI.AddMessage(reason);
    }
}
