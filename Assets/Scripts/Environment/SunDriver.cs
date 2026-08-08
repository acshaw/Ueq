using UnityEngine;

/// <summary>
/// 5.12 (DC4) — rotates + colors the scene's sun Directional Light from <see cref="WorldClock.DayFraction"/>.
/// Attach directly to the Directional Light (wired by <c>Tools/World Clock/Setup Scene</c>). Runs on every
/// peer, including a headless server — cheap, and harmless where nothing renders it.
///
/// A Directional Light's effect depends only on its rotation, never its position (see the 5.12 devplan's
/// Grounding section), so this single light correctly illuminates every zone regardless of that zone's
/// world-space offset — no per-zone duplication needed.
/// </summary>
[RequireComponent(typeof(Light))]
public class SunDriver : MonoBehaviour
{
    [SerializeField] Gradient sunColor = DefaultSunColor();
    [SerializeField] AnimationCurve intensityCurve = DefaultIntensityCurve();
    [SerializeField] float maxIntensity = 2f;

    Light _light;
    float _yaw;

    /// <summary>
    /// World-space direction from a viewer toward the sun's position in the sky (opposite the light's
    /// ray-travel direction). Read by <see cref="SkyDriver"/> to aim the skybox's sun glow at the same
    /// spot the Directional Light is actually coming from. Defaults to straight up so a reader before
    /// the first Update (e.g. SkyDriver's own Awake-time material setup) gets a harmless value.
    /// </summary>
    public static Vector3 CurrentDirection { get; private set; } = Vector3.up;

    void Awake()
    {
        _light = GetComponent<Light>();
        _yaw = transform.eulerAngles.y; // preserve whatever facing the light was authored with
    }

    void Update()
    {
        // DC7 — a zone that's opted out of the cycle just freezes the sun where it last was; a real
        // separate indoor lighting scheme is a future zone-content concern (e.g. 7.3), not this devplan's.
        if (!ZoneClientHelper.CurrentZoneUsesDayNightCycle()) return;

        float f = WorldClock.DayFraction;
        // noon (f=0.5) -> 90 (straight down); midnight (f=0/1) -> -90 (straight up, harmless — intensity
        // curve is near-zero there anyway); dawn/dusk (f=0.25/0.75) -> 0 (horizontal).
        float angle = f * 360f - 90f;
        transform.rotation = Quaternion.Euler(angle, _yaw, 0f);

        _light.color     = sunColor.Evaluate(f);
        _light.intensity = intensityCurve.Evaluate(f) * maxIntensity;

        CurrentDirection = -transform.forward;
    }

    static Gradient DefaultSunColor()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.40f, 0.45f, 0.70f), 0f),    // midnight (cool — dimmed by intensity)
                new GradientColorKey(new Color(1.00f, 0.60f, 0.35f), 0.22f), // dawn
                new GradientColorKey(Color.white,                     0.5f), // noon
                new GradientColorKey(new Color(1.00f, 0.50f, 0.30f), 0.78f), // dusk
                new GradientColorKey(new Color(0.40f, 0.45f, 0.70f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static AnimationCurve DefaultIntensityCurve() => new AnimationCurve(
        new Keyframe(0f,   0.05f),
        new Keyframe(0.2f, 0.30f),
        new Keyframe(0.3f, 1.00f),
        new Keyframe(0.5f, 1.00f),
        new Keyframe(0.7f, 1.00f),
        new Keyframe(0.8f, 0.30f),
        new Keyframe(1f,   0.05f));
}
