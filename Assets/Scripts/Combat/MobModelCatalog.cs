using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3.1.10 Increment B — maps a <c>modelId</c> to a body prefab, mirroring how the player's
/// <c>CharacterRoster</c> references Synty bodies. Prefabs are referenced <b>in place</b> (any folder/pack) —
/// no copying into Resources. A single asset lives at <c>Assets/Resources/MobModelCatalog.asset</c> so
/// <see cref="MobModelRegistry"/> can load it at runtime on clients.
///
/// A mob resolves its body by <c>modelId</c> (its explicit id, or its mob id by convention). Populate the
/// catalog one-click via <c>Tools/Character/Build Mob Model Catalog</c>, then edit entries in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Ueq/Mob Model Catalog")]
public class MobModelCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("The id a mob references — its explicit modelId, or its mob id by convention. Not the file " +
                 "name of the prefab; name it however reads best for authoring.")]
        public string modelId;

        [Tooltip("The body prefab, referenced in place (any pack folder). Must be a rigged character prefab " +
                 "(SkinnedMeshRenderer + Animator), not a raw .fbx or a static prop.")]
        public GameObject prefab;

        [Tooltip("Optional. Leave blank for Humanoid bodies — they retarget the shared locomotion controller " +
                 "for free. Set this ONLY for non-Humanoid / Generic rigs that ship their own idle/walk clips.")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("Local position offset applied to the body. Synty bodies are authored feet-at-origin so this " +
                 "stays zero; a non-Synty pack (e.g. a store-bought animal) may pivot elsewhere — nudge this if " +
                 "the body floats or sinks relative to the placeholder cube.")]
        public Vector3 offset;

        [Tooltip("Local rotation offset (degrees), for bodies that don't face +Z by default.")]
        public Vector3 eulerOffset;
    }

    public List<Entry> entries = new();
}
