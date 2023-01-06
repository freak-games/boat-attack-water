Shader "Boat Attack/Water"
{
    Properties {}
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite On

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite On

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "WaterCommon.hlsl"

            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}