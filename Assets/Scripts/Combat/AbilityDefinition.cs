using System.Collections.Generic;
using UnityEngine;

public enum AbilityTargetType { Self, SingleTarget }

[CreateAssetMenu(menuName = "Ueq/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    public string abilityId   = "";
    public string displayName = "New Ability";
    [TextArea(2, 4)]
    public string description = "";

    [Header("Targeting")]
    public AbilityTargetType targetingType = AbilityTargetType.SingleTarget;
    public float             range         = 20f;
    public float             castTime      = 0f;

    [Header("Resource")]
    public int manaCost = 0;

    [Header("Animation")]
    [Tooltip("Animator trigger fired on a successful cast (empty = no animation). " +
             "Must match a Trigger parameter + state in the player's controller.")]
    public string animTrigger = "";

    [Header("Tags")]
    public List<AbilityTag> tags = new();

    [Header("Cooldowns")]
    public List<CooldownLink> cooldownLinks = new();

    [Header("Effects")]
    public List<AbilityEffect> effects = new();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(abilityId))
            abilityId = name;
    }
#endif
}
