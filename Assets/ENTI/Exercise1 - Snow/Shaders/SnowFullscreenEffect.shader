// ============================================================
// SnowFullscreenEffect.shader
Shader "Custom/SnowFullscreenEffect"
{
    Properties
    {
        [HideInInspector] _BlitTexture("Blit Source", 2D) = "white" {}

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

        Pass
        {
            Name "SnowFullscreen"

            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  Off

            Stencil
            {
                Ref   1
                Comp  NotEqual
                Pass  Keep
                Fail  Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment SnowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_SnowTexture);
            SAMPLER(sampler_SnowTexture);

            CBUFFER_START(UnityPerMaterial)
                float  _SnowAmount;
                float4 _SnowColor;
                float  _SnowScale;
            CBUFFER_END

            float3 GetWorldSpacePosition(float2 uv)
            {
                float depth = SampleSceneDepth(uv);

                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            half4 SnowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float3 worldNormal = SampleSceneNormals(uv);

                float3 worldPos = GetWorldSpacePosition(uv);

                float normalDotUp = dot(normalize(worldNormal), float3(0.0, 1.0, 0.0));

                float snowNormalMask = smoothstep(0.17, 0.50, normalDotUp); //Transicion suave 70º

                float2 snowUV     = worldPos.xz / max(_SnowScale, 0.001);
                half   snowDetail = SAMPLE_TEXTURE2D(_SnowTexture, sampler_SnowTexture, snowUV).r;

                //Combinar
                float snowBlend = snowNormalMask * _SnowAmount * snowDetail;

                //Color final
                half3 finalColor = lerp(sceneColor.rgb, _SnowColor.rgb, saturate(snowBlend));

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
