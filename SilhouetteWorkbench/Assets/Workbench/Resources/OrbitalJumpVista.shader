Shader "Workbench/Orbital Jump Vista"
{
    Properties
    {
        _MainTex ("Destination panorama", 2D) = "black" {}
        _JumpClock ("Flight clock", Float) = 0
        _JumpEnergy ("Crossing energy", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "Vista"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _JumpClock;
                float _JumpEnergy;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 offset = input.uv * 2.0 - 1.0;
                float radius = length(offset);
                float2 radial = offset / max(radius, .001);
                float2 tangent = float2(-radial.y, radial.x);

                // Keep the middle view legible. Near the rim, compress the sky into an arc
                // and stretch the brighter stars along that arc as the ship approaches.
                float rimInfluence = smoothstep(.53, .94, radius);
                float curve = (.08 + .11 * _JumpEnergy) * rimInfluence * rimInfluence;
                float drift = (.04 + .06 * _JumpEnergy) * rimInfluence *
                    sin(_JumpClock * 1.1 + radial.x * 1.8);
                float2 warped = input.uv - radial * curve + tangent * drift;
                half3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped).rgb;
                float smear = rimInfluence * (.012 + .027 * _JumpEnergy);
                half3 echoA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped - tangent * smear).rgb;
                half3 echoB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped + tangent * smear).rgb;
                float sky = smoothstep(.44, .51, warped.y);
                scene += max(max(echoA, echoB) - scene, 0) * (.33 * sky * rimInfluence);

                float rimDistance = (radius - .975) * 39.0;
                float hazeDistance = (radius - .91) * 9.0;
                float rim = exp(-rimDistance * rimDistance);
                float haze = exp(-hazeDistance * hazeDistance);
                float sweepPosition = lerp(-1.35, 1.35, saturate((_JumpClock - 3.3) / 1.28));
                float front = (offset.x + offset.y * .28 - sweepPosition) * 4.2;
                float sweep = exp(-front * front);
                half3 cyan = half3(.15, .50, .86);
                scene += cyan * (rim * (.38 + _JumpEnergy * .40) + haze * .105);
                scene += half3(.35, .70, 1.0) * (sweep * _JumpEnergy * (.11 + .22 * haze));
                return half4(scene, 1);
            }
            ENDHLSL
        }
    }
}
