using UnityEngine;

/// <summary>
/// AV1/AV2 — piecewise-linear approximation of the Eventide Dodge curve (design doc §3.3), reused for
/// Parry and Riposte with Dexterity as the input (doc §3.4: same curve shape, values not yet
/// differentiated). Isolated in its own lookup table so the real full per-point Agility 1–250 table
/// (referenced but not included in the design doc) can replace <see cref="Points"/> later with zero
/// changes to <see cref="CombatResolver"/> or any call site.
/// </summary>
public static class AvoidanceCurve
{
    // (stat value, chance %) control points, taken directly from the design doc's 6 summary breakpoints
    // (§3.3) — including the small discontinuities the doc's own numbers show at range boundaries (e.g.
    // 1.0% at Agility 75 jumping to 1.16% at Agility 76). Placeholder pending the full table.
    static readonly (float stat, float pct)[] Points =
    {
        (1f,   0.10f),
        (50f,  0.10f),
        (51f,  0.136f),
        (75f,  1.0f),
        (76f,  1.16f),
        (100f, 5.0f),
        (101f, 5.14f),
        (135f, 10.0f),
        (136f, 10.07f),
        (209f, 14.93f),
        (210f, 15.0f),
    };

    public static float Evaluate(float stat)
    {
        if (stat <= Points[0].stat)  return Points[0].pct;
        if (stat >= Points[^1].stat) return Points[^1].pct; // ~15% asymptote past 210

        for (int i = 0; i < Points.Length - 1; i++)
        {
            var (s0, p0) = Points[i];
            var (s1, p1) = Points[i + 1];
            if (stat >= s0 && stat <= s1)
            {
                float t = (stat - s0) / (s1 - s0);
                return Mathf.Lerp(p0, p1, t);
            }
        }
        return Points[^1].pct;
    }
}
