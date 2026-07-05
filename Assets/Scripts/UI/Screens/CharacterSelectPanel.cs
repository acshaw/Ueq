using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Character select / create (M3.1.1 shell; redesigned in 3.1.6). Reuses the exact same Mirror messages
/// (<c>CharacterListRequest</c>/<c>CharacterListMessage</c>, <c>EnterWorldMessage</c>,
/// <c>CreateCharacterMessage</c>, <c>DeleteCharacterMessage</c>, <c>CharacterActionResult</c>) — no server
/// change.
///
/// 3.1.6: the roster and the two-column create form (live 3D <see cref="CharacterPreview"/> + ◀/▶ selectors)
/// are persistent containers toggled by visibility, and the create form is built once — switching
/// roster ↔ create and cycling gender/race/class update in place, so nothing tears down and flickers. Only a
/// roster data change (list arrival / delete-confirm) repopulates the roster rows.
/// </summary>
public class CharacterSelectPanel : ScreenPanel
{
    CharacterPreview _preview;

    RectTransform   _rosterRoot, _createRoot;
    TextMeshProUGUI _loadingText, _errorText;
    Button          _backButton;

    bool _haveList;
    bool _creating;
    long _confirmDeleteId;
    string _error = "";

    CharacterListMessage _list;
    int _genderIdx, _raceIdx, _classIdx;

    // Persistent create-form widgets (built once; updated in place by RefreshCreate).
    TMP_InputField   _nameField;
    TextMeshProUGUI  _genderLabel, _raceLabel, _classLabel;
    string[] _genders = new string[0], _races = new string[0], _classes = new string[0];

    protected override void Build()
    {
        MenuUI.FullScreenImage(Root, "Dim", new Color(0.05f, 0.06f, 0.09f, 1f));
        var card = MenuUI.Card(Root, 720, 700);
        MenuUI.Text(card, "Character Select", 32, TextAlignmentOptions.Center);

        _loadingText = MenuUI.Text(card, "Loading...", 20, TextAlignmentOptions.Center);
        _rosterRoot  = Column(card);
        _createRoot  = Column(card);
        _errorText   = MenuUI.Text(card, "", 18, TextAlignmentOptions.Center);
        _errorText.color = MenuUI.ErrorColor;
        MenuUI.Button(card, "Log Out", () => Manager.Disconnect());

        _preview = CharacterPreview.Create(360, 528); // 3D staging built once; RawImage lives in the create form
        BuildCreateForm();

        ShowMode();
    }

    public override void OnShow()
    {
        RegisterHandlers();
        _creating = false;
        _confirmDeleteId = 0;
        _error = "";
        _haveList = false;
        _genderIdx = _raceIdx = _classIdx = 0;
        if (_nameField != null) _nameField.text = "";
        if (NetworkClient.active) NetworkClient.Send(new CharacterListRequest());
        ShowMode();
    }

    public override void OnHide()
    {
        if (_preview != null) _preview.SetActive(false);
    }

    void RegisterHandlers()
    {
        // Re-register every time the screen shows. Mirror wipes NetworkClient.handlers on every disconnect
        // (NetworkClient.Shutdown → handlers.Clear), so a guard that persisted across reconnects left these
        // unregistered after a camp/relogin. ReplaceHandler is idempotent within a live session.
        NetworkClient.ReplaceHandler<CharacterListMessage>(OnList);
        NetworkClient.ReplaceHandler<CharacterActionResult>(OnResult);
    }

    void OnList(CharacterListMessage msg)
    {
        _list = msg;
        _haveList = true;
        if (msg.entries == null || msg.entries.Length == 0) _creating = true;
        RebuildRoster();
        ShowMode();
    }

    void OnResult(CharacterActionResult msg)
    {
        if (!msg.ok) { _error = msg.error; ShowMode(); }
    }

