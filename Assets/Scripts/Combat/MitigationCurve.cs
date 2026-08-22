using UnityEngine;

/// <summary>
/// Step 4 (Mitigation, 2026-08-21) — piecewise-linear diminishing-returns curve mapping AC to a
/// mitigation percentage, same mechanism as <see cref="AvoidanceCurve"/>. The design doc's own §5
/// marked this step "NAMED — UNDEFINED" with no numbers to port, unlike Avoidance (doc-given
/// breakpoints) — these are placeholder breakpoints, tuned via the Combat Simulator, same treatment
/// MinAtk/MaxAtk got in 5.1.5. Asymptotes at 50% (well under 100%) per the doc's own requirement that
/// mitigation cannot trivialize outcomes or create invulnerability.
/// </summary>
public static class MitigationCurve
{
    static readonly (float ac, float pct)[] Points =
    {
        (0f,   0f),
        (50f,  10f),
        (100f, 20f),
        (200f, 32f),
        (400f, 42f),
        (800f, 50f),
    };

    public static float Evaluate(float ac)
    {
        if (ac <= Points[0].ac)  return Points[0].pct;
        if (ac >= Points[^1].ac) return Points[^1].pct; // 50% asymptote past AC 800

        for (int i = 0; i < Points.Length - 1; i++)
        {
            var (a0, p0) = Points[i];
            var (a1, p1) = Points[i + 1];
            if (ac >= a0 && ac <= a1)
            {
                float t = (ac - a0) / (a1 - a0);
                return Mathf.Lerp(p0, p1, t);
            }
        }
        return Points[^1].pct;
    }
}
