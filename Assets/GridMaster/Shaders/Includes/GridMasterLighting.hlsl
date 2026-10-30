#ifndef GRIDMASTER_LIGHTING_INCLUDED
#define GRIDMASTER_LIGHTING_INCLUDED

#include "GridMasterInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

void InitializeGridInputData(float3 positionWS, float3 normalWS, float4 positionCS,
    half fogFactor, half3 vertexLight, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    inputData.shadowCoord = ComputeScreenPos(positionCS);
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), fogFactor);
    inputData.vertexLighting = vertexLight;
    inputData.bakedGI = SampleSH(normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);
}

SurfaceData GetGridSurfaceData(half3 albedo)
{
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = _Metallic;
    surfaceData.smoothness = _Smoothness;
    surfaceData.occlusion = 1.0;
    surfaceData.alpha = 1.0;
    surfaceData.normalTS = half3(0, 0, 1);
    return surfaceData;
}

#endif
