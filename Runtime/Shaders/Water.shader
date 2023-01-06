Shader "Boat Attack/Water"
{
    Properties
    {
        _DitherPattern ("Dithering Pattern", 2D) = "bump" {}
        [Toggle(_STATIC_SHADER)] _Static ("Static", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite On

        Pass
        {
            Name "WaterShading"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            // TODO: Why it's needed?
            #pragma shader_feature _REFLECTION_PROBES
            
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