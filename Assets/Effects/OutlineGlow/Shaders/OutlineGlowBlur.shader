Shader "Hidden/OutlineGlow/Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineGlowBlur"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // sampler_LinearClamp is declared inside Blit.hlsl — do not redeclare.

            float4 _OutlineGlowBlurDirection; // (offsetX, offsetY, 0, 0) in UV space
            float _OutlineGlowBlurRadius;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 offset = _OutlineGlowBlurDirection.xy * _OutlineGlowBlurRadius;

                // 9-tap Gaussian (sigma ~= 2 px), normalized weights.
                half4 sum = (half4)0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * 4.0) * 0.0162162162;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * 3.0) * 0.0540540541;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * 2.0) * 0.1216216216;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset      ) * 0.1945945946;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv               ) * 0.2270270270;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset      ) * 0.1945945946;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * 2.0) * 0.1216216216;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * 3.0) * 0.0540540541;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * 4.0) * 0.0162162162;

                return sum;
            }
            ENDHLSL
        }
    }
}
