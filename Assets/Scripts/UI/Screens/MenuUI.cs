using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// M3.1.1 — small procedural uGUI factory for the client screen shell. Matches the project's existing
/// convention (TMP text + legacy <see cref="Button"/>/<see cref="Image"/>, CanvasScaler @1920×1080). The
/// panels are intentionally minimal/unstyled — visual design lands in 3.1.2/3.1.3/3.1.6/3.1.7.
/// </summary>
public static class MenuUI
{
    public static readonly Color PanelColor  = new Color(0.06f, 0.07f, 0.10f, 0.92f);
    public static readonly Color FieldColor  = new Color(0.14f, 0.15f, 0.19f, 1f);
    public static readonly Color ButtonColor = new Color(0.20f, 0.34f, 0.52f, 1f);
    public static readonly Color TextColor   = new Color(0.92f, 0.93f, 0.96f, 1f);
    public static readonly Color ErrorColor  = new Color(1f, 0.5f, 0.5f, 1f);

    // A full-screen stretch RectTransform under `parent`.
    public static RectTransform FullScreen(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    // A centered card (vertical layout) — the body of a menu screen.
    public static RectTransform Card(Transform parent, float width, float height)
    {
        var go = new GameObject("Card", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.color = PanelColor;

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;   // honor each element's LayoutElement.preferredHeight (44px buttons, etc.)
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Height follows the content so the card is exactly tall enough and stays vertically centered — the
        // `height` arg is now just an initial hint (fixed content won't overflow off-screen). Width stays fixed.
        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    public static Image FullScreenImage(Transform parent, string name, Color color)
    {
        var rt = FullScreen(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // ── Backdrops (3.1.2) ────────────────────────────────────────────────────────

    /// <summary>Full-screen background image that cover-fits (fills + crops, keeps aspect, centered) via an
    /// AspectRatioFitter — the correct way to show a 16:9 art plate on any window aspect.</summary>
    public static Image CoverBackground(Transform parent, Sprite sprite)
    {
        var rt = FullScreen(parent, "Background");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // centered; the fitter drives the size
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
        var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : 16f / 9f;
        return img;
    }

    /// <summary>A full-screen overlay tinted by a 1-D gradient sprite — used for the left legibility scrim
    /// (dark → transparent) and the art-free fallback background.</summary>
    public static Image GradientOverlay(Transform parent, string name, Color a, Color b, bool horizontal)
    {
        var rt = FullScreen(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = GradientSprite(a, b, horizontal);
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    static Sprite GradientSprite(Color a, Color b, bool horizontal)
    {
        const int n = 128;
        var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int i = 0; i < n; i++)
        {
            var c = Color.Lerp(a, b, i / (float)(n - 1)); // index 0 = a (left / bottom)
            if (horizontal) tex.SetPixel(i, 0, c); else tex.SetPixel(0, i, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Give a TMP element a soft drop shadow via the font's underlay (per-instance material — does
    /// not touch other text). Adds depth/legibility for the wordmark over art.</summary>
    public static void AddSoftShadow(TextMeshProUGUI t, Color color, Vector2 offset, float softness)
    {
        var mat = t.fontMaterial; // accessing fontMaterial creates a per-instance material
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
    }

    public static TextMeshProUGUI Text(Transform parent, string text, int size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = TextColor;
        t.alignment = align;
        t.raycastTarget = false;
        SetPreferredHeight(go, size + 8);
        return t;
    }

    public static TMP_InputField Input(Transform parent, string placeholder, bool password)
    {
        var resources = new TMP_DefaultControls.Resources();
        var go = TMP_DefaultControls.CreateInputField(resources);
        go.name = "Input_" + placeholder;
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        if (img != null) img.color = FieldColor;

        var field = go.GetComponent<TMP_InputField>();
        if (field.placeholder is TextMeshProUGUI ph) { ph.text = placeholder; ph.color = new Color(0.6f, 0.62f, 0.66f, 1f); }
        if (field.textComponent != null) field.textComponent.color = TextColor;
        if (password) field.contentType = TMP_InputField.ContentType.Password;

        SetPreferredHeight(go, 40);
        return field;
    }

    public static Button Button(Transform parent, string label, System.Action onClick)
        => Button(parent, label, onClick, ButtonColor);

    public static Button Button(Transform parent, string label, System.Action onClick, Color baseColor,
                                int fontSize = 22, float height = 44)
    {
        var go = new GameObject("Button_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = baseColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Hover / press / focus feedback (T5) — the default Selectable has no states configured.
        var colors = btn.colors;
        colors.normalColor      = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.14f);
        colors.selectedColor    = Color.Lerp(baseColor, Color.white, 0.10f);
        colors.pressedColor     = Color.Lerp(baseColor, Color.black, 0.18f);
        colors.disabledColor    = new Color(0.3f, 0.3f, 0.32f, 0.6f);
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(() => onClick());

        var label2 = Text(go.transform, label, fontSize, TextAlignmentOptions.Center);
        var lrt = label2.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        SetPreferredHeight(go, height);
        return btn;
    }

    // A thin spacer for vertical layout.
    public static void Spacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetPreferredHeight(go, height);
    }

    public static void SetPreferredHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
    }

    // ── Keyboard navigation (forms) ──────────────────────────────────────────────
    // Unity's UI doesn't treat Tab as a navigation key, and this project uses the new Input System, so we
    // poll here: Tab / Shift+Tab cycles the fields, Enter submits. Call from a panel's Update.

    public static void HandleFormKeys(Selectable[] fields, System.Action submit)
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame)
        {
            bool back = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            CycleFocus(fields, back);
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            submit?.Invoke();
    }

    /// <summary>True on the frame Escape is pressed — panels use it to go back / cancel.</summary>
    public static bool BackPressed()
        => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    static void CycleFocus(Selectable[] fields, bool back)
    {
        if (fields == null || fields.Length == 0) return;
        var es = EventSystem.current;
        if (es == null) return;

        var cur = es.currentSelectedGameObject;
        int idx = -1;
        for (int i = 0; i < fields.Length; i++)
            if (fields[i] != null && fields[i].gameObject == cur) { idx = i; break; }

        int len = fields.Length;
        int next = idx < 0 ? 0 : (back ? (idx - 1 + len) % len : (idx + 1) % len);
        Focus(fields[next]);
    }

    /// <summary>Select a field (and, for an input field, put it straight into edit mode) so the user can
    /// type without clicking. Call from a panel's OnShow to focus the first field.</summary>
    public static void Focus(Selectable field)
    {
        if (field == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(field.gameObject);
        if (field is TMP_InputField tmp) tmp.ActivateInputField();
    }
}
