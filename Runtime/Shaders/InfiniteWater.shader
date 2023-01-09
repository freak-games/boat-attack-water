Shader "Boat Attack/Water/InfiniteWater"
{
    Properties
    {
        _Size ("size", float) = 3.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent-101" "RenderPipeline" = "UniversalPipeline"
        }
        
        ZWrite off
        Cull off

        Pass
        {
            Name "InfiniteWaterShading"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma multi_compile_fog

            #include "WaterCommon.hlsl"
            #include "InfiniteWater.hlsl"

            #pragma vertex InfiniteWaterVertex
            #pragma fragment InfiniteWaterFragment

            struct Output
            {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };

            Varyings InfiniteWaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                output.uv.xy = input.texcoord;

                float3 cameraOffset = GetCameraPositionWS();
                input.positionOS.xz *= _BoatAttack_Water_DistanceBlend; // scale range to blend distance
                input.positionOS.y *= cameraOffset.y - _WaveHeight; // scale height to camera
                input.positionOS.y -= cameraOffset.y - _WaveHeight;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                float3 viewPos = vertexInput.positionVS;
                output.viewDirectionWS.xyz = UNITY_MATRIX_IT_MV[2].xyz;
                output.viewDirectionWS.w = length(viewPos / viewPos.z);

                return output;
            }

            float _Size;

            Output InfiniteWaterFragment(Varyings i)
            {
                float4 screenUV = 0.0;
                screenUV.zw = screenUV.xy; // screen UVs

                InfinitePlane plane = WorldPlane(i.viewDirectionWS, i.positionWS);
                i.positionWS = plane.positionWS;

                Output output;
                i.fogFactorNoise.x = ComputeFogFactor(TransformWorldToHClip(plane.positionWS).z);

                i.normalWS = float3(0.0, 1.0, 0.0);
                i.viewDirectionWS = normalize(GetCameraPositionWS() - i.positionWS).xyzz;
                i.uv = DetailUVs(i.positionWS * (1 / _Size), 1);

                WaterInputData inputData;
                InitializeInputData(i, inputData, screenUV.xy);

                float4 color;
                color.a = 1;
                color.rgb = WaterShading(inputData, screenUV.xy);

                output.color = color;
                output.depth = plane.depth;
                return output;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}