using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The character-creation lineup (3.1.4, decision RM1) — one authored asset that is simultaneously the
/// create-form option source, the server-side validation whitelist, and the client body-model resolver.
///
/// A <see cref="RosterEntry"/> is one legal <c>(gender, race, class)</c> tuple plus the Synty body prefab
/// to show for it. Availability cascades gender → race → class; the model is looked up by the full tuple
/// (so a Dwarf can collapse both its classes to one body while a Human's classes each get a distinct one).
/// Lives at <c>Resources/CharacterRoster.asset</c> so both server and client load it at runtime; M2.10 can
/// move the backing store to the DB behind <see cref="CharacterRosterRegistry"/>'s lookups.
///
/// Race extensibility (a real Elf later) = add rows + a <see cref="RaceDefinition"/>; no code change.
/// </summary>
[CreateAssetMenu(menuName = "Ueq/Character Roster")]
public class CharacterRoster : ScriptableObject
{
    public List<RosterEntry> entries = new();
}

[System.Serializable]
public struct RosterEntry
{
    public Gender     gender;
    public string     race;         // matches RaceDefinition.raceName
    public string     cls;          // matches ClassDefinition.className
    public GameObject modelPrefab;  // the Synty body shown in-world for this tuple
}
