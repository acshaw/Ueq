using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5.10 finding: this class used to live in HotbarUI.cs alongside the HotbarUI class. A
// MonoBehaviour whose name doesn't match its containing file consistently lost its script
// reference (m_Script: {fileID: 0}) when the HUD was saved via PrefabUtility.SaveAsPrefabAsset —
// reproducible across multiple clean rebuilds, not a one-off glitch. InventorySlotUI (its own
// file, matching its class name) never had this problem. Giving this class its own file fixes it.
public class HotbarSlotUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI cdText;
    [SerializeField] Image           cdOverlay;

    // Self-wire from the named child objects at runtime if the serialized refs didn't take — the
    // edit-time wiring (SerializedObject / Init) has proven flaky in a loop, so this guarantees every
    // slot resolves its label/cooldown widgets regardless. Children are named in CreateHotbarUI.
    void Awake()
    {
        if (nameText  == null) nameText  = FindChild<TextMeshProUGUI>("Name");
        if (cdText    == null) cdText    = FindChild<TextMeshProUGUI>("CDText");
        if (cdOverlay == null) cdOverlay = FindChild<Image>("CDOverlay");
    }

    T FindChild<T>(string childName) where T : Component
    {
        var t = transform.Find(childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    // Direct wiring from the scene builder (kept; the Awake fallback covers any that don't persist).
    public void Init(TextMeshProUGUI name, TextMeshProUGUI cd, Image overlay)
    {
        nameText  = name;
        cdText    = cd;
        cdOverlay = overlay;
    }

    public void Refresh(string label, float cooldownRemaining)
    {
        if (nameText)  nameText.text  = label;
        bool onCd = cooldownRemaining > 0.05f;
        if (cdText)    cdText.text    = onCd ? $"{cooldownRemaining:F1}s" : "";
        if (cdOverlay)
        {
            var c = cdOverlay.color;
            c.a = onCd ? 0.55f : 0f;
            cdOverlay.color = c;
        }
    }
}
