// 5.12 (DC5) — procedural moon, no phase textures. A flat quad shaded by a physically-derived spherical
// lit/shadow term: project the disc as a sphere cap facing the camera, and light it against a sun
// direction that sweeps around the moon as WorldClock.LunarFraction advances. This is a single continuous
// dot-product formula (no branching, no hand-tuned two-circle hack) — correct by construction at every
// cardinal phase: new moon (phase=0) is dark everywhere, full moon (phase=0.5) is lit everywhere, and the
// quarters (phase=0.25/0.75) are cleanly half-lit, with everything in between varying smoothly.
//
// The dark side is ALWAYS FULLY OPAQUE (not alpha-blended into the sky) — an earlier version made it
// mostly transparent so it would "blend with the sky," which turned out to make stars punch through it,
// but stars-in-front-of-the-moon was independently confirmed to predate that change too (i.e. it happened
// even when this shader was already fully opaque) — so alpha was never the root cause, just a second real
// bug layered on top. The "blends with the sky" look now comes from _ShadowColor being set dynamically
// every frame by MoonRig (from SkyDriver.CurrentZenithColor) instead of a fixed dark color, so opacity
// doesn't cost the blended look.
//
// The actual stars-in-front bug: this material lived in the Transparent queue with ZWrite Off, which only
// draws correctly on top of the skybox if the engine's actual per-frame submission order is guaranteed
// Opaque -> Skybox -> Transparent AND nothing later re-touches those pixels. This project's URP renderer
// (PC_Renderer.asset) is configured for Forward+ rendering with an SSAO feature active — a less "textbook"
// pipeline than plain Forward — so that ordering assumption isn't safe to lean on blind. Rather than chase
// the exact pass-order mechanism (needs the Frame Debugger to confirm, not available this session), this
// shader is reclassified as a true opaque alpha-tested object (TransparentCutout/AlphaTest queue, ZWrite
// On) — which it always was in every way except its Tags/Blend state, since it's fully opaque and only
// uses clip() for its circular cutout. An opaque object writes real depth in the same phase as terrain,
// which the skybox is *defined* to respect (a skybox only ever fills gaps left by opaque geometry) — so
// this sidesteps the Transparent-vs-Skybox ordering question entirely instead of depending on it.
Shader "Ueq/ProceduralMoon"
{
    Properties
    {
        _LitColor    ("Lit Color",    Color) = (0.92, 0.92, 0.85, 1)
        _ShadowColor ("Shadow Color (set at runtime by MoonRig; this is only the pre-Play default)", Color) = (0.06, 0.06, 0.10, 1)
        _LunarFraction       ("Lunar Fraction (0=new,0.5=full)", Range(0,1))     = 0.5
        _TerminatorSoftness  ("Terminator Softness",             Range(0.001,0.2)) = 0.03
    }

    SubShader
    {
        // Opaque-with-alpha-cutout classification (not Transparent) — see the class doc comment. Queue
        // "AlphaTest" (2450) still sits comfortably before the Skybox's pass timing, same as "Transparent"
        // did, but now via the Opaque phase's guaranteed-before-Skybox ordering instead of an assumption
        // about Transparent's ordering relative to it.
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Back
        ZWrite On

        Pass
        {
            Name "MoonUnlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LitColor;
                float4 _ShadowColor;
                float  _LunarFraction;
                float  _TerminatorSoftness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p  = IN.uv * 2.0 - 1.0; // quad UV (0..1) -> disc space (-1..1)
                float  r2 = dot(p, p);
                clip(1.0 - r2); // discard outside the disc — quad becomes a circle

                float x = p.x;
                float y = p.y;
                float z = sqrt(saturate(1.0 - r2)); // depth of the visible sphere cap, toward the viewer

                // Sun direction sweeps around the moon as the phase advances: behind at phase=0 (new),
                // in front at phase=0.5 (full). illum = dot(sphereNormal, sunDir); lit where illum > 0.
                float phaseAngle = _LunarFraction * 6.2831853; // TWO_PI
                float illum = x * sin(phaseAngle) - z * cos(phaseAngle);
                float lit = smoothstep(-_TerminatorSoftness, _TerminatorSoftness, illum);

                float3 col = lerp(_ShadowColor.rgb, _LitColor.rgb, lit);
                // Always fully opaque — see the class doc comment for why (stars must not show through).
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
