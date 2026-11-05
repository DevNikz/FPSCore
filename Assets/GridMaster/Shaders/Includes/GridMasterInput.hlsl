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
    float _GlobalScaleMode;
    float _ObjectUVTiling;
    float _TriplanarSharpness;
    float _PatternMode;
    half4 _CheckerColor;
    float _CheckerLines;
    float _MinorDotSize;
    float _MajorDotSize;
    float _LabelsEnabled;
    half4 _LabelColor;
    float _LabelSize;
    float4 _LabelOffset;
    float _LabelUnit;
    float _LabelSoftness;
    float _LabelMinPixelHeight;
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

// Set by the GridMasterGlobals component (Shader.SetGlobalFloat). 0 means no GridMasterGlobals is active.
float _GM_GlobalScale;

// World-space cell size after applying the scene-wide scale.
// Ignore: material value. Multiply: material value * global. Override: global value.
// Falls back to the material value whenever no global scale is active.
float GetEffectiveCellSize()
{
    float cellSize = max(_CellSize, 1e-4);
    if (_GM_GlobalScale <= 0.0)
    {
        return cellSize;
    }

    int mode = (int)round(_GlobalScaleMode);
    if (mode == 1)
    {
        return max(cellSize * _GM_GlobalScale, 1e-4);
    }
    if (mode == 2)
    {
        return max(_GM_GlobalScale, 1e-4);
    }
    return cellSize;
}

#endif
