using Mirror;
using UnityEngine;

/// <summary>
/// Pre-spawn character select / creation panel. Shown after login (1.4) and whenever the client is
/// connected + authenticated but has no player object — i.e. before entering and again after camping
/// back from the world (1.6). IMGUI per decision D3 (throwaway; the polished version is 1.7).
/// Lives on the NetworkManager GameObject next to LoginUI.
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    bool _registered;
    bool _requested;
    bool _haveList;
    bool _wasInWorld;
    CharacterListMessage _list;
    string _error = "";

    // view state
    bool   _creating;            // showing the create form vs. the roster
    long   _confirmingDeleteId;  // character id pending a delete confirm (0 = none)

    // create-form fields
    string _name = "";
    int    _raceIdx;
    int    _classIdx;

    void Update()
    {
        if (NetworkClient.active)
        {
            if (!_registered)
            {
                NetworkClient.RegisterHandler<CharacterListMessage>(OnList);
                NetworkClient.RegisterHandler<CharacterActionResult>(OnResult);
                _registered = true;
            }

            // Returning to select after camping (localPlayer went away) → refresh the roster.
            bool inWorld = NetworkClient.localPlayer != null;
            if (inWorld) _wasInWorld = true;
            else if (_wasInWorld)
            {
                _wasInWorld = false;
                _requested = _haveList = _creating = false;
                _confirmingDeleteId = 0;
                _error = "";
            }
        }
        else
        {
            // Reset for the next client session (reconnect / host restart).
            _registered = _requested = _haveList = _creating = _wasInWorld = false;
            _confirmingDeleteId = 0;
            _list = default;
            _error = "";
        }
    }

    bool ShouldShow =>
        NetworkClient.active &&
        NetworkClient.connection != null &&
        NetworkClient.connection.isAuthenticated &&
        NetworkClient.localPlayer == null;

    void OnList(CharacterListMessage msg)
    {
        _list = msg;
        _haveList = true;
        if (msg.entries == null || msg.entries.Length == 0) _creating = true; // nothing to show → create
    }

    void OnResult(CharacterActionResult msg) { if (!msg.ok) _error = msg.error; }

    void OnGUI()
    {
        if (!ShouldShow) return;

        if (!_requested)
        {
            NetworkClient.Send(new CharacterListRequest());
            _requested = true;
        }

        const float W = 360f;
        GUILayout.BeginArea(new Rect(Screen.width / 2f - W / 2f, 50, W, 520), GUI.skin.box);
        GUILayout.Label("<b>Character Select</b>");

        if (!_haveList) { GUILayout.Label("Loading…"); GUILayout.EndArea(); return; }

        if (_creating) DrawCreate();
        else           DrawRoster();

        if (!string.IsNullOrEmpty(_error))
        {
            GUILayout.Space(6);
            GUILayout.Label($"<color=#ff8080>{_error}</color>");
        }

        GUILayout.EndArea();
    }

    void DrawRoster()
    {
        int count = _list.entries != null ? _list.entries.Length : 0;
        GUILayout.Label($"Characters ({count}/{_list.maxSlots})");

        for (int i = 0; i < count; i++)
        {
            var e = _list.entries[i];
            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>{e.name}</b>  —  Level {e.level} {e.race} {e.cls}");

            if (_confirmingDeleteId == e.id)
            {
                GUILayout.Label($"<color=#ff8080>Delete {e.name}? This cannot be undone.</color>");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm Delete"))
                {
                    _confirmingDeleteId = 0;
                    _error = "";
                    _haveList = false; // show "Loading…" until the server resends the roster
                    NetworkClient.Send(new DeleteCharacterMessage { characterId = e.id });
                }
                if (GUILayout.Button("Cancel")) _confirmingDeleteId = 0;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Enter World"))
                {
                    _error = "";
                    NetworkClient.Send(new EnterWorldMessage { characterId = e.id });
                }
                if (GUILayout.Button("Delete")) _confirmingDeleteId = e.id;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        GUILayout.Space(10);
        if (count < _list.maxSlots)
        {
            if (GUILayout.Button("Create New Character"))
            {
                _error = "";
                _name = "";
                _creating = true;
            }
        }
        else
        {
            GUILayout.Label("All character slots are full.");
        }
    }

    void DrawCreate()
    {
        GUILayout.Space(6);
        GUILayout.Label("Create a character");
        GUILayout.Label("Name");
        _name = GUILayout.TextField(_name);

        _raceIdx  = OptionRow("Race",  _list.raceOptions,  _raceIdx);
        _classIdx = OptionRow("Class", _list.classOptions, _classIdx);

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create & Enter"))
        {
            _error = "";
            NetworkClient.Send(new CreateCharacterMessage
            {
                name = _name.Trim(),
                race = Pick(_list.raceOptions,  _raceIdx),
                cls  = Pick(_list.classOptions, _classIdx),
            });
        }
        // Allow backing out only if there's already a character to return to.
        if (_list.entries != null && _list.entries.Length > 0 && GUILayout.Button("Back"))
        {
            _error = "";
            _creating = false;
        }
        GUILayout.EndHorizontal();
    }

    static int OptionRow(string label, string[] opts, int idx)
    {
        GUILayout.Space(4);
        GUILayout.Label(label);
        if (opts == null || opts.Length == 0) { GUILayout.Label("(none available)"); return 0; }

        GUILayout.BeginHorizontal();
        for (int i = 0; i < opts.Length; i++)
        {
            bool sel = i == idx;
            if (GUILayout.Toggle(sel, opts[i], GUI.skin.button) && !sel) idx = i;
        }
        GUILayout.EndHorizontal();
        return idx;
    }

    static string Pick(string[] opts, int idx)
        => opts != null && idx >= 0 && idx < opts.Length ? opts[idx] : "";
}
