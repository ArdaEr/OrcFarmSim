Shader "OrcFarm/Effects/Jelly Shake"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.34, 0.95, 0.45, 0.62)
        [HDR] _RimColor("Rim Color", Color) = (0.65, 1.25, 0.72, 1.0)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity("Rim Intensity", Range(0.0, 4.0)) = 1.2
        _Smoothness("Specular Smoothness", Range(0.0, 1.0)) = 0.75
        _Alpha("Alpha", Range(0.0, 1.0)) = 0.8

        [Header(Jelly Motion)]
        _BaseY("Base World Y", Range(-5.0, 5.0)) = 0.0
        _JellyHeight("Jelly Height", Range(0.01, 10.0)) = 1.0
        _BaseAnchor("Base Anchor", Range(0.0, 0.8)) = 0.12
        _WobbleStrength("Wobble Strength", Range(0.0, 0.5)) = 0.012
        _WobbleFrequency("Wobble Frequency", Range(0.0, 25.0)) = 3.5
        _WobbleSpeed("Wobble Speed", Range(0.0, 20.0)) = 1.75
        _SecondaryStrength("Secondary Wobble", Range(0.0, 0.5)) = 0.003
        _SquashStrength("Squash Stretch", Range(0.0, 0.3)) = 0.008
        [HideInInspector] _GrabShakeAmount("Grab Shake Amount", Float) = 0.0
        [HideInInspector] _GrabStretchAmount("Grab Stretch Amount", Float) = 0.0
        [HideInInspector] _JellyCenterWorld("Jelly Center World", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _JellyRadiusWorld("Jelly Radius World", Float) = 1.0
        [HideInInspector] _UseRendererBounds("Use Renderer Bounds", Float) = 0.0
        _ShakeDirection("Shake Direction", Vector) = (1.0, 0.0, 0.0, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "Lit"
        }

        LOD 200
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half _Smoothness;
            half _Alpha;
            float _BaseY;
            float _JellyHeight;
            float _BaseAnchor;
            float _WobbleStrength;
            float _WobbleFrequency;
            float _WobbleSpeed;
            float _SecondaryStrength;
            float _SquashStrength;
            float _GrabShakeAmount;
            float _GrabStretchAmount;
            float4 _JellyCenterWorld;
            float _JellyRadiusWorld;
            float _UseRendererBounds;
            float4 _ShakeDirection;
        CBUFFER_END

        static const float MinHeight = 0.001;
        static const float DirectionEpsilon = 0.0001;
        static const float Pi = 3.14159265;

        float2 GetShakeDirectionXZ()
        {
            float2 direction = float2(_ShakeDirection.x, _ShakeDirection.z);
            float lengthSquared = dot(direction, direction);

            if (lengthSquared < DirectionEpsilon)
            {
                return float2(1.0, 0.0);
            }

            return direction * rsqrt(lengthSquared);
        }

        float3 ApplyJellyDeformation(float3 positionOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float2 objectCenterXZ = TransformObjectToWorld(float3(0.0, 0.0, 0.0)).xz;
            float2 centerXZ = lerp(objectCenterXZ, _JellyCenterWorld.xz, saturate(_UseRendererBounds));
            float2 relativeFromCenter = positionWS.xz - centerXZ;
            float height01 = saturate((positionWS.y - _BaseY) / max(_JellyHeight, MinHeight));
            float anchoredHeight = smoothstep(_BaseAnchor, 1.0, height01);
            float time = _Time.y * _WobbleSpeed;
            float wave = sin(time + height01 * _WobbleFrequency);
            float secondaryWave = sin(time * 1.73 + height01 * _WobbleFrequency * 0.67 + positionWS.x * 2.1 + positionWS.z * 1.3);

            float2 direction = GetShakeDirectionXZ();
            float2 perpendicular = float2(-direction.y, direction.x);
            float lateralOffset = (wave * _WobbleStrength + secondaryWave * _SecondaryStrength) * anchoredHeight;

            positionWS.xz += direction * lateralOffset;
            positionWS.xz += direction * _GrabShakeAmount * anchoredHeight;
            positionWS.xz += perpendicular * secondaryWave * _SecondaryStrength * anchoredHeight * 0.5;

            float radius = max(_JellyRadiusWorld, MinHeight);
            float side01 = saturate(dot(relativeFromCenter, direction) / radius * 0.5 + 0.5);
            float sidePullMask = side01 * side01;
            float stretchAmount = _GrabStretchAmount * anchoredHeight * sidePullMask;
            float2 alongDirection = direction * dot(relativeFromCenter, direction);
            positionWS.xz += direction * stretchAmount;
            positionWS.xz += alongDirection * stretchAmount * 0.75;

            float squashWave = sin(time * 0.8 + height01 * Pi);
            float squash = (squashWave * _SquashStrength + abs(_GrabShakeAmount) * 0.35 + abs(_GrabStretchAmount) * 0.18) * anchoredHeight;
            positionWS.y += height01 * squash;
            positionWS.xz = centerXZ + (positionWS.xz - centerXZ) * (1.0 - squash * 0.5);

            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 viewDirectionWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 deformedPositionWS = ApplyJellyDeformation(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = TransformWorldToHClip(deformedPositionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = deformedPositionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirectionWS = GetWorldSpaceNormalizeViewDir(deformedPositionWS);
                output.shadowCoord = TransformWorldToShadowCoord(deformedPositionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half3 ApplyMainLight(half3 color, half3 normalWS, half3 viewDirectionWS, float4 shadowCoord)
            {
                Light mainLight = GetMainLight(shadowCoord);
                half lightAmount = saturate(dot(normalWS, mainLight.direction));
                half3 directLight = color * mainLight.color * lightAmount * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirectionWS);
                half specularPower = lerp(8.0, 96.0, _Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower) * _Smoothness;

                return directLight + mainLight.color * specular * 0.35;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(input.viewDirectionWS);

                half3 ambient = SampleSH(normalWS) * baseSample.rgb;
                half3 lighting = ambient + ApplyMainLight(baseSample.rgb, normalWS, viewDirectionWS, input.shadowCoord);

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _RimPower);
                lighting += _RimColor.rgb * rim * _RimIntensity;

                lighting = MixFog(lighting, input.fogFactor);

                half alpha = saturate(baseSample.a * _Alpha + rim * 0.08);
                return half4(lighting, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = ApplyJellyDeformation(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
