using UnityEngine;

/// <summary>
/// Runtime-only since M2.9 — built by <see cref="AbilityRegistry"/> from the DB <c>ability_tags</c>
/// table (embedded inline in an ability's tag refs, not looked up independently at runtime). No longer
/// authored as an asset; author tags in the web Ability Tag Editor.
/// </summary>
public class AbilityTag : ScriptableObject
{
    public string tagId      = "";
    public string displayName = "";
}
