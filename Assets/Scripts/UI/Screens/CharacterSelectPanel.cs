using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// M3.1.1 — uGUI character select / create (replaces the IMGUI <c>CharacterSelectUI</c>). Reuses the exact
/// same Mirror messages (<c>CharacterListRequest</c>/<c>CharacterListMessage</c>, <c>EnterWorldMessage</c>,
/// <c>CreateCharacterMessage</c>, <c>DeleteCharacterMessage</c>, <c>CharacterActionResult</c>) — no server
/// change. Minimal/unstyled (SF6); the styled redesign + live 3D preview is 3.1.6. Race/class pickers are
/// simple cycle buttons here (functional, not final UI).
/// </summary>
public class CharacterSelectPanel : ScreenPanel
{
    RectTransform _content;

    bool _haveList;
    bool _creating;
    long _confirmDeleteId;
    string _error = "";

    CharacterListMessage _list;
    int _genderIdx, _raceIdx, _classIdx;
    string _pendingName = "";   // survives the per-click form rebuild so the name field isn't wiped

    protected override void Build()
    {
        MenuUI.FullScreenImage(Root, "Dim", new Color(0.05f, 0.06f, 0.09f, 1f));
        var card = MenuUI.Card(Root, 520, 700);
        MenuUI.Text(card, "Character Select", 32, TextAlignmentOptions.Center);

        // Roster/create rows are appended directly to the card (matching the Title/Login panels) so the
        // card's own VerticalLayoutGroup stacks them — a nested container + ContentSizeFitter overlapped
        // the title due to a layout-ordering race. The persistent title stays as child 0; ClearContent
        // rebuilds everything below it.
        _content = card;
    }

    public override void OnShow()
    {
        RegisterHandlers();
        _creating = false;
        _confirmDeleteId = 0;
        _error = "";
        _haveList = false;
        _genderIdx = _raceIdx = _classIdx = 0;
        _pendingName = "";
        if (NetworkClient.active) NetworkClient.Send(new CharacterListRequest());
        Rebuild();
    }

    void RegisterHandlers()
    {
        // Re-register every time the screen shows. Mirror wipes NetworkClient.handlers on every disconnect
        // (NetworkClient.Shutdown → handlers.Clear), so a guard that persisted across reconnects left these
        // unregistered after a camp/relogin — the server's character-list reply then had no handler and
        // Mirror disconnected the client ("failed to unpack and invoke message"). ReplaceHandler is
        // idempotent within a live session (no duplicate-handler warning).
        NetworkClient.ReplaceHandler<CharacterListMessage>(OnList);
        NetworkClient.ReplaceHandler<CharacterActionResult>(OnResult);
    }

    void OnList(CharacterListMessage msg)
    {
        _list = msg;
        _haveList = true;
        if (msg.entries == null || msg.entries.Length == 0) _creating = true;
        Rebuild();
    }

    void OnResult(CharacterActionResult msg)
    {
        if (!msg.ok) { _error = msg.error; Rebuild(); }
    }

    void Update()
    {
        if (!MenuUI.BackPressed()) return;
        // Esc cancels the create form back to the roster when there's one to return to; otherwise it logs out.
        if (_creating && _list.entries != null && _list.entries.Length > 0)
        {
            _error = ""; _creating = false; Rebuild();
        }
        else
        {
            Manager.Disconnect();
        }
    }

    // ── Build the dynamic content ────────────────────────────────────────────────

    void Rebuild()
    {
        if (_content == null) return;

        // The form is torn down + rebuilt on every interaction (the flicker + full redesign is 3.1.6).
        // Preserve the typed name across that teardown so cycling gender/race/class doesn't wipe it, and
        // null the stale field ref so a later rebuild can't read a destroyed input. BuildCreate restores it.
        if (_nameField != null) _pendingName = _nameField.text;
        _nameField = null;

        ClearContent();

        if (!_haveList)
        {
            MenuUI.Text(_content, "Loading…", 20, TextAlignmentOptions.Center);
            MenuUI.Spacer(_content, 10);
            MenuUI.Button(_content, "Log Out", () => Manager.Disconnect());
            return;
        }

        if (_creating) BuildCreate();
        else           BuildRoster();

        if (!string.IsNullOrEmpty(_error))
        {
            var e = MenuUI.Text(_content, _error, 18, TextAlignmentOptions.Center);
            e.color = MenuUI.ErrorColor;
        }

        MenuUI.Spacer(_content, 10);
        MenuUI.Button(_content, "Log Out", () => Manager.Disconnect());
    }

