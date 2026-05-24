// ============================================================
// SnowFullscreenEffect.shader
// URP Fullscreen post-process snow effect with stencil masking.
//
// REQUIREMENTS (URP Renderer settings):
//   - Depth Texture:         Enabled
//   - Opaque Texture:        Enabled
//   - Depth Normal Prepass:  Enabled  (for _CameraNormalsTexture)
//
// HOW STENCIL WORKS:
//   Objects rendered with StencilWrite.shader get stencil = 1.
//   This pass uses Comp NotEqual, so stencil=1 pixels are SKIPPED.
//   Those objects show their original color, unaffected by snow.
//
// Compatible with URP 14+ (Unity 2022.2+)
// ============================================================

Shader "Custom/SnowFullscreenEffect"
{
    Properties
    {
        // Set by the Blitter system — do not assign manually
        [HideInInspector] _BlitTexture("Blit Source", 2D) = "white" {}

        // ── Snow parameters ────────────────────────────────
        [Header(Snow Settings)]
        _SnowAmount("Snow Amount",   Range(0, 1)) = 0.0
        [HDR] _SnowColor("Snow Color", Color)    = (1, 1, 1, 1)
        _SnowTexture("Snow Texture", 2D)          = "white" {}
        _SnowScale("Snow Scale",     Float)       = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Snow Fullscreen Pass ──────────────────────────────────────────
        Pass
        {
            Name "SnowFullscreen"

            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  Off

            // ── Stencil: skip pixels written by StencilWrite.shader ──────
            // Objects that wrote Ref=1 will NOT receive snow.
            Stencil
            {
                Ref   1
                Comp  NotEqual   // pass only if stencil != 1
                Pass  Keep
                Fail  Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment SnowFrag

            // Core URP & utility includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ── Snow texture ──────────────────────────────────────────────
            TEXTURE2D(_SnowTexture);
            SAMPLER(sampler_SnowTexture);

            // ── Per-material properties (SRP-Batcher compatible) ──────────
            CBUFFER_START(UnityPerMaterial)
                float  _SnowAmount;
                float4 _SnowColor;
                float  _SnowScale;
            CBUFFER_END

            // ── Helpers ───────────────────────────────────────────────────

            /// Reconstruct world-space position from the depth buffer.
            /// Requires _CameraDepthTexture (Depth Texture: ON in URP settings).
            float3 GetWorldSpacePosition(float2 uv)
            {
                float depth = SampleSceneDepth(uv);

                // Handle reversed Z (DX / Vulkan / Metal)
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            // ── Fragment ──────────────────────────────────────────────────
            half4 SnowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // 1. Current scene colour (copied into _BlitTexture before this pass)
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // 2. World-space normals from the Depth Normal Prepass.
                //    SampleSceneNormals returns world-space normals (URP 12+).
                float3 worldNormal = SampleSceneNormals(uv);

                // 3. World-space position for XZ-tiled snow texture.
                float3 worldPos = GetWorldSpacePosition(uv);

                // 4. ── Normal → Snow mask ─────────────────────────────────
                //    dot(normal, up) = cos(angle).
                //    cos(70°) ≈ 0.342 — the maximum "upward" threshold.
                //    We smooth around that value so the border fades nicely.
                float normalDotUp = dot(normalize(worldNormal), float3(0.0, 1.0, 0.0));

                // Smoothstep edges: [cos(80°)≈0.17, cos(60°)≈0.50]
                // This creates a soft 20° transition band around the 70° cutoff.
                float snowNormalMask = smoothstep(0.17, 0.50, normalDotUp);

                // 5. ── Snow texture (world-space XZ tiling) ───────────────
                float2 snowUV     = worldPos.xz / max(_SnowScale, 0.001);
                half   snowDetail = SAMPLE_TEXTURE2D(_SnowTexture, sampler_SnowTexture, snowUV).r;

                // 6. ── Combine ────────────────────────────────────────────
                //    Snow appears only where:
                //      • normal faces upward  (snowNormalMask)
                //      • overall amount is > 0 (_SnowAmount)
                //      • the texture has brightness (snowDetail)
                float snowBlend = snowNormalMask * _SnowAmount * snowDetail;

                // 7. Final colour: lerp between scene and snow colour
                half3 finalColor = lerp(sceneColor.rgb, _SnowColor.rgb, saturate(snowBlend));

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }

    // ── Fallback ──────────────────────────────────────────────────────────
    FallBack Off
}
