Shader "Hidden/Workbench/ObjectId"
{
    SubShader
    {
        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4x4 _WorkbenchObjectToClip;
            float4 _WorkbenchObjectId;

            float4 Vert(float3 position : POSITION) : SV_POSITION
            {
                return mul(_WorkbenchObjectToClip, float4(position, 1.0));
            }

            float4 Frag() : SV_Target
            {
                return _WorkbenchObjectId;
            }
            ENDHLSL
        }
    }
}
