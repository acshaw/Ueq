using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/Ability Tag")]
public class AbilityTag : ScriptableObject
{
    public string tagId      = "";
    public string displayName = "";

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(tagId))
            tagId = name;
    }
#endif
}