    void Update()
    {
        if (!MenuUI.BackPressed()) return;
        // Esc cancels the create form back to the roster when there's one to return to; otherwise it logs out.
        if (_creating && _list.entries != null && _list.entries.Length > 0)
        {
            _error = ""; _creating = false; ShowMode();
        }
        else
        {
            Manager.Disconnect();
        }
    }

    // ── Mode visibility (no teardown → no flicker on roster ↔ create) ─────────────

    void ShowMode()
    {
        _loadingText.gameObject.SetActive(!_haveList);
        _rosterRoot.gameObject.SetActive(_haveList && !_creating);
        _createRoot.gameObject.SetActive(_haveList && _creating);
        if (_preview != null) _preview.SetActive(_haveList && _creating);

        _errorText.gameObject.SetActive(!string.IsNullOrEmpty(_error));
        _errorText.text = _error ?? "";

        if (_haveList && _creating) RefreshCreate();
    }

    // ── Roster (rebuilt only on a data/confirm change, while roster is shown) ─────

    void RebuildRoster()
    {
        ClearChildren(_rosterRoot);

        int count = _list.entries != null ? _list.entries.Length : 0;
        MenuUI.Text(_rosterRoot, $"Characters ({count}/{_list.maxSlots})", 18, TextAlignmentOptions.Center);

        for (int i = 0; i < count; i++)
        {
            var e = _list.entries[i];
            MenuUI.Text(_rosterRoot, $"{e.name} — Lvl {e.level} {e.gender} {e.race} {e.cls}", 18, TextAlignmentOptions.Center);

            if (_confirmDeleteId == e.id)
            {
                MenuUI.Text(_rosterRoot, $"Delete {e.name}? This cannot be undone.", 16, TextAlignmentOptions.Center);
                MenuUI.Button(_rosterRoot, "Confirm Delete", () =>
                {
                    _confirmDeleteId = 0; _error = ""; _haveList = false;
                    NetworkClient.Send(new DeleteCharacterMessage { characterId = e.id });
                    ShowMode();
                });
                MenuUI.Button(_rosterRoot, "Cancel", () => { _confirmDeleteId = 0; RebuildRoster(); });
            }
            else
            {
                MenuUI.Button(_rosterRoot, $"Enter World as {e.name}", () =>
                {
                    _error = "";
                    NetworkClient.Send(new EnterWorldMessage { characterId = e.id });
                });
                MenuUI.Button(_rosterRoot, $"Delete {e.name}", () => { _confirmDeleteId = e.id; RebuildRoster(); });
            }
            MenuUI.Spacer(_rosterRoot, 4);
        }

        if (count < _list.maxSlots)
            MenuUI.Button(_rosterRoot, "Create New Character", () => { _error = ""; _creating = true; ShowMode(); });
        else
            MenuUI.Text(_rosterRoot, "All character slots are full.", 16, TextAlignmentOptions.Center);
    }

    // ── Create form (built once; two columns, in-place selector updates) ─────────

    void BuildCreateForm()
    {
        MenuUI.Text(_createRoot, "Create a character", 22, TextAlignmentOptions.Center);

        var row = HRow(_createRoot, 448);
        MenuUI.RawImage(row, _preview != null ? _preview.Texture : null, 300, 440);
        var form = VColumn(row);

        _nameField = MenuUI.Input(form, "Name", false);

        _genderLabel = MenuUI.ArrowSelector(form,
            () => { _genderIdx = Prev(_genderIdx, _genders.Length); _raceIdx = 0; _classIdx = 0; RefreshCreate(); },
            () => { _genderIdx = Next(_genderIdx, _genders.Length); _raceIdx = 0; _classIdx = 0; RefreshCreate(); });
        _raceLabel = MenuUI.ArrowSelector(form,
            () => { _raceIdx = Prev(_raceIdx, _races.Length); _classIdx = 0; RefreshCreate(); },
            () => { _raceIdx = Next(_raceIdx, _races.Length); _classIdx = 0; RefreshCreate(); });
        _classLabel = MenuUI.ArrowSelector(form,
            () => { _classIdx = Prev(_classIdx, _classes.Length); RefreshCreate(); },
            () => { _classIdx = Next(_classIdx, _classes.Length); RefreshCreate(); });

        MenuUI.Spacer(form, 6);
        MenuUI.Button(form, "Create & Enter", OnCreateSubmit);
        _backButton = MenuUI.Button(form, "Back", () => { _error = ""; _creating = false; ShowMode(); });
    }

