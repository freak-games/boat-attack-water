#ifndef WATER_INPUT_INCLUDED
#define WATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

half3 _ScatteringColor;
half _BoatAttack_Water_DistanceBlend;
half _BoatAttack_Water_MicroWaveIntensity;
half _WaveHeight;
half _MaxDepth;
half _MaxWaveHeight;

TEXTURE2D(_WaterBufferA);
SAMPLER(sampler_ScreenTextures_linear_clamp);
TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);

///////////////////////////////////////////////////////////////////////////////
//                  				Structs		                             //
///////////////////////////////////////////////////////////////////////////////

struct Attributes // vert struct
{
    float4 positionOS 			    : POSITION;		// vertex positions
	float2	texcoord 				: TEXCOORD0;	// local UVs
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings // fragment struct
{
	float4	uv 						: TEXCOORD0;	// Geometric UVs stored in xy, and world(pre-waves) in zw
	float3	positionWS				: TEXCOORD1;	// world position of the vertices
	half3 	normalWS 				: NORMAL;		// vert normals
	float4 	viewDirectionWS 		: TEXCOORD2;	// view direction
	float3	preWaveSP 				: TEXCOORD3;	// screen position of the verticies before wave distortion
	half2 	fogFactorNoise          : TEXCOORD4;	// x: fogFactor, y: noise

	float4	positionCS				: SV_POSITION;
};

struct VaryingsInfinite // infinite water Varyings
{
	float3	nearPosition			: TEXCOORD0;	// near position of the vertices
    float3	farPosition				: TEXCOORD1;	// far position of the vertices
	float3	positionWS				: TEXCOORD2;	// world position of the vertices
	float4 	viewDirectionWS 		: TEXCOORD3;	// view direction
    half4	screenPosition			: TEXCOORD4;	// screen position after the waves
    float4  positionCS              : SV_POSITION;
};

struct WaterInputData
{
    float3 positionWS;
    half3 normalWS;
    half3 viewDirectionWS;
    float4 detailUV;
    half4 waterBufferA;
    half fogCoord;
    half3 GI;
};

struct WaterLighting
{
    half3 driectLighting;
    half3 ambientLighting;
    half3 sss;
    half3 shadow;
};

#endif // WATER_INPUT_INCLUDED
