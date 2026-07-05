using Mirror;

// Wire messages for the pre-spawn character-select flow (1.5). Client requests its list, then either
// creates-and-enters a new character or enters an existing one; the server validates and spawns.

/// <summary>Client → server: send me this account's characters + the creation options.</summary>
public struct CharacterListRequest : NetworkMessage { }

/// <summary>Server → client: the account's characters and the create-form options.</summary>
public struct CharacterListMessage : NetworkMessage
{
    public CharacterListEntry[] entries;
    public string[]            raceOptions;   // legacy (all races) — kept for the retired IMGUI select UI
    public string[]            classOptions;  // legacy (all classes) — kept for the retired IMGUI select UI
    public CreateOption[]      createOptions; // 3.1.4: the gated gender→race→class lineup (authority)
    public int                 maxSlots;      // character cap (1.6, O1) — Create disabled at the cap
}

/// <summary>One legal creation tuple (3.1.4). The uGUI create form filters these into the
/// gender → race → class cascade; the server validates a request against the same roster.
/// Gender travels as its enum name ("Male"/"Female") to keep the wire struct string-only.</summary>
public struct CreateOption
{
    public string gender;
    public string race;
    public string cls;
}

/// <summary>One row in the character-select list (level is derived server-side before sending).</summary>
public struct CharacterListEntry
{
    public long   id;
    public string name;
    public string gender;
    public string race;
    public string cls;
    public int    level;
}

/// <summary>Client → server: create a new character and enter the world as it (D2: create = enter).</summary>
public struct CreateCharacterMessage : NetworkMessage
{
    public string name;
    public string gender;   // 3.1.4 — enum name ("Male"/"Female"); server validates the tuple
    public string race;
    public string cls;
}

/// <summary>Client → server: enter the world as an existing character.</summary>
public struct EnterWorldMessage : NetworkMessage
{
    public long characterId;
}

/// <summary>Client → server: delete a character (D7).</summary>
public struct DeleteCharacterMessage : NetworkMessage
{
    public long characterId;
}

/// <summary>Client → server: camp the current character — save + despawn + return to select (1.6, O4).</summary>
public struct CampMessage : NetworkMessage { }

/// <summary>Server → client: a create/enter/delete request that failed validation (no spawn). On
/// success the player simply spawns and the select UI hides — no result message is needed.</summary>
public struct CharacterActionResult : NetworkMessage
{
    public bool   ok;
    public string error;
}
