using UnityEngine;

/// <summary>
/// 5.12 (DC5) — the procedural moon. A disc (quad) on a pivot that rotates across
/// <see cref="WorldClock.DayFraction"/>, shaded by <c>Ueq/ProceduralMoon</c> reading
/// <see cref="WorldClock.LunarFraction"/> (DC5 — no phase textures).
///
/// The pivot uses the SAME angle formula as <see cref="SunDriver"/> (not a +180 offset — see the fixed
/// bug note on <see cref="LateUpdate"/>), because the two components mean different physical things:
/// SunDriver's angle sets the Directional Light's ray-travel direction (forward = where light goes, so
/// "forward = down" correctly means "sun is up" at noon); this rig's angle sets a POSITION offset for a
/// visible disc (the disc sits along the pivot's forward direction, so "forward = down" would place the
/// disc BELOW the camera). Those are inverses of each other by construction, so reusing the sun's own
/// formula unmodified already places the moon on the opposite side of the sky — the +180 in the original
/// implementation double-flipped it back onto the sun's own side (visible overhead by day, hidden by
/// night — exactly backwards), confirmed by the user's first in-editor test.
///
/// The moon is real geometry, so it can't sit at a fixed world position and still look "infinitely far
/// away" once the player crosses a 5000+-unit zone offset; only the sun's pure light-rotation and the
/// inherently camera-relative sky shader are exempt from that problem. This rig therefore recenters on
/// the camera every frame.
/// </summary>
public class MoonRig : MonoBehaviour
{
    [SerializeField] Transform disc;        // child quad — assigned by Tools/World Clock/Setup Scene
    // Must clear the largest zone's terrain diagonal (Creslin's Field is 1500x1500u, diagonal ~2121u) with
    // real margin, or the disc z-fights against distant mountain/cliff silhouettes at grazing "moonrise"
    // angles — the depth buffer has its worst precision near the far clip plane, and 900 sat right in the
    // middle of plausible terrain depths, causing the rapid flicker seen near the horizon. Also must stay
    // safely under the camera's far clip plane (4000, see Player.prefab) rather than butt up against it,
    // for the same precision reason. visualScale is scaled with distance to keep the same apparent size
    // (angular size ~= visualScale / distance for a small-angle billboard).
    [SerializeField] float distance    = 2800f;
    [SerializeField] float visualScale = 187f;

    [Header("Dark side")]
    [Tooltip("Matching the dark side's color exactly to the sky (SkyDriver.CurrentZenithColor) still reads " +
             "as a stark void up close, since it's a flat, star-free patch sitting right next to a bright " +
             "crescent. Real moons aren't truly black either — dimly lit by earthshine (reflected planet " +
             "light). This blends a floor color in so the dark side never gets as dark as the raw night sky.")]
    [SerializeField] Color earthshineTint = new Color(0.25f, 0.25f, 0.28f);
    [Tooltip("How much of the earthshine floor to blend in — 0 = exact (too dark) sky match, 1 = flat tint.")]
    [SerializeField, Range(0f, 1f)] float earthshineAmount = 0.3f;

    Camera   _cam;
    Renderer _discRenderer;
    Material _mat;
    float    _yaw;
    static readonly int LunarFractionId = Shader.PropertyToID("_LunarFraction");
    static readonly int ShadowColorId   = Shader.PropertyToID("_ShadowColor");

    void Awake()
    {
        _cam = Camera.main;
        // Cache yaw ONCE — matching SunDriver's pattern. Reading transform.eulerAngles.y back every
        // frame (the original bug) round-trips through Unity's quaternion->Euler decomposition, which
        // isn't guaranteed stable: the same rotation has multiple equivalent Euler representations (e.g.
        // (X,Y,Z) vs (180-X, Y+180, Z+180)), and floating-point noise can flip between them. When it did,
        // the next frame's Quaternion.Euler(angle, flippedY, 0) produced a rotation at the SAME elevation
        // but the OPPOSITE azimuth — which read as a second moon rising from the opposite horizon,
        // flickering against the real one, until the arc reached its peak (where both representations
        // collapse to the same point) and it "snapped into focus."
        _yaw = transform.eulerAngles.y;
        if (disc != null)
        {
            _discRenderer = disc.GetComponent<Renderer>();
            // Renderer.material auto-instantiates on first access, unlike RenderSettings.skybox — safe to
            // mutate directly without an explicit Instantiate() (see SkyDriver's comment on that distinction).
            if (_discRenderer != null) _mat = _discRenderer.material;
            disc.localScale = Vector3.one * visualScale;
        }
    }

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        if (!ZoneClientHelper.CurrentZoneUsesDayNightCycle())
        {
            if (_discRenderer != null) _discRenderer.enabled = false;
            return;
        }
        if (_discRenderer != null) _discRenderer.enabled = true;

        transform.position = _cam.transform.position;

        float f = WorldClock.DayFraction;
        // SAME formula as SunDriver — see the class doc comment for why this (not +180) is what places
        // the moon opposite the sun in the sky. Uses the cached _yaw (see Awake), not a live read-back.
        float angle = f * 360f - 90f;
        transform.rotation = Quaternion.Euler(angle, _yaw, 0f);

        if (disc != null)
            disc.localPosition = new Vector3(0f, 0f, distance);

        if (_mat != null)
        {
            _mat.SetFloat(LunarFractionId, WorldClock.LunarFraction);
            // Dark side tracks the current sky tone (see SkyDriver.CurrentZenithColor) instead of a fixed
            // color — stays fully opaque (ProceduralMoon.shader always outputs alpha 1) so it still
            // correctly hides stars behind it; only the COLOR follows the sky, not the transparency. Blended
            // with a earthshine floor (see field tooltip) so it doesn't read as a stark black void even when
            // the sky itself is near-black at night.
            Color shadow = Color.Lerp(SkyDriver.CurrentZenithColor, earthshineTint, earthshineAmount);
            _mat.SetColor(ShadowColorId, shadow);
        }
    }
}
