using UnityEditor;
using UnityEngine;

/// <summary>
/// 5.12 (build stage 7) — Tools/World Clock Debug: scrub time-of-day and lunar phase without sitting
/// through a real 45-60 minute cycle. Only meaningful in Play mode (WorldClock's statics are runtime
/// state); the window disables itself otherwise. Overrides are process-local (via WorldClock's static
/// debug fields) — they never touch the network, so they only affect this one peer's own view.
/// </summary>
public class WorldClockDebugWindow : EditorWindow
{
    float _dayFraction = 0.5f;
    float _lunarFraction = 0.5f;

    [MenuItem("Tools/World Clock Debug")]
    public static void Open() => GetWindow<WorldClockDebugWindow>("World Clock Debug");

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode to scrub the world clock.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Day / Night", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Live day fraction: {WorldClock.DayFraction:F3}" +
                                    (WorldClock.DebugOverrideActive ? "  (overridden)" : "  (real time)"));

        EditorGUI.BeginChangeCheck();
        _dayFraction = EditorGUILayout.Slider("Day Fraction", _dayFraction, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) WorldClock.SetDebugDayFraction(_dayFraction);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Dawn"))     { _dayFraction = 0.22f; WorldClock.SetDebugDayFraction(_dayFraction); }
            if (GUILayout.Button("Noon"))     { _dayFraction = 0.5f;  WorldClock.SetDebugDayFraction(_dayFraction); }
            if (GUILayout.Button("Dusk"))     { _dayFraction = 0.78f; WorldClock.SetDebugDayFraction(_dayFraction); }
            if (GUILayout.Button("Midnight")) { _dayFraction = 0f;    WorldClock.SetDebugDayFraction(_dayFraction); }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lunar Phase", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Live lunar fraction: {WorldClock.LunarFraction:F3} " +
                                    $"({WorldClock.CurrentLunarPhase})");

        EditorGUI.BeginChangeCheck();
        _lunarFraction = EditorGUILayout.Slider("Lunar Fraction", _lunarFraction, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) WorldClock.SetDebugLunarFraction(_lunarFraction);

        EditorGUILayout.Space();
        if (GUILayout.Button("Clear Overrides (resume real time)"))
            WorldClock.ClearDebugOverrides();
    }

    void OnInspectorUpdate() => Repaint(); // keep the "live fraction" readout moving
}