    // Recompute the gated cascade + push it to the labels and the 3D preview — no teardown.
    void RefreshCreate()
    {
        var opts = _list.createOptions ?? new CreateOption[0];

        _genders = Genders(opts);
        _genderIdx = Clamp(_genderIdx, _genders.Length);
        string gender = Pick(_genders, _genderIdx);

        _races = RacesFor(opts, gender);
        _raceIdx = Clamp(_raceIdx, _races.Length);
        string race = Pick(_races, _raceIdx);

        _classes = ClassesFor(opts, gender, race);
        _classIdx = Clamp(_classIdx, _classes.Length);
        string cls = Pick(_classes, _classIdx);

        if (_genderLabel != null) _genderLabel.text = $"Gender:  {gender}";
        if (_raceLabel   != null) _raceLabel.text   = $"Race:  {race}";
        if (_classLabel  != null) _classLabel.text  = $"Class:  {cls}";

        // Back is hidden when there's no roster to return to (creating the first character).
        if (_backButton != null)
            _backButton.gameObject.SetActive(_list.entries != null && _list.entries.Length > 0);

        if (_preview != null && System.Enum.TryParse<Gender>(gender, out var g))
            _preview.Show(g, race, cls);
    }

    void OnCreateSubmit()
    {
        _error = "";
        NetworkClient.Send(new CreateCharacterMessage
        {
            name   = _nameField != null ? _nameField.text.Trim() : "",
            gender = Pick(_genders, _genderIdx),
            race   = Pick(_races, _raceIdx),
            cls    = Pick(_classes, _classIdx),
        });
    }

    // ── Layout scaffolding ───────────────────────────────────────────────────────

    RectTransform Column(Transform parent)
    {
        var go = new GameObject("Column", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        return (RectTransform)go.transform;
    }

    RectTransform HRow(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childControlWidth = true;  hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.UpperCenter;
        MenuUI.SetPreferredHeight(go, height);
        return (RectTransform)go.transform;
    }

    RectTransform VColumn(Transform parent)
    {
        var go = new GameObject("Column", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1; le.minWidth = 320;
        return (RectTransform)go.transform;
    }

    // ── Roster filters (client-side; the server re-validates the tuple on create) ─────────────────

    static string[] Genders(CreateOption[] opts)
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var o in opts) if (!string.IsNullOrEmpty(o.gender) && !list.Contains(o.gender)) list.Add(o.gender);
        return list.ToArray();
    }

    static string[] RacesFor(CreateOption[] opts, string gender)
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var o in opts)
            if (o.gender == gender && !string.IsNullOrEmpty(o.race) && !list.Contains(o.race)) list.Add(o.race);
        return list.ToArray();
    }

    static string[] ClassesFor(CreateOption[] opts, string gender, string race)
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var o in opts)
            if (o.gender == gender && o.race == race && !string.IsNullOrEmpty(o.cls) && !list.Contains(o.cls)) list.Add(o.cls);
        return list.ToArray();
    }

    static int Clamp(int idx, int len) => len <= 0 ? 0 : Mathf.Clamp(idx, 0, len - 1);

    void ClearChildren(RectTransform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var ch = container.GetChild(i);
            ch.SetParent(null, false);
            Destroy(ch.gameObject);
        }
    }

    static string Pick(string[] opts, int idx)
        => opts != null && idx >= 0 && idx < opts.Length ? opts[idx] : "(none)";

    static int Next(int idx, int len) => len <= 0 ? 0 : (idx + 1) % len;
    static int Prev(int idx, int len) => len <= 0 ? 0 : (idx - 1 + len) % len;
}
