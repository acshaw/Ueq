// 5.12 (DC3/DC10) — procedural stylized skybox: a flat horizon->zenith gradient blended between day and
// night palettes, driven by SkyDriver from WorldClock.DayFraction. No Physically Based Sky, no clouds/
// atmospheric scattering, no downloaded texture — matches this project's established flat/matte look
// (same posture as Ueq/LowPolyTerrain). DC10 folds a cheap noise-based star field into the same shader
// rather than a separate object: no new assets, faded in only at night and near the zenith.
Shader "Ueq/StylizedSky"
{
    Properties
    {
        // "Day Horizon" is now the NOON horizon — a pale haze, not a fixed sunset color. The actual
        // sunrise/sunset warmth lives in _DawnDuskHorizonColor and only blends in near dawn/dusk via
        // _DawnDuskAmount, so the sky doesn't look perpetually sunset-orange at high noon.
        _DayHorizonColor      ("Noon Horizon Color",      Color) = (0.72, 0.80, 0.88, 1)
        _DawnDuskHorizonColor ("Dawn/Dusk Horizon Color", Color) = (1.00, 0.55, 0.32, 1)
        _DayZenithColor       ("Day Zenith Color",        Color) = (0.30, 0.50, 0.78, 1)
        _NightHorizonColor    ("Night Horizon Color",     Color) = (0.05, 0.05, 0.18, 1)
        _NightZenithColor     ("Night Zenith Color",      Color) = (0.02, 0.02, 0.05, 1)

        _DayAmount      ("Day Amount (0=night,1=day)",              Range(0,1)) = 1
        _NightAmount    ("Night Amount (0=day,1=night)",            Range(0,1)) = 0
        _DawnDuskAmount ("Dawn/Dusk Amount (0=noon/night,1=horizon sun)", Range(0,1)) = 0

        // Soft brightening around the sun's position — set every frame by SkyDriver from the same
        // direction SunDriver rotates the Directional Light toward. This is the single biggest lever
        // for making the sky read as "painted" instead of a flat two-stop gradient.
        _SunDirection      ("Sun Direction (world, set by script)", Vector) = (0, 1, 0, 0)
        _SunGlowExponent   ("Sun Glow Exponent",                    Range(2, 64)) = 12
        _SunGlowIntensity  ("Sun Glow Intensity",                   Range(0, 3))  = 0.8
        _NoonGlowColor     ("Noon Glow Color",                      Color) = (1.00, 0.95, 0.85, 1)
        _DawnDuskGlowColor ("Dawn/Dusk Glow Color",                 Color) = (1.00, 0.55, 0.25, 1)

        _StarDensity    ("Star Density (higher = fewer stars)", Range(0.9, 0.9999)) = 0.998
        _StarBrightness ("Star Brightness",                     Range(0, 5))       = 1.5
        // Computed on the CPU by SkyDriver directly from WorldClock.DayFraction (not derived from
        // _DayAmount/_NightAmount's cosine curve, which is already at 50% by dawn/dusk) — see SkyDriver's
        // starHideStart/starFadeWidth fields for the actual tunable controls.
        _StarVisibility ("Star Visibility (set by script)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DayHorizonColor;
                float4 _DawnDuskHorizonColor;
                float4 _DayZenithColor;
                float4 _NightHorizonColor;
                float4 _NightZenithColor;
                float  _DayAmount;
                float  _NightAmount;
                float  _DawnDuskAmount;
                float4 _SunDirection;
                float  _SunGlowExponent;
                float  _SunGlowIntensity;
                float4 _NoonGlowColor;
                float4 _DawnDuskGlowColor;
                float  _StarDensity;
                float  _StarBrightness;
                float  _StarVisibility;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirOS       : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // The skybox mesh is a unit cube/sphere centered on the camera, so object-space position
                // IS the view direction — no camera-relative math needed.
                OUT.dirOS = IN.positionOS.xyz;
                return OUT;
            }

            // Cheap hash for a pseudo-random star field — no texture, keyed off a quantized view direction.
            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dirOS);

                // Elevation-based gradient, curved instead of linear: a sqrt-like falloff keeps the
                // horizon color to a narrow band close to the horizon and lets the zenith color fill
                // most of the upper sky, the way real atmospheric haze reads (bright/pale low, deep
                // color overhead) rather than a flat two-tone split at the midpoint.
                float h = pow(saturate(dir.y), 0.6);

                // Horizon only warms up toward dawn/dusk orange when the sun is actually near the
                // horizon; at noon it stays a pale haze instead of a fixed sunset band all day.
                float3 dayHorizon = lerp(_DayHorizonColor.rgb, _DawnDuskHorizonColor.rgb, _DawnDuskAmount);
                float3 dayCol     = lerp(dayHorizon, _DayZenithColor.rgb, h);
                float3 nightCol   = lerp(_NightHorizonColor.rgb, _NightZenithColor.rgb, h);
                float3 col        = lerp(nightCol, dayCol, _DayAmount);

                // Looking below the horizon (over a cliff edge, etc.) fades toward a darkened horizon
                // tone instead of continuing the sky gradient downward, which used to read as "the
                // ground glows sky-blue."
                float below = saturate(-dir.y);
                col = lerp(col, lerp(_NightHorizonColor.rgb, dayHorizon, _DayAmount) * 0.5, below);

                // Soft glow around the sun's position — warm near dawn/dusk, pale gold at noon, absent
                // at night (the moon carries its own separate glow via ProceduralMoon).
                float3 sunDir  = normalize(_SunDirection.xyz);
                float  sunDot  = saturate(dot(dir, sunDir));
                float  glow    = pow(sunDot, _SunGlowExponent) * _SunGlowIntensity * _DayAmount;
                float3 glowCol = lerp(_NoonGlowColor.rgb, _DawnDuskGlowColor.rgb, _DawnDuskAmount);
                col += glow * glowCol;

                // DC10 — stars: quantize the view direction into a coarse cell grid, hash each cell to a
                // pseudo-random value, threshold it so only a sparse set of cells are "stars"; _StarVisibility
                // (CPU-computed from raw DayFraction, not this shader's own day/night curve) fully hides them
                // through the daytime window; fade out near the horizon so they don't read as clipping
                // through terrain.
                float3 cell = floor(dir * 300.0);
                float star = step(_StarDensity, Hash3(cell));
                float horizonFade = saturate(dir.y * 4.0);
                col += star * _StarBrightness * _StarVisibility * horizonFade;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
