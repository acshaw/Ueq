using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 5.12 (DC3/DC10) — drives the runtime-instanced Ueq/StylizedSky skybox material + ambient light color
/// from <see cref="WorldClock.DayFraction"/>. <c>Instantiate()</c>s the skybox material into a runtime
/// copy in <see cref="Awake"/> so tuning during Play never dirties the shared <c>.mat</c> asset on disk —
/// <c>RenderSettings.skybox</c> has no built-in per-use instancing the way <c>Renderer.material</c> does,
/// so this instantiation is load-bearing, not optional.
///
/// Also owns distance fog (post-8.8-session follow-up): raising the camera's far clip plane for the moon
/// fix meant players could suddenly see across an entire zone, which defeats the EQ-style trick of hiding
/// mob spawn pop-in behind limited draw distance. Fog is the modern equivalent of that same trick — it
/// fades geometry color toward the fog color with distance instead of hard-clipping it, so an object
/// fading in/out reads as "emerging from haze" instead of "popping into existence." <c>Ueq/LowPolyTerrain</c>
/// (the terrain shader) already had full URP fog support wired in from the start (unused until now, since
/// RenderSettings.fog was never turned on) — nothing to change there. Deliberately does NOT touch
/// <c>Ueq/StylizedSky</c> (the skybox itself is exempt from fog by default in Unity/URP — you don't want
/// the sky fogging into itself) or <c>Ueq/ProceduralMoon</c> (the moon is meant to read as being far beyond
/// the fog layer, like the sky; RenderSettings.fog only ever affects a shader that explicitly opts in via
/// MixFog, so leaving that shader untouched is sufficient — no risk of it fading unexpectedly). Fog color
/// is matched to the sky's own horizon color (read back from the same material) so the point where terrain
/// fully fogs out blends into the horizon instead of showing a mismatched "wall."
///
/// Also exposes <see cref="CurrentZenithColor"/> (post-8.8-session follow-up #2) so <see cref="MoonRig"/>
/// can color its dark side to match the current sky instead of a fixed dark color, WITHOUT using alpha
/// transparency to do it — an earlier attempt made the dark side mostly see-through so it would "blend
/// with the sky," but that let bright night-sky stars punch straight through the moon's dark side as it
/// crossed them, reading as "stars in front of the moon." Real moons occult stars behind them; the fix
/// keeps the moon disc fully opaque (correct occlusion) and gets the "blends with the sky" effect from a
/// dynamically-colored, still-opaque shadow side instead.
/// </summary>
public class SkyDriver : MonoBehaviour
{
    [SerializeField] Gradient ambientDayNight = DefaultAmbientGradient();

    [Header("Distance fog")]
    [Tooltip("Linear keeps the hidden-until distance precise and tunable against SpawnPoint activation radii. " +
             "Start/end distance are NOT set here — they're server/DB-authored via WorldClock (web-tunable in " +
             "the World Clock admin page), read every frame below so a change applies identically to every peer.")]
    [SerializeField] FogMode fogMode = FogMode.Linear;

    [Header("Stars")]
    [Tooltip("Day Fraction (0=midnight,0.5=noon) at which stars are FULLY hidden, mirrored around noon — " +
             "e.g. 0.25 means zero star visibility for the whole 0.25-0.75 daytime window. Computed directly " +
             "from DayFraction, not from the day/night color curve (which is already 50% by dawn/dusk).")]
    [SerializeField, Range(0f, 0.5f)] float starHideStart = 0.25f;
    [Tooltip("How much Day Fraction before/after the hide boundary stars take to fade fully in/out.")]
    [SerializeField, Range(0.001f, 0.25f)] float starFadeWidth = 0.08f;

    Material _runtimeSky;
    Color    _dayHorizonColor, _dawnDuskHorizonColor, _nightHorizonColor;
    Color    _dayZenithColor, _nightZenithColor;
    static readonly int DayAmountId            = Shader.PropertyToID("_DayAmount");
    static readonly int NightAmountId          = Shader.PropertyToID("_NightAmount");
    static readonly int DawnDuskAmountId       = Shader.PropertyToID("_DawnDuskAmount");
    static readonly int SunDirectionId         = Shader.PropertyToID("_SunDirection");
    static readonly int DayHorizonColorId      = Shader.PropertyToID("_DayHorizonColor");
    static readonly int DawnDuskHorizonColorId = Shader.PropertyToID("_DawnDuskHorizonColor");
    static readonly int NightHorizonColorId    = Shader.PropertyToID("_NightHorizonColor");
    static readonly int DayZenithColorId       = Shader.PropertyToID("_DayZenithColor");
    static readonly int NightZenithColorId     = Shader.PropertyToID("_NightZenithColor");
    static readonly int StarVisibilityId       = Shader.PropertyToID("_StarVisibility");

    /// <summary>Current overall sky tone (zenith colors blended by day/night), read by <see cref="MoonRig"/>
    /// to color its dark side so it blends into the current sky instead of a fixed dark color — and,
    /// critically, does so at full opacity (see MoonRig) so it still correctly occults stars behind it.</summary>
    public static Color CurrentZenithColor { get; private set; } = Color.black;

