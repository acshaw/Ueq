using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5.3 — own file per the 5.10 lesson: a MonoBehaviour class not matching its containing filename has
// reproducibly lost its script reference when the HUD is saved via PrefabUtility.SaveAsPrefabAsset (see
// HotbarSlotUI.cs's history). Giving this class its own file avoids that failure mode entirely.
public class PartyFrameSlotUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Image           healthFill;
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] Image           background;

    static readonly Color LiveColor        = new Color(0.3f, 0.55f, 0.85f); // blue — distinct from the
                                                                              // player frame's green and
                                                                              // the target frame's red
    static readonly Color PlaceholderColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
    static readonly Color BgNormal         = new Color(0.05f, 0.05f, 0.05f, 0.82f);
    static readonly Color BgSelected       = new Color(0.35f, 0.30f, 0.05f, 0.9f);

    public NetworkIdentity Member { get; private set; }

    void Awake()
    {
        if (nameText   == null) nameText   = FindChild<TextMeshProUGUI>("Name");
        if (healthFill == null) healthFill = FindChild<Image>("HealthBarBG/HealthBarFill");
        if (healthText == null) healthText = FindChild<TextMeshProUGUI>("HealthBarBG/HealthText");
        if (background == null) background = GetComponent<Image>();
    }

    T FindChild<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    // Direct wiring from the scene builder (kept; the Awake fallback covers any that don't persist).
    public void Init(TextMeshProUGUI name, Image fill, TextMeshProUGUI hpText, Image bg)
    {
        nameText   = name;
        healthFill = fill;
        healthText = hpText;
        background = bg;
    }

    public void Show(NetworkIdentity member)
    {
        Member = member;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        Member = null;
        gameObject.SetActive(false);
    }

    public void RefreshLive(string label, int current, int max)
    {
        if (nameText) nameText.text = label;
        float pct = max > 0 ? (float)current / max : 0f;
        if (healthFill)
        {
            healthFill.rectTransform.anchorMax = new Vector2(pct, 1f);
            healthFill.color = LiveColor;
        }
        if (healthText) healthText.text = $"{current} / {max}";
    }

    // GP7-A — a member in a different zone isn't observed by this client (Health/Nameplate simply aren't
    // replicated here), so show the name only, greyed, with no live bar rather than stale/fake data.
    public void RefreshPlaceholder(string label)
    {
        if (nameText) nameText.text = label;
        if (healthFill)
        {
            healthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            healthFill.color = PlaceholderColor;
        }
        if (healthText) healthText.text = "";
    }

    // Cosmetic only — highlights whichever slot the local player last F-key-targeted (GP11).
    public void SetSelected(bool selected)
    {
        if (background) background.color = selected ? BgSelected : BgNormal;
    }
}