    void BuildRoster()
    {
        int count = _list.entries != null ? _list.entries.Length : 0;
        MenuUI.Text(_content, $"Characters ({count}/{_list.maxSlots})", 18, TextAlignmentOptions.Center);

        for (int i = 0; i < count; i++)
        {
            var e = _list.entries[i];
            MenuUI.Text(_content, $"{e.name} — Lvl {e.level} {e.gender} {e.race} {e.cls}", 18, TextAlignmentOptions.Center);

            if (_confirmDeleteId == e.id)
            {
                MenuUI.Text(_content, $"Delete {e.name}? This cannot be undone.", 16, TextAlignmentOptions.Center);
                MenuUI.Button(_content, "Confirm Delete", () =>
                {
                    _confirmDeleteId = 0; _error = ""; _haveList = false;
                    NetworkClient.Send(new DeleteCharacterMessage { characterId = e.id });
                    Rebuild();
                });
                MenuUI.Button(_content, "Cancel", () => { _confirmDeleteId = 0; Rebuild(); });
            }
            else
            {
                MenuUI.Button(_content, $"▶ Enter World as {e.name}", () =>
                {
                    _error = "";
                    NetworkClient.Send(new EnterWorldMessage { characterId = e.id });
                });
                MenuUI.Button(_content, $"Delete {e.name}", () => { _confirmDeleteId = e.id; Rebuild(); });
            }
            MenuUI.Spacer(_content, 4);
        }

        if (count < _list.maxSlots)
            MenuUI.Button(_content, "Create New Character", () => { _error = ""; _creating = true; Rebuild(); });
        else
            MenuUI.Text(_content, "All character slots are full.", 16, TextAlignmentOptions.Center);
    }

    TMP_InputField _nameField;

    void BuildCreate()
    {
        MenuUI.Text(_content, "Create a character", 20, TextAlignmentOptions.Center);
        _nameField = MenuUI.Input(_content, "Name", false);
        if (_nameField != null) _nameField.text = _pendingName;   // restore across the rebuild

        // 3.1.4 — gated cascade from the roster: gender → race (for that gender) → class (for that pair).
        var opts = _list.createOptions ?? new CreateOption[0];

        string[] genders = Genders(opts);
        _genderIdx = Clamp(_genderIdx, genders.Length);
        string gender = Pick(genders, _genderIdx);

        string[] races = RacesFor(opts, gender);
        _raceIdx = Clamp(_raceIdx, races.Length);
        string race = Pick(races, _raceIdx);

        string[] classes = ClassesFor(opts, gender, race);
        _classIdx = Clamp(_classIdx, classes.Length);
        string cls = Pick(classes, _classIdx);

        // Changing an earlier pick resets the later ones to a valid default (avoids landing on a stale
        // out-of-range combination when the available options shrink).
        MenuUI.Button(_content, $"Gender:  {gender}",
            () => { _genderIdx = Next(_genderIdx, genders.Length); _raceIdx = 0; _classIdx = 0; Rebuild(); });
        MenuUI.Button(_content, $"Race:  {race}",
            () => { _raceIdx = Next(_raceIdx, races.Length); _classIdx = 0; Rebuild(); });
        MenuUI.Button(_content, $"Class:  {cls}",
            () => { _classIdx = Next(_classIdx, classes.Length); Rebuild(); });

        MenuUI.Spacer(_content, 6);
        MenuUI.Button(_content, "Create & Enter", () =>
        {
            _error = "";
            NetworkClient.Send(new CreateCharacterMessage
            {
                name   = _nameField != null ? _nameField.text.Trim() : "",
                gender = gender,
                race   = race,
                cls    = cls,
            });
        });

        if (_list.entries != null && _list.entries.Length > 0)
            MenuUI.Button(_content, "Back", () => { _error = ""; _creating = false; Rebuild(); });
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

    // Rebuilding: detach immediately (out of the layout) + destroy next frame. Child 0 is the persistent
    // "Character Select" title — keep it; rebuild only the roster/create rows below it.
    void ClearContent()
    {
        for (int i = _content.childCount - 1; i >= 1; i--)
        {
            var ch = _content.GetChild(i);
            ch.SetParent(null, false);
            Destroy(ch.gameObject);
        }
    }

    static string Pick(string[] opts, int idx)
        => opts != null && idx >= 0 && idx < opts.Length ? opts[idx] : "(none)";

    static int Next(int idx, int len) => len <= 0 ? 0 : (idx + 1) % len;
}