    void Awake()
    {
        if (RenderSettings.skybox != null)
        {
            _runtimeSky = Instantiate(RenderSettings.skybox);
            RenderSettings.skybox = _runtimeSky;

            // Cache the horizon colors once — read back from the same material so fog automatically tracks
            // any future tuning of the sky's own palette instead of duplicating literal color values here.
            _dayHorizonColor      = _runtimeSky.GetColor(DayHorizonColorId);
            _dawnDuskHorizonColor = _runtimeSky.GetColor(DawnDuskHorizonColorId);
            _nightHorizonColor    = _runtimeSky.GetColor(NightHorizonColorId);
            _dayZenithColor       = _runtimeSky.GetColor(DayZenithColorId);
            _nightZenithColor     = _runtimeSky.GetColor(NightZenithColorId);
        }
        else
        {
            Debug.LogWarning("[SkyDriver] RenderSettings.skybox is unset — run Tools/World Clock/Setup Scene.");
        }

        // DC4 — ambient driven directly from the same time-of-day data instead of skybox-derived ambient,
        // which needs periodic DynamicGI.UpdateEnvironment() calls this project has no other use for.
        RenderSettings.ambientMode = AmbientMode.Flat;

        RenderSettings.fog = true;
        RenderSettings.fogMode = fogMode;
        // Start/end distance set every frame in Update from WorldClock (not here) — WorldClock's static
        // fields already carry safe hardcoded defaults before any sync/DB value arrives, so there's no
        // "unset" window, and this stays correct across the DB-authored value updating on server restart.
    }

    void Update()
    {
        // DC7 — a zone that's opted out just freezes the sky (and fog) where it last was (see SunDriver
        // for the same note); real indoor lighting is a future zone-content concern.
        if (!ZoneClientHelper.CurrentZoneUsesDayNightCycle()) return;

        RenderSettings.fogStartDistance = WorldClock.FogStartDistance;
        RenderSettings.fogEndDistance   = WorldClock.FogEndDistance;

        float f = WorldClock.DayFraction;
        // Smooth day/night blend peaking at noon (f=0.5), troughing at midnight (f=0/1), symmetric
        // through dawn/dusk.
        float dayAmount = Mathf.Clamp01(Mathf.Cos((f - 0.5f) * 2f * Mathf.PI) * 0.5f + 0.5f);
        // Triangle peaking at dayAmount=0.5 (sun near the horizon, i.e. dawn/dusk) and zero at
        // dayAmount=0 or 1 (midnight or noon) — drives the sky's horizon-warmth + sun-glow color so
        // the sunset palette only shows up when the sun is actually low, not all day long.
        float dawnDuskAmount = 1f - Mathf.Abs(dayAmount * 2f - 1f);

        if (_runtimeSky != null)
        {
            _runtimeSky.SetFloat(DayAmountId, dayAmount);
            _runtimeSky.SetFloat(NightAmountId, 1f - dayAmount);
            _runtimeSky.SetFloat(DawnDuskAmountId, dawnDuskAmount);
            _runtimeSky.SetVector(SunDirectionId, SunDriver.CurrentDirection);
            _runtimeSky.SetFloat(StarVisibilityId, StarVisibility(f));

            // Same horizon blend the shader itself computes at dir.y=0 (see StylizedSky.shader's frag) —
            // keeps fog visually seamless with the sky at the point where terrain fully fogs out.
            Color dayHorizonNow = Color.Lerp(_dayHorizonColor, _dawnDuskHorizonColor, dawnDuskAmount);
            RenderSettings.fogColor = Color.Lerp(_nightHorizonColor, dayHorizonNow, dayAmount);

            CurrentZenithColor = Color.Lerp(_nightZenithColor, _dayZenithColor, dayAmount);
        }

        RenderSettings.ambientLight = ambientDayNight.Evaluate(f);
    }

    /// <summary>0 for any DayFraction inside [starHideStart, 1-starHideStart] (the daytime window — default
    /// 0.25 means zero stars for the whole 0.25-0.75 span), ramping to 1 over starFadeWidth outside it.
    /// Computed straight from the raw DayFraction rather than the day/night color curve, which is already
    /// at 50% by the 0.25/0.75 boundary and doesn't give a real "stars off during the day" cutoff.</summary>
    float StarVisibility(float f)
    {
        float hideEnd = 1f - starHideStart;
        float dist = (f < starHideStart) ? starHideStart - f
                   : (f > hideEnd)        ? f - hideEnd
                   : 0f;
        return Mathf.Clamp01(dist / Mathf.Max(starFadeWidth, 0.0001f));
    }

    static Gradient DefaultAmbientGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.05f, 0.05f, 0.12f), 0f),    // midnight
                new GradientColorKey(new Color(0.55f, 0.40f, 0.35f), 0.22f), // dawn
                new GradientColorKey(new Color(0.55f, 0.55f, 0.60f), 0.5f),  // noon
                new GradientColorKey(new Color(0.55f, 0.35f, 0.30f), 0.78f), // dusk
                new GradientColorKey(new Color(0.05f, 0.05f, 0.12f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
