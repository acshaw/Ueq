using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The character-creation lineup (3.1.4, decision RM1) — one authored asset that is simultaneously the
/// create-form option source, the server-side validation whitelist, and the client body-model resolver.
///
/// A <see cref="RosterEntry"/> is one legal <c>(gender, race, class)</c> tuple plus the Synty body prefab
/// to show for it. Availability cascades gender → race → class; the model is looked up by the full tuple
/// (so a Dwarf can collapse both its classes to one body while a Human's classes each get a distinct one).
/// Lives at <c>Resources/CharacterRoster.asset</c> so both server and client load it at runtime.
/// **Stays a Unity asset, not DB-migrated (M2.10, RC5)** — it's prefab wiring (which Synty body/weapon
/// goes with which tuple), not authorable text/numeric content; mirrors the <c>MobModelCatalog</c>
/// precedent, which also never moved to DB despite mobs being fully DB-migrated.
///
/// Race extensibility (a real Elf later) = add rows + a <see cref="RaceDefinition"/>; no code change.
/// </summary>
[CreateAssetMenu(menuName = "Ueq/Character Roster")]
public class CharacterRoster : ScriptableObject
{
    public List<RosterEntry> entries = new();

    [Tooltip("Locomotion controller (PlayerLocomotion) assigned to instantiated bodies. Lives here so the " +
             "runtime create-form preview (3.1.6) can resolve it via Resources without a serialized scene ref.")]
    public RuntimeAnimatorController locomotionController;

    // M2.10 (RC4) — moved off ClassDefinition once classes became DB-backed content: a weapon prop is
    // pure Unity-asset wiring (a prefab reference + tuning vectors), not authorable text/numeric content,
    // so it stays here alongside the roster's other prefab wiring instead of being the one dangling asset
    // field left on an otherwise-clean DB row.
    [Tooltip("Cosmetic weapon per class, attached to the body's right-hand bone (Warrior sword / Wizard " +
             "staff / Cleric sceptre). Shown in the create preview and in-world.")]
    public List<ClassWeaponProp> classWeaponProps = new();
}

[System.Serializable]
public struct RosterEntry
{
    public Gender     gender;
    public string     race;         // matches RaceDefinition.raceName
    public string     cls;          // matches ClassDefinition.className
    public GameObject modelPrefab;  // the Synty body shown in-world for this tuple
}

[System.Serializable]
public struct ClassWeaponProp
{
    public string     className;           // matches ClassDefinition.className
    public GameObject prop;                // leave empty for no prop, or for a body that already ships one
    public Vector3    gripPositionOffset;   // local offset relative to the right-hand bone
    public Vector3    gripEulerOffset;      // local euler offset relative to the right-hand bone
}
