using UnityEngine;

// Attach to any object a player can select — enemy, NPC, interactable, etc.
public class Targetable : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    public void SetHighlight(bool on)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColor, on ? new Color(1f, 0.25f, 0.25f) : Color.white);
        _renderer.SetPropertyBlock(_mpb);
    }
}
