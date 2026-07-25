using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VendorUI : MonoBehaviour
{
    [SerializeField] GameObject    panel;
    [SerializeField] RectTransform content;
    [SerializeField] TMP_Text      currencyLabel;
    [SerializeField] Button        buyTabBtn;
    [SerializeField] Button        sellTabBtn;

    public static VendorUI Instance { get; private set; }

    NetworkIdentity _vendor;
    string[]        _stockItemIds = System.Array.Empty<string>();
    PlayerInventory _inventory;
    NetworkedPlayer _player;
    bool            _showBuy = true;
    int             _lastCurrencyTotal = -1;

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
        _inventory = p.GetComponent<PlayerInventory>();
        if (_inventory != null) _inventory.Slots.Callback += OnInventoryChanged;
        _lastCurrencyTotal = -1;
    }

    void OnLocalDespawned()
    {
        _inventory = null;   // SyncList + callback die with the destroyed player object
        _player    = null;
        _lastCurrencyTotal = -1;
    }

    void Update()
    {
        // Self-heal fallback — see PlayerFrame.Update for why.
        if (_inventory == null && LocalPlayer.Current != null) OnLocalSpawned(LocalPlayer.Current);

        if (_inventory != null && panel.activeSelf)
        {
            int total = _inventory.TotalCopperValue;
            if (total != _lastCurrencyTotal)
            {
                _lastCurrencyTotal = total;
                RefreshCurrency();
            }
        }
    }

    void OnInventoryChanged(SyncList<InventorySlot>.Operation op, int idx,
                            InventorySlot old, InventorySlot next)
    {
        if (panel.activeSelf && !_showBuy) PopulateContent();
    }

    // Stock ids are pushed by the server on open (M2.3 — clients have no DB access); item display
    // details still resolve through the client's ItemRegistry (2.2 catalog).
    public void Open(NetworkIdentity vendor, string[] stockItemIds)
    {
        _vendor       = vendor;
        _stockItemIds = stockItemIds ?? System.Array.Empty<string>();
        _showBuy      = true;
        SetTabStyle();
        PopulateContent();
        RefreshCurrency();
        panel.SetActive(true);
    }

    public void Close() => panel.SetActive(false);

    public void OnBuyTabClicked()  { _showBuy = true;  SetTabStyle(); PopulateContent(); }
    public void OnSellTabClicked() { _showBuy = false; SetTabStyle(); PopulateContent(); }

    void SetTabStyle()
    {
        var active   = new Color(0.18f, 0.38f, 0.18f);
        var inactive = new Color(0.2f, 0.2f, 0.2f);
        if (buyTabBtn  != null) buyTabBtn .GetComponent<Image>().color = _showBuy ? active : inactive;
        if (sellTabBtn != null) sellTabBtn.GetComponent<Image>().color = _showBuy ? inactive : active;
    }

    void PopulateContent()
    {
        foreach (Transform t in content) Destroy(t.gameObject);
        if (_showBuy) PopulateBuy();
        else          PopulateSell();
    }

    void PopulateBuy()
    {
        if (_stockItemIds.Length == 0)
        { AddInfoRow("This vendor has no wares."); return; }
        foreach (var itemId in _stockItemIds)
        {
            var def = ItemRegistry.Instance?.Get(itemId);
            if (def != null) AddBuyRow(def);
        }
        if (content.childCount == 0) AddInfoRow("This vendor has no wares.");
    }

    void PopulateSell()
    {
        if (_inventory == null) return;
        bool any = false;
        for (int i = 0; i < _inventory.Slots.Count; i++)
        {
            var slot = _inventory.Slots[i];
            if (slot.IsEmpty) continue;
            var def = ItemRegistry.Instance?.Get(slot.itemId);
            if (def == null || def.sellPrice <= 0) continue;
            AddSellRow(i, def);
            any = true;
        }
        if (!any) AddInfoRow("You have nothing to sell.");
    }

    void AddBuyRow(ItemDefinition def)
    {
        var row = MakeRow();
        AddLabel(row, def.displayName, flex: true);
        AddLabel(row, CurrencyUtil.Format(def.buyPrice), width: 75f,
                 color: new Color(1f, 0.85f, 0.4f));
        var id = def.itemId;
        MakeButton(row, "Buy", new Color(0.18f, 0.32f, 0.18f), () =>
        {
            _player?.CmdBuyItem(_vendor, id);
            if (!_showBuy) PopulateContent();
        });
    }

    void AddSellRow(int slotIdx, ItemDefinition def)
    {
        var row = MakeRow();
        AddLabel(row, def.displayName, flex: true);
        AddLabel(row, CurrencyUtil.Format(def.sellPrice), width: 75f,
                 color: new Color(1f, 0.85f, 0.4f));
        int idx = slotIdx;
        MakeButton(row, "Sell", new Color(0.32f, 0.18f, 0.10f),
            () => _player?.CmdSellItem(_vendor, idx));
    }

    void AddInfoRow(string message)
    {
        var obj  = new GameObject("Info");
        obj.transform.SetParent(content, false);
        obj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 26f);
        var tmp  = obj.AddComponent<TextMeshProUGUI>();
        tmp.text          = message;
        tmp.fontSize      = 12;
        tmp.color         = new Color(0.6f, 0.6f, 0.6f);
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    void RefreshCurrency()
    {
        if (_inventory == null || currencyLabel == null) return;
        currencyLabel.text =
            $"PP:{_inventory.Platinum}  GP:{_inventory.Gold}  SP:{_inventory.Silver}  CP:{_inventory.Copper}";
        _lastCurrencyTotal = _inventory.TotalCopperValue;
    }

    // ── Row builders ──────────────────────────────────────────────────────────

    RectTransform MakeRow()
    {
        const float RowH = 26f;
        var obj  = new GameObject("Row");
        obj.transform.SetParent(content, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, RowH);
        var hlg  = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.spacing                = 4;
        hlg.childControlWidth      = false;
        hlg.childForceExpandWidth  = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandHeight = false;
        var le   = obj.AddComponent<LayoutElement>();
        le.preferredHeight = RowH;
        le.minHeight       = RowH;
        return rect;
    }

    static TMP_Text AddLabel(RectTransform row, string text,
                              bool flex = false, float width = 80f, Color? color = null)
    {
        const float RowH = 26f;
        var obj  = new GameObject("Lbl");
        obj.transform.SetParent(row, false);
        obj.AddComponent<RectTransform>().sizeDelta = new Vector2(flex ? 80f : width, RowH);
        var le   = obj.AddComponent<LayoutElement>();
        if (flex) le.flexibleWidth = 1;
        else      le.preferredWidth = width;
        le.preferredHeight = RowH;
        var tmp  = obj.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = 12;
        tmp.color         = color ?? Color.white;
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode  = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void MakeButton(RectTransform row, string label, Color bg, System.Action onClick)
    {
        const float RowH = 26f;
        const float BtnW = 50f;
        var obj  = new GameObject("Btn");
        obj.transform.SetParent(row, false);
        obj.AddComponent<RectTransform>().sizeDelta = new Vector2(BtnW, RowH);
        var le   = obj.AddComponent<LayoutElement>();
        le.preferredWidth  = BtnW;
        le.preferredHeight = RowH;
        var img  = obj.AddComponent<Image>();
        img.color = bg;
        var btn  = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        var txtObj  = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        var txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 11;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }
}
