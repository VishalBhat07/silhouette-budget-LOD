Shader "Hidden/Workbench/NativeLodId"
{
    Properties
    {
        _LevelId ("Part and level ID", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _LevelId;
            CBUFFER_END

            float4 Vert(float3 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position);
            }

            float4 Frag() : SV_Target
            {
                return float4(_LevelId, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
