Shader "Hidden/OutlineGlow/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineGlowComposite"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One // additive: glow adds to camera color

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // _BlitTexture and sampler_LinearClamp are declared inside Blit.hlsl — bound to the blurred mask in this pass.
            TEXTURE2D_X(_OutlineGlowOriginalMask);

            float _OutlineGlowStrength;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 blurred = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 sharp   = SAMPLE_TEXTURE2D_X(_OutlineGlowOriginalMask, sampler_LinearClamp, uv);

                // Outer-glow: present in the blurred mask but not in the original silhouette.
                half3 glow = max(half3(0,0,0), blurred.rgb - sharp.rgb) * _OutlineGlowStrength;

                return half4(glow, 0.0);
            }
            ENDHLSL
        }
    }
}
