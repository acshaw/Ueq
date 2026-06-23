using System.Collections.Generic;
using UnityEngine;

public class AbilityRegistry : MonoBehaviour
{
    public static AbilityRegistry Instance { get; private set; }

    readonly Dictionary<string, AbilityDefinition> _abilities = new();

    void Awake()
    {
        Instance = this;
        foreach (var def in Resources.LoadAll<AbilityDefinition>("Abilities"))
            Register(def);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Register(AbilityDefinition def)
    {
        if (def != null && !string.IsNullOrEmpty(def.abilityId))
            _abilities[def.abilityId] = def;
    }

    public AbilityDefinition Get(string abilityId)
        => string.IsNullOrEmpty(abilityId) ? null : _abilities.GetValueOrDefault(abilityId);
}
