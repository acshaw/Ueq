using UnityEngine;

/// <summary>M3.1.1 — the client shell's logical screens. (3.1.2: Login/Register/Connecting folded into
/// inline modes of the Title screen, so the pre-connect flow stays on the title art with no fade.)</summary>
public enum ClientScreen
{
    Title,
    CharacterSelect,
    InWorld,
}

/// <summary>
/// M3.1.1 — base for a shell screen. The <see cref="UIScreenManager"/> creates a full-screen root, adds the
/// concrete panel, and calls <see cref="Init"/> once; the panel builds its (minimal, unstyled) uGUI in
/// <see cref="Build"/>. Visibility is toggled by the manager through the fader.
/// </summary>
public abstract class ScreenPanel : MonoBehaviour
{
    protected UIScreenManager Manager { get; private set; }
    protected RectTransform Root { get; private set; }

    public void Init(UIScreenManager mgr)
    {
        Manager = mgr;
        Root = (RectTransform)transform;
        Build();
        gameObject.SetActive(false);
    }

    protected abstract void Build();

    /// <summary>Called each time this screen becomes the active one (after the fade covers).</summary>
    public virtual void OnShow() { }

    /// <summary>Called when leaving this screen.</summary>
    public virtual void OnHide() { }
}
