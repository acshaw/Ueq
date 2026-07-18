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

        var instance = BuildFromPrefab(parent, prefab, controller, driveLocomotion);
        if (instance == null) return null;
        instance.name = $"Model_{gender}_{race}_{cls}";

        if (controller == null)
            Debug.LogWarning("[CharacterModelFactory] No locomotion controller — the body will not animate.");

        // Class weapon prop is player-only (mobs resolve their own art); attach it after the shared build.
        var anim = instance.GetComponentInChildren<Animator>();
        if (anim != null) AttachWeaponProp(anim, cls);

        return instance;
    }

    /// <summary>
    /// Shared instantiate + Animator-wiring recipe with no roster/class coupling (3.1.10 Stage 0). Used by the
    /// player path above and by <see cref="MobModel"/> to attach an arbitrary Synty body prefab. Sets the
    /// controller BEFORE adding <see cref="PlayerAnimator"/> so its Awake sees a live controller (no T-pose);
    /// <paramref name="driveLocomotion"/> feeds transform movement into the blend tree (works for mobs too —
    /// their transform is NetworkTransform-driven on clients).
    /// </summary>
    public static GameObject BuildFromPrefab(Transform parent, GameObject prefab,
                                             RuntimeAnimatorController controller, bool driveLocomotion)
    {
        if (prefab == null) return null;

        var instance = Object.Instantiate(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        var anim = instance.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            if (controller != null) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            if (driveLocomotion && anim.GetComponent<PlayerAnimator>() == null)
                anim.gameObject.AddComponent<PlayerAnimator>();
        }

        return instance;
    }

    // Parent the class's weapon prop to the Humanoid right-hand bone (rig-independent — works across every
    // Synty pack's avatar). Grip offsets are authored per class (on CharacterRoster since M2.10, RC4) and
    // tuned live in the 3.1.6 preview.
    static void AttachWeaponProp(Animator anim, string cls)
    {
        var weapon = CharacterRosterRegistry.GetWeaponProp(cls);
        if (weapon.prop == null) return;

        var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return; // non-humanoid or unrigged body — skip quietly

        var prop = Object.Instantiate(weapon.prop, hand);
        prop.transform.localPosition = weapon.gripPositionOffset;
        prop.transform.localRotation = Quaternion.Euler(weapon.gripEulerOffset);
        prop.name = $"Weapon_{cls}";

        // Cosmetic only — strip any colliders so the weapon can't block the click-to-target raycast or
        // perturb physics from the player's hand.
        foreach (var col in prop.GetComponentsInChildren<Collider>()) Object.Destroy(col);
    }
}
