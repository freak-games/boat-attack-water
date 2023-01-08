#ifndef WATER_LIGHTING_INCLUDED
#define WATER_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define SHADOW_ITERATIONS 4

float CalculateFresnelTerm(float3 normalWS, float3 viewDirectionWS)
{
    return saturate(pow(1.0 - dot(normalWS, viewDirectionWS), 5)); //fresnel TODO - find a better place
}

///////////////////////////////////////////////////////////////////////////////
//                         Lighting Calculations                             //
///////////////////////////////////////////////////////////////////////////////

//diffuse
float4 VertexLightingAndFog(float3 normalWS, float3 posWS, float3 clipPos)
{
    float3 vertexLight = VertexLighting(posWS, normalWS);
    float fogFactor = ComputeFogFactor(clipPos.z);
    return float4(fogFactor, vertexLight);
}

//specular
float3 Highlights(float3 positionWS, float roughness, float3 normalWS, float3 viewDirectionWS)
{
    Light mainLight = GetMainLight();

    float roughness2 = roughness * roughness;
    float3 floatDir = SafeNormalize(mainLight.direction + viewDirectionWS);
    float NoH = saturate(dot(normalize(normalWS), floatDir));
    float LoH = saturate(dot(mainLight.direction, floatDir));
    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    float d = NoH * NoH * (roughness2 - 1.h) + 1.0001h;
    float LoH2 = LoH * LoH;
    float specularTerm = roughness2 / ((d * d) * max(0.1h, LoH2) * (roughness + 0.5h) * 4);
    // on mobiles (where float actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
    // #if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 5.0); // Prevent FP16 overflow on mobiles
    // #endif
    return specularTerm * mainLight.color * mainLight.distanceAttenuation;
}

///////////////////////////////////////////////////////////////////////////////
//                           Reflection Modes                                //
///////////////////////////////////////////////////////////////////////////////

float3 SampleReflections(float3 normalWS, float3 viewDirectionWS, float2 screenUV, float roughness)
{
    float3 reflectVector = reflect(-viewDirectionWS, normalWS);
    float3 reflection = GlossyEnvironmentReflection(reflectVector, 0, 1);
    return reflection;
}

#endif // WATER_LIGHTING_INCLUDED
