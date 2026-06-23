using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class EquipmentUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] EquipSlotRowUI[] rows;

    public static EquipmentUI Instance { get; private set; }

    PlayerEquipment _equipment;
    NetworkedPlayer _player;

    void Awake()     => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

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
        _equipment = p.GetComponent<PlayerEquipment>();
        if (_equipment == null) return;
        for (int i = 0; i < rows.Length; i++)
            rows[i].Init(i, _player);
        _equipment.Slots.Callback += OnSlotsChanged;
        Refresh();
    }

    void OnLocalDespawned()
    {
        _equipment = null;   // SyncList + callback die with the destroyed player object
        _player    = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame && !ChatUI.IsOpen)
            panel.SetActive(!panel.activeSelf);
    }

    void OnSlotsChanged(SyncList<string>.Operation op, int idx, string old, string next) => Refresh();

    void Refresh()
    {
        if (_equipment == null) return;
        for (int i = 0; i < rows.Length; i++)
            rows[i].Refresh(_equipment.GetItemId((EquipSlot)i));
    }
}
