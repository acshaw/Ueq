using UnityEngine;

// Attach to any object a player can select — enemy, NPC, player, interactable, etc.
public class Targetable : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly Color HostileColor  = new Color(1f, 0.25f, 0.25f);  // enemies/NPCs (original color)
    static readonly Color FriendlyColor = new Color(0.3f, 1f, 0.55f);  // players — never "hostile" (no PvP)

    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        ResolveRenderer();
    }

    // Players build their body at runtime (PlayerModel), after this Awake — a one-shot resolve here would
    // permanently miss it. Re-checking lazily on each SetHighlight call is cheap (only called on target
    // change, never per-frame) and self-heals once the body exists.
    void ResolveRenderer()
    {
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
    }

    /// <summary>hostile=true (default) preserves every existing caller's behavior (mobs/NPCs). Pass false
    /// for a friendly target (a player) so it doesn't visually read as an enemy.</summary>
    public void SetHighlight(bool on, bool hostile = true)
    {
        ResolveRenderer();
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColor, on ? (hostile ? HostileColor : FriendlyColor) : Color.white);
        _renderer.SetPropertyBlock(_mpb);
    }
}
