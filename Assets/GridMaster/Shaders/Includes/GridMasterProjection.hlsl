#ifndef GRIDMASTER_PROJECTION_INCLUDED
#define GRIDMASTER_PROJECTION_INCLUDED

#include "GridMasterInput.hlsl"

// One UV set per world axis plane, blended by weights.
// Object UV mode fills all three sets with the mesh UV and uses weights (1, 0, 0).
struct GridUV
{
    float2 uvX; // plane seen along X (uses world Z, Y)
    float2 uvY; // plane seen along Y (uses world X, Z)
    float2 uvZ; // plane seen along Z (uses world X, Y)
    float3 weights;
};

float3 GetTriplanarWeights(float3 normalWS, float sharpness)
{
    float3 weights = pow(abs(normalWS), sharpness);
    return weights / max(weights.x + weights.y + weights.z, 1e-5);
}

GridUV GetGridUV(float3 positionWS, float3 normalWS, float2 meshUV)
{
    GridUV grid;
#if defined(_PROJECTION_OBJECT)
    float2 uv = meshUV * _ObjectUVTiling;
    grid.uvX = uv;
    grid.uvY = uv;
    grid.uvZ = uv;
    grid.weights = float3(1.0, 0.0, 0.0);
#else
    float3 gridPosition = positionWS / max(_CellSize, 1e-4);
    grid.uvX = gridPosition.zy;
    grid.uvY = gridPosition.xz;
    grid.uvZ = gridPosition.xy;
    grid.weights = GetTriplanarWeights(normalWS, _TriplanarSharpness);
#endif
    return grid;
}

#endif
