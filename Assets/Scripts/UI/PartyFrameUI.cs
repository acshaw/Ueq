using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 5.3 — group member health frames (GP7: same-zone live, greyed placeholder elsewhere — Mirror's
/// SceneInterestManagement means an out-of-zone member's Health/Nameplate simply isn't replicated to this
/// client) plus F1-F6 quick group-targeting (GP11). F1 always targets self, and self always occupies slot 1
/// FROM THIS CLIENT'S OWN POINT OF VIEW — the roster is reordered locally (self first) so "group member 1"
/// means "me" identically for every client, not a shared/global slot number.
///
/// F-key targeting sets the server-side combat target (NetworkedPlayer.ServerTarget) directly — it
/// deliberately does NOT route through the click-to-target Targetable/TargetFrame system: players aren't
/// Targetable objects in this codebase, and that system's highlight tint already means "hostile," so reusing
/// it for a friendly group member would be actively confusing. Live HP visibility for group members comes
/// from this panel's own frames instead (GP7), not the TargetFrame.
/// </summary>
public class PartyFrameUI : MonoBehaviour
{
    [SerializeField] PartyFrameSlotUI[] slots; // other members only — self stays in the existing PlayerFrame

    static readonly Key[] GroupTargetKeys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6 };

    NetworkedPlayer _player;
    PlayerParty     _party;
    int             _selectedIndex = -1; // index into _localOrder — purely cosmetic slot highlight

    readonly List<NetworkIdentity> _localOrder = new(); // [0] = self, [1..] = other members, netId order

    // Self-wire at runtime — immune to the edit-time serialization quirk (see HotbarUI's identical pattern).
    void Awake()
    {
        bool needsHeal = slots == null || slots.Length == 0;
        if (!needsHeal)
            foreach (var s in slots) if (s == null) { needsHeal = true; break; }
        if (needsHeal)
            slots = GetComponentsInChildren<PartyFrameSlotUI>(true);
    }

    void OnEnable()
    {
        LocalPlayer.Spawned   += OnLocalSpawned;
        LocalPlayer.Despawned += OnLocalDespawned;
        if (LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);
    }

    void OnDisable()
    {
        LocalPlayer.Spawned   -= OnLocalSpawned;
        LocalPlayer.Despawned -= OnLocalDespawned;
    }

    void OnLocalSpawned(NetworkedPlayer p)
    {
        _player = p;
        _party  = p.GetComponent<PlayerParty>();
    }

    void OnLocalDespawned()
    {
        _player = null;
        _party  = null;
        _selectedIndex = -1;
        if (slots != null) foreach (var s in slots) s?.Hide();
    }

    void Update()
    {
        // Self-heal fallback — see PlayerFrame.Update for why.
        if (_party == null)
        {
            if (LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);
            return;
        }

        RebuildLocalOrder();
        RefreshSlots();
        HandleInput();
    }

    void RebuildLocalOrder()
    {
        _localOrder.Clear();
        _localOrder.Add(_player.netIdentity); // slot 0 = self, always, for every client

        var others = new List<NetworkIdentity>();
        foreach (var m in _party.Members)
            if (m != null && m != _player.netIdentity) others.Add(m);
        others.Sort((a, b) => a.netId.CompareTo(b.netId)); // stable order, identical on every client
        _localOrder.AddRange(others);
    }

    void RefreshSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int memberIndex = i + 1; // slots[0] shows _localOrder[1] — index 0/self is shown by PlayerFrame
            if (memberIndex >= _localOrder.Count || _localOrder[memberIndex] == null)
            {
                slots[i]?.Hide();
                continue;
            }

            var member = _localOrder[memberIndex];
            slots[i]?.Show(member);

            string label = member.name;
            if (NetworkClient.spawned.ContainsKey(member.netId))
            {
                var np = member.GetComponent<Nameplate>();
                if (!string.IsNullOrEmpty(np?.Label)) label = np.Label;
                var health = member.GetComponent<Health>();
                if (health != null) slots[i]?.RefreshLive(label, health.Current, health.Max);
                else                slots[i]?.RefreshPlaceholder(label);
            }
            else
            {
                slots[i]?.RefreshPlaceholder(label); // GP7-A — different zone, not observed here
            }

            slots[i]?.SetSelected(memberIndex == _selectedIndex);
        }
    }

    void HandleInput()
    {
        if (ChatUI.IsOpen) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        for (int i = 0; i < GroupTargetKeys.Length; i++)
        {
            if (!kb[GroupTargetKeys[i]].wasPressedThisFrame) continue;
            if (i >= _localOrder.Count || _localOrder[i] == null) return;
            _selectedIndex = i;
            _player.CmdSetTarget(_localOrder[i]);
            return;
        }
    }
}
