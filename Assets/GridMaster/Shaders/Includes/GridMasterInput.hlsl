#ifndef GRIDMASTER_INPUT_INCLUDED
#define GRIDMASTER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Every non-texture material property must live here for SRP Batcher compatibility.
CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half _Metallic;
    half _Smoothness;
    float _Projection;
    float _CellSize;
    float _ObjectUVTiling;
    float _TriplanarSharpness;
    float _TintMode;
    float _TintStrength;
    float _TintSharpness;
    half4 _TintPosX;
    half4 _TintNegX;
    half4 _TintPosY;
    half4 _TintNegY;
    half4 _TintPosZ;
    half4 _TintNegZ;
    half4 _LineColor;
    half4 _MajorLineColor;
    float _MinorLineWidth;
    float _MajorLineWidth;
    float _MajorInterval;
    float _LineSoftness;
    float _DistanceFade;
    float _FadeStart;
    float _FadeEnd;
    float _MajorFadeMultiplier;
CBUFFER_END

#endif
