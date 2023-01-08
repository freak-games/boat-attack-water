#ifndef WATER_COMMON_INCLUDED
#define WATER_COMMON_INCLUDED

#define SHADOWS_SCREEN 0

#include "WaterInput.hlsl"
#include "CommonUtilities.hlsl"
#include "GerstnerWaves.hlsl"
#include "WaterLighting.hlsl"

#define WATER_TIME _Time.y

#define DEPTH_MULTIPLIER 1 / _MaxDepth
#define WaterBufferA(uv) SAMPLE_TEXTURE2D(_WaterBufferA, sampler_ScreenTextures_linear_clamp, float2(uv.x, 1-uv.y))
#define WaterBufferAVert(uv) SAMPLE_TEXTURE2D_LOD(_WaterBufferA, sampler_ScreenTextures_linear_clamp, float2(uv.x, 1-uv.y), 0)

///////////////////////////////////////////////////////////////////////////////
//          	   	      Water shading functions                            //
///////////////////////////////////////////////////////////////////////////////

float3 Scattering(float depth)
{
    const float grad = saturate(exp2(-depth * DEPTH_MULTIPLIER));
    return _ScatteringColor * (1 - grad);
}

float4 DetailUVs(float3 positionWS, float noise)
{
    float4 output = positionWS.xzxz * float4(0.4, 0.4, 0.1, 0.1);
    output.xy -= WATER_TIME * 0.1h * 2 + (noise * 0.2); // small detail
    output.zw += WATER_TIME * 0.05h * 2 + (noise * 0.1); // medium detail
    return output;
}

void DetailNormals(inout float3 normalWS, float4 uvs, float4 waterFX)
{
    float2 detailBump1 = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, uvs.zw * 0.2).xy * 2 - 1;
    float2 detailBump2 = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, uvs.xy*0.2).xy * 2 - 1;
    float2 detailBump = (detailBump1 + detailBump2 * 0.5) * saturate(1000 * 0.25);

    float3 normal1 = float3(detailBump.x, 0, detailBump.y) * _BoatAttack_Water_MicroWaveIntensity;
    float3 normal2 = float3(1 - waterFX.y, 0.5h, 1 - waterFX.z) - 0.5;
    normalWS = normalize(normalWS + normal1 + normal2);
}

Varyings WaveVertexOperations(Varyings input)
{
    input.fogFactorNoise.y = ((noise((input.positionWS.xz * 0.5) + WATER_TIME) + noise(
        (input.positionWS.xz * 1) + WATER_TIME)) * 0.25 - 0.5) + 1;

    input.uv = DetailUVs(input.positionWS, input.fogFactorNoise.y);

    float4 screenUV = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    screenUV.xyz /= screenUV.w;

    WaveStruct wave;
    SampleWaves(input.positionWS, wave);
    input.normalWS = wave.normal;
    input.positionWS += wave.position;

    // After waves
    input.positionCS = TransformWorldToHClip(input.positionWS);
    input.viewDirectionWS.xyz = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);

    // Fog
    input.fogFactorNoise.x = ComputeFogFactor(input.positionCS.z);
    input.preWaveSP = screenUV.xyz; // pre-displaced screenUVs

    // distance blend
    float distanceBlend = saturate(abs(length((_WorldSpaceCameraPos.xz - input.positionWS.xz) * 0.005)) - 0.25);
    input.normalWS = lerp(input.normalWS, float3(0, 1, 0), distanceBlend);

    return input;
}

void InitializeInputData(Varyings input, out WaterInputData inputData, float2 screenUV)
{
    inputData.waterBufferA = WaterBufferA(input.preWaveSP.xy);
    inputData.positionWS = input.positionWS;

    inputData.normalWS = input.normalWS;

    DetailNormals(inputData.normalWS, input.uv, inputData.waterBufferA);

    inputData.viewDirectionWS = input.viewDirectionWS.xyz;

    inputData.detailUV = input.uv;

    inputData.fogCoord = input.fogFactorNoise.x;
    inputData.GI = 0;
}

float3 WaterShading(WaterInputData input, float2 screenUV)
{
    float fresnelTerm = CalculateFresnelTerm(input.normalWS, input.viewDirectionWS);
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, 0);

    float3 GI = SampleSH(input.normalWS) * 1.5;

    BRDFData brdfData;
    float alpha = 1;
    InitializeBRDFData(float3(0, 0, 0), 0, float3(1, 1, 1), 1, alpha, brdfData);
    float3 spec = DirectBDRF(brdfData, input.normalWS, mainLight.direction, input.viewDirectionWS) * mainLight.color * _GlossPower;

    float3 sss = GI;
    sss *= Scattering(1000);

    float3 reflection = SampleReflections(input.normalWS, input.viewDirectionWS, screenUV, 0.0);
    float3 output = lerp(lerp(sss, reflection + spec, fresnelTerm), 0, 0);
    output = MixFog(output, input.fogCoord);
    return output;
}

float WaterNearFade(float3 positionWS)
{
    float3 camPos = GetCameraPositionWS();
    camPos.y = 0;
    return 1 - saturate((distance(positionWS, camPos) - _BoatAttack_Water_DistanceBlend) * 0.01);
}

///////////////////////////////////////////////////////////////////////////////
//               	   Vertex and Fragment functions                         //
///////////////////////////////////////////////////////////////////////////////

// Vertex: Used for Standard non-tessellated water
Varyings WaterVertex(Attributes v)
{
    Varyings o = (Varyings)0;
    o.uv.xy = v.texcoord;
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);

    o = WaveVertexOperations(o);
    return o;
}

// Fragment for water
float4 WaterFragment(Varyings IN) : SV_Target
{
    float4 screenUV = 0.0;
    screenUV.zw = IN.preWaveSP.xy;

    WaterInputData inputData;
    InitializeInputData(IN, inputData, screenUV.xy);

    float4 current;
    current.a = WaterNearFade(IN.positionWS);
    current.rgb = WaterShading(inputData, screenUV.xy);

    return current;
}

#endif // WATER_COMMON_INCLUDED
