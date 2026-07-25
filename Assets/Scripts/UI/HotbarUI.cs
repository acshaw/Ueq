using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class HotbarUI : MonoBehaviour
{
    [SerializeField] HotbarSlotUI[] slots;

    PlayerAbilities _abilities;
    NetworkedPlayer _player;

    static readonly Key[] HotbarKeys =
    {
        Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
    };

    // Self-wire the slot array at runtime — immune to the edit-time serialization quirk that left some
    // slots' references unset (manifested as missing hotbar labels). The HotbarSlotUI components live
    // on the child Slot objects in hierarchy (= slot) order, so GetComponentsInChildren returns 0..N.
    void Awake()
    {
        bool needsHeal = slots == null || slots.Length == 0;
        if (!needsHeal)
            foreach (var s in slots) if (s == null) { needsHeal = true; break; }
        if (needsHeal)
            slots = GetComponentsInChildren<HotbarSlotUI>(true);
    }

    // Bind via the central LocalPlayer service (1.7).
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
        _player    = p;
        _abilities = p.GetComponent<PlayerAbilities>();
    }

    void OnLocalDespawned()
    {
        _abilities = null;
        _player    = null;
    }

    void Update()
    {
        // Self-heal fallback — see PlayerFrame.Update for why.
        if (_abilities == null)
        {
            if (LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);
            return;
        }
        RefreshSlots();
        HandleInput();
    }

    void RefreshSlots()
    {
        var hotbar    = _abilities.Hotbar;
        var cooldowns = _abilities.HotbarCooldowns;

        for (int i = 0; i < PlayerAbilities.HotbarSize && i < slots.Length; i++)
        {
            string id  = i < hotbar.Count    ? hotbar[i]    : "";
            float  cd  = i < cooldowns.Count ? cooldowns[i] : 0f;

            string label = string.IsNullOrEmpty(id)
                ? $"{i + 2}"
                : $"{i + 2}. {(AbilityRegistry.Instance?.Get(id)?.displayName ?? id)}";

            slots[i]?.Refresh(label, cd);
        }
    }

    void HandleInput()
    {
        if (ChatUI.IsOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        for (int i = 0; i < HotbarKeys.Length && i < PlayerAbilities.HotbarSize; i++)
        {
            if (!kb[HotbarKeys[i]].wasPressedThisFrame) continue;
            _player?.CmdCastAbility(i, _player.CurrentTargetIdentity);
            break;
        }
    }
}
