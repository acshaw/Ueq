// 3.1.9 — flat-shaded low-poly URP terrain shader. Faceted lighting (per-triangle world-space normal via
// screen-space derivatives) + procedural grass/rock/sand color by world height + slope, matte (no gloss).
// Assigned to the ZoneTerrain's materialTemplate by TerrainZoneSetup. Facet size == terrain triangle size,
// so lower the heightmap resolution for chunkier facets. Colors/thresholds are material properties (tunable).
Shader "Ueq/LowPolyTerrain"
{
    Properties
    {
        _GrassTex ("Grass albedo", 2D) = "white" {}
        _RockTex  ("Rock albedo",  2D) = "white" {}
        _SandTex  ("Sand albedo",  2D) = "white" {}
        _TexTiling ("Texture tiling (repeats/world-unit)", Float) = 0.1

        _GrassColor ("Grass tint", Color) = (1, 1, 1, 1)
        _RockColor  ("Rock tint",  Color) = (1, 1, 1, 1)
        _SandColor  ("Sand tint",  Color) = (1, 1, 1, 1)

        _SandMaxY   ("Sand max Y (world)", Float) = -30
        _SandBlend  ("Sand blend",         Float) = 12
        _RockMinY   ("Rock min Y (world)", Float) = 70
        _RockBlend  ("Rock blend",         Float) = 26

        _SlopeRockStart ("Slope rock start (n.y)", Range(0,1)) = 0.92
        _SlopeRockEnd   ("Slope rock end (n.y)",   Range(0,1)) = 0.72
        _AmbientBoost   ("Ambient boost",          Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        // ── Forward lit ──────────────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassColor;
                float4 _RockColor;
                float4 _SandColor;
                float  _TexTiling;
                float  _SandMaxY;
                float  _SandBlend;
                float  _RockMinY;
                float  _RockBlend;
                float  _SlopeRockStart;
                float  _SlopeRockEnd;
                float  _AmbientBoost;
            CBUFFER_END

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_RockTex);  SAMPLER(sampler_RockTex);
            TEXTURE2D(_SandTex);  SAMPLER(sampler_SandTex);

            // Triplanar sample (blend the 3 world-axis projections by the flat normal → no cliff stretching).
            #define TRIPLANAR(tex, wp, bl, t) ( \
                SAMPLE_TEXTURE2D(tex, sampler##tex, (wp).yz * (t)).rgb * (bl).x + \
                SAMPLE_TEXTURE2D(tex, sampler##tex, (wp).xz * (t)).rgb * (bl).y + \
                SAMPLE_TEXTURE2D(tex, sampler##tex, (wp).xy * (t)).rgb * (bl).z )

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float  fogFactor   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.fogFactor   = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Flat, per-triangle world normal from screen-space derivatives.
                float3 dpx = ddx(IN.positionWS);
                float3 dpy = ddy(IN.positionWS);
                float3 n = normalize(cross(dpy, dpx));
                if (n.y < 0) n = -n; // face up

                float worldY = IN.positionWS.y;

                // grass / rock / sand by height + slope (matches the splat logic, done in-shader).
                float rockSlope = saturate((_SlopeRockStart - n.y) / max(1e-4, (_SlopeRockStart - _SlopeRockEnd)));
                float rockHigh  = saturate((worldY - _RockMinY) / max(1e-4, _RockBlend));
                float rock = max(rockSlope, rockHigh);
                float sand = saturate((_SandMaxY - worldY) / max(1e-4, _SandBlend)) * (1.0 - rock);
                float grass = saturate(1.0 - rock - sand);

                // Triplanar texture per material, tinted, blended by the height/slope weights.
                float3 bl = abs(n); bl /= (bl.x + bl.y + bl.z + 1e-4);
                float t = _TexTiling;
                float3 grassA = TRIPLANAR(_GrassTex, IN.positionWS, bl, t) * _GrassColor.rgb;
                float3 rockA  = TRIPLANAR(_RockTex,  IN.positionWS, bl, t) * _RockColor.rgb;
                float3 sandA  = TRIPLANAR(_SandTex,  IN.positionWS, bl, t) * _SandColor.rgb;
                float3 albedo = grass * grassA + rock * rockA + sand * sandA;

                // Simple matte lighting: main light (Lambert) + shadows + SH ambient.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(n, mainLight.direction));
                float3 direct  = albedo * mainLight.color * ndotl * mainLight.shadowAttenuation;
                float3 ambient = albedo * SampleSH(n) * _AmbientBoost;

                float3 color = direct + ambient;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow caster ────────────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionHCS = pos;
                return OUT;
            }

            half4 shadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ── Depth only (URP depth prepass / SSAO) ──────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings depthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
