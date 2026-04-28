Shader "Hidden/OutlineGlow/Mask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineGlowMask"

            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Globals — set per-draw via CommandBuffer.SetGlobalColor / SetGlobalFloat.
            float4 _OutlineGlowColor;
            float _OutlineGlowIntensity;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineGlowColor.rgb * _OutlineGlowIntensity, 1.0);
            }
            ENDHLSL
        }
    }
}
