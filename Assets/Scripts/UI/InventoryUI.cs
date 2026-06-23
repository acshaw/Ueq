using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] GameObject        panel;
    [SerializeField] RectTransform     panelRect;
    [SerializeField] InventorySlotUI[] slotTiles;      // 8 elements
    [SerializeField] TMP_Text          currencyLabel;
    [SerializeField] RectTransform     cursorItemRect;  // follows mouse while holding
    [SerializeField] TMP_Text          cursorItemText;

    public static InventoryUI Instance { get; private set; }

    PlayerInventory _inventory;
    NetworkedPlayer _player;
    int             _heldSlotIndex = -1;
    int             _lastCurrencyTotal = -1;
    RectTransform   _canvasRect;

    void Awake()
    {
        Instance    = this;
        _canvasRect = GetComponent<RectTransform>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Bind via the central LocalPlayer service (1.7) — no more FindObjectsByType / _bound latch.
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
        _inventory = p.GetComponent<PlayerInventory>();
        if (_inventory != null) _inventory.Slots.Callback += OnSlotsChanged;
        _lastCurrencyTotal = -1;
        Refresh();
    }

    void OnLocalDespawned()
    {
        _inventory = null;   // SyncList + callback die with the destroyed player object
        _player    = null;
        _lastCurrencyTotal = -1;
        CancelHold();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame && !ChatUI.IsOpen)
            SetVisible(!panel.activeSelf);

        // Catch SyncVar-only currency changes (no slot change fires Refresh)
        if (_inventory != null && panel.activeSelf)
        {
            int total = _inventory.Copper + _inventory.Silver * 10
                      + _inventory.Gold * 100 + _inventory.Platinum * 1000;
            if (total != _lastCurrencyTotal)
            {
                _lastCurrencyTotal = total;
                RefreshCurrency();
            }
        }

        if (_heldSlotIndex >= 0)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, mousePos, null, out var localPt))
                cursorItemRect.localPosition = localPt;

            // LMB on the game world (not over any UI) → drop item
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                var es = EventSystem.current;
                if (es == null || !es.IsPointerOverGameObject())
                    DropHeldItem();
            }
        }
    }

    void SetVisible(bool visible)
    {
        panel.SetActive(visible);
        if (!visible) CancelHold();
    }

    void OnSlotsChanged(SyncList<InventorySlot>.Operation op, int idx,
                        InventorySlot oldItem, InventorySlot newItem) => Refresh();

    void Refresh()
    {
        if (_inventory == null) return;

        for (int i = 0; i < slotTiles.Length; i++)
        {
            if (slotTiles[i] == null) continue;
            var slot = i < _inventory.Slots.Count ? _inventory.Slots[i] : InventorySlot.Empty;
            slotTiles[i].Refresh(slot, i == _heldSlotIndex);
        }

        RefreshCurrency();
    }

    void RefreshCurrency()
    {
        if (_inventory == null || currencyLabel == null) return;
        currencyLabel.text =
            $"PP: {_inventory.Platinum}  GP: {_inventory.Gold}  SP: {_inventory.Silver}  CP: {_inventory.Copper}";
        _lastCurrencyTotal = _inventory.Copper + _inventory.Silver * 10
                           + _inventory.Gold * 100 + _inventory.Platinum * 1000;
    }

    public void OnSlotRightClicked(int slotIndex)
    {
        if (_inventory == null || _heldSlotIndex >= 0) return;
        var slot = _inventory.Slots[slotIndex];
        if (slot.IsEmpty) return;
        var def = ItemRegistry.Instance?.Get(slot.itemId);
        if (def == null || !def.isEquippable) return;
        _player?.CmdEquipItem(slotIndex);
    }

    public void OnSlotClicked(int slotIndex)
    {
        if (_inventory == null) return;

        if (_heldSlotIndex < 0)
        {
            var slot = _inventory.Slots[slotIndex];
            if (slot.IsEmpty) return;

            _heldSlotIndex = slotIndex;
            var def = ItemRegistry.Instance?.Get(slot.itemId);
            cursorItemText.text = def != null ? def.displayName : slot.itemId;
            if (slot.quantity > 1) cursorItemText.text += $" x{slot.quantity}";
            cursorItemRect.gameObject.SetActive(true);

            // Snap cursor item to current mouse position immediately
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, mousePos, null, out var localPt))
                cursorItemRect.localPosition = localPt;

            Refresh();
        }
        else
        {
            if (slotIndex == _heldSlotIndex) { CancelHold(); return; }
            _player?.CmdMoveInventorySlot(_heldSlotIndex, slotIndex);
            CancelHold();
        }
    }

    void DropHeldItem()
    {
        _player?.CmdDropInventoryItem(_heldSlotIndex);
        CancelHold();
    }

    void CancelHold()
    {
        _heldSlotIndex = -1;
        cursorItemRect.gameObject.SetActive(false);
        Refresh();
    }
}
