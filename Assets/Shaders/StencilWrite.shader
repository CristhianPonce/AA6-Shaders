// ============================================================
// StencilWrite.shader
// Writes stencil value 1 for objects that should NOT receive snow.
//
// Usage:
//   1. Create a Material using this shader.
//   2. Assign objects to a dedicated Unity Layer (e.g. "NoSnow").
//   3. Set that layer in the SnowRenderFeature's "Stencil Layer" field.
//
// The render feature will draw those objects with this material
// before the snow pass, marking their pixels in the stencil buffer.
// The snow fullscreen pass then skips all stencil=1 pixels.
//
// Notes:
//   - ColorMask 0  : doesn't touch colour output
//   - ZWrite Off   : doesn't modify depth
//   - ZTest LEqual : only marks *visible* (unoccluded) surfaces
// ============================================================

Shader "Custom/StencilWrite"
{
    // No inspector properties needed — this shader only writes stencil.
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry-1"   // render slightly before other opaques
        }

        Pass
        {
            Name "StencilWrite"

            // ── Render state ──────────────────────────────────────────────
            ColorMask 0         // write nothing to the colour buffer
            ZWrite    Off       // write nothing to depth
            ZTest     LEqual    // only process fragments that pass depth test

            // ── Stencil: mark this pixel as "no-snow" ────────────────────
            Stencil
            {
                Ref   1
                Comp  Always    // always write (for every fragment that passes ZTest)
                Pass  Replace   // write Ref (1) into stencil buffer
                ZFail Keep      // don't touch stencil for occluded surfaces
            }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Instancing / stereo boilerplate
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Empty CBUFFER needed for SRP Batcher compatibility
            CBUFFER_START(UnityPerMaterial)
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // Fragment output is irrelevant (ColorMask 0), but must exist.
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
