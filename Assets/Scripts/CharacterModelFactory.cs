using UnityEngine;

/// <summary>
/// Shared body-build recipe (3.1.6, PV3) used by both the in-world <see cref="PlayerModel"/> and the
/// character-create <see cref="CharacterPreview"/> so the two can't drift. Given a (gender, race, class),
/// it instantiates the roster body under a parent, wires the locomotion Animator, and attaches the class
/// weapon prop to the right-hand bone.
///
/// <paramref name="driveLocomotion"/> distinguishes the two callers: the in-world model adds
/// <see cref="PlayerAnimator"/> (so movement drives the blend tree), while the static preview leaves it off
/// (the Animator idles at Speed = 0).
/// </summary>
public static class CharacterModelFactory
{
    public static GameObject Build(Transform parent, Gender gender, string race, string cls,
                                   RuntimeAnimatorController controller, bool driveLocomotion,
                                   GameObject fallback = null)
    {
        var prefab = CharacterRosterRegistry.GetModel(gender, race, cls) ?? fallback;
        if (prefab == null)
        {
            Debug.LogWarning($"[CharacterModelFactory] No body model for {gender}/{race}/{cls} and no fallback.");
            return null;
        }

        var instance = Object.Instantiate(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.name = $"Model_{gender}_{race}_{cls}";

        var anim = instance.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            // Set the controller BEFORE adding PlayerAnimator so its Awake sees a live controller (no T-pose).
            if (controller != null) anim.runtimeAnimatorController = controller;
            else Debug.LogWarning("[CharacterModelFactory] No locomotion controller — the body will not animate.");
            anim.applyRootMotion = false;

            if (driveLocomotion && anim.GetComponent<PlayerAnimator>() == null)
                anim.gameObject.AddComponent<PlayerAnimator>();

            AttachWeaponProp(anim, cls);
        }

        return instance;
    }

    // Parent the class's weapon prop to the Humanoid right-hand bone (rig-independent — works across every
    // Synty pack's avatar). Grip offsets are authored per class and tuned live in the 3.1.6 preview.
    static void AttachWeaponProp(Animator anim, string cls)
    {
        var def = RaceClassRegistry.GetClass(cls);
        if (def == null || def.weaponPropPrefab == null) return;

        var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return; // non-humanoid or unrigged body — skip quietly

        var prop = Object.Instantiate(def.weaponPropPrefab, hand);
        prop.transform.localPosition = def.gripPositionOffset;
        prop.transform.localRotation = Quaternion.Euler(def.gripEulerOffset);
        prop.name = $"Weapon_{cls}";

        // Cosmetic only — strip any colliders so the weapon can't block the click-to-target raycast or
        // perturb physics from the player's hand.
        foreach (var col in prop.GetComponentsInChildren<Collider>()) Object.Destroy(col);
    }
}
