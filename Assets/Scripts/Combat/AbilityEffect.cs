using Mirror;
using UnityEngine;

public abstract class AbilityEffect : ScriptableObject
{
    public abstract void Apply(NetworkIdentity caster, NetworkIdentity target, AbilityDefinition source);
}

public enum ScalingStatType { None, Str, Sta, Agi, Dex, Int, Wis, Cha }
