using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LootUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text   titleLabel;
    [SerializeField] Transform  slotsContainer;
    [SerializeField] TMP_Text   coinLabel;
    [SerializeField] Button     takeCoinButton;
    [SerializeField] Button     lootAllButton;

    public static LootUI Instance { get; private set; }
    public static bool    IsOpen   => Instance != null && Instance.panel != null && Instance.panel.activeSelf;

    Corpse          _mobCorpse;
    PlayerCorpse    _playerCorpse;
    NetworkIdentity _corpseIdentity;
    NetworkedPlayer _localPlayer;
    int             _lastCopper = -1;

    void Awake()     => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Active corpse accessors ───────────────────────────────────────────────

    SyncList<InventorySlot> ActiveSlots =>
        _mobCorpse != null ? _mobCorpse.Slots : _playerCorpse?.Slots;

    int ActiveCopper =>
        _mobCorpse != null ? _mobCorpse.Copper : (_playerCorpse?.Copper ?? 0);

    bool HasActive => _mobCorpse != null || _playerCorpse != null;

    Nameplate ActiveNameplate =>
        _mobCorpse != null
            ? _mobCorpse.GetComponent<Nameplate>()
            : _playerCorpse?.GetComponent<Nameplate>();

    void Update()
    {
        if (!panel.activeSelf) return;

        if (!HasActive) { CloseInternal(); return; }

        if (ActiveCopper != _lastCopper)
        {
            _lastCopper = ActiveCopper;
            RefreshCoin();
        }

        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            CloseInternal();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Open(Corpse corpse)       => Instance?.OpenInternal(corpse, null);
    public static void Open(PlayerCorpse corpse) => Instance?.OpenInternal(null, corpse);
    public static void Close()                   => Instance?.CloseInternal();

    // ── Internals ─────────────────────────────────────────────────────────────

    void OpenInternal(Corpse mobCorpse, PlayerCorpse playerCorpse)
    {
        var prevSlots = ActiveSlots;
        if (prevSlots != null) prevSlots.Callback -= OnSlotsChanged;

        _mobCorpse      = mobCorpse;
        _playerCorpse   = playerCorpse;
        _corpseIdentity = mobCorpse  != null ? mobCorpse.GetComponent<NetworkIdentity>()
                        : playerCorpse?.GetComponent<NetworkIdentity>();
        _lastCopper     = -1;

        _localPlayer = LocalPlayer.Current; // 1.7 — single binding seam (loot opens while in-world)

        var slots = ActiveSlots;
        if (slots != null) slots.Callback += OnSlotsChanged;

        panel.SetActive(true);
        Refresh();
    }

    void CloseInternal()
    {
        var slots = ActiveSlots;
        if (slots != null) slots.Callback -= OnSlotsChanged;
        _mobCorpse      = null;
        _playerCorpse   = null;
        _corpseIdentity = null;
        panel.SetActive(false);
    }

    void OnSlotsChanged(SyncList<InventorySlot>.Operation op, int index, InventorySlot old, InventorySlot next)
        => Refresh();

    void Refresh()
    {
        if (!HasActive) return;

        var nameplate = ActiveNameplate;
        titleLabel.text = nameplate != null ? nameplate.Label : "Loot";

        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
            Destroy(slotsContainer.GetChild(i).gameObject);

        var slots = ActiveSlots;
        if (slots != null)
            for (int i = 0; i < slots.Count; i++)
                CreateSlotRow(i);

        RefreshCoin();

        lootAllButton.interactable = true;
        lootAllButton.onClick.RemoveAllListeners();
        var capturedId = _corpseIdentity;
        lootAllButton.onClick.AddListener(() => _localPlayer?.CmdTakeLootAll(capturedId));
    }

    void RefreshCoin()
    {
        if (!HasActive) return;
        int copper                  = ActiveCopper;
        coinLabel.text              = copper > 0 ? $"{copper} copper" : "No coin";
        takeCoinButton.interactable = copper > 0;
        var capturedId = _corpseIdentity;
        takeCoinButton.onClick.RemoveAllListeners();
        takeCoinButton.onClick.AddListener(() => _localPlayer?.CmdTakeLootCopper(capturedId));
    }

    void CreateSlotRow(int index)
    {
        var slots = ActiveSlots;
        if (slots == null || index >= slots.Count) return;

        var slot = slots[index];
        var def  = ItemRegistry.Instance?.Get(slot.itemId);
        string label = def != null ? def.displayName : slot.itemId;

        var row      = new GameObject($"Slot{index}");
        row.transform.SetParent(slotsContainer, false);
        row.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 26);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment         = TextAnchor.MiddleLeft;
        rowLayout.spacing                = 4;
        rowLayout.childControlHeight     = false;
        rowLayout.childForceExpandHeight = false;
        row.AddComponent<LayoutElement>().preferredHeight = 26;

        var lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        lblObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 22);
        var lblLE = lblObj.AddComponent<LayoutElement>();
        lblLE.flexibleWidth   = 1;
        lblLE.preferredHeight = 22;
        var lblTMP = lblObj.AddComponent<TextMeshProUGUI>();
        lblTMP.text              = label;
        lblTMP.fontSize          = 13;
        lblTMP.color             = Color.white;
        lblTMP.verticalAlignment = VerticalAlignmentOptions.Middle;

        int             captured   = index;
        NetworkIdentity capturedId = _corpseIdentity;

        var btnObj  = new GameObject("Take");
        btnObj.transform.SetParent(row.transform, false);
        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(50, 22);
        var btnLE = btnObj.AddComponent<LayoutElement>();
        btnLE.preferredWidth  = 50;
        btnLE.preferredHeight = 22;
        var btnImg  = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.45f, 0.2f);
        var btnComp = btnObj.AddComponent<Button>();
        btnComp.targetGraphic = btnImg;

        var txtObj  = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        var txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
        var txtTMP = txtObj.AddComponent<TextMeshProUGUI>();
        txtTMP.text      = "Take";
        txtTMP.fontSize  = 12;
        txtTMP.color     = Color.white;
        txtTMP.alignment = TextAlignmentOptions.Center;

        btnComp.onClick.AddListener(() => _localPlayer?.CmdTakeLootSlot(capturedId, captured));
    }
}
