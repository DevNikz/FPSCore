#ifndef GRIDMASTER_TINT_INCLUDED
#define GRIDMASTER_TINT_INCLUDED

#include "GridMasterProjection.hlsl"

// Tint follows the world-space surface normal, independent of the projection mode.
// _TINT_AXIS: one color per axis (uses the +X/+Y/+Z colors).
// _TINT_FACE: separate color for each of the six face directions.
half3 GetNormalTint(float3 normalWS)
{
    float3 weights = GetTriplanarWeights(normalWS, _TintSharpness);
#if defined(_TINT_FACE)
    half3 tintX = normalWS.x >= 0.0 ? _TintPosX.rgb : _TintNegX.rgb;
    half3 tintY = normalWS.y >= 0.0 ? _TintPosY.rgb : _TintNegY.rgb;
    half3 tintZ = normalWS.z >= 0.0 ? _TintPosZ.rgb : _TintNegZ.rgb;
#else
    half3 tintX = _TintPosX.rgb;
    half3 tintY = _TintPosY.rgb;
    half3 tintZ = _TintPosZ.rgb;
#endif
    return tintX * (half)weights.x + tintY * (half)weights.y + tintZ * (half)weights.z;
}

half3 ApplyNormalTint(half3 color, float3 normalWS)
{
#if defined(_TINT_AXIS) || defined(_TINT_FACE)
    return lerp(color, color * GetNormalTint(normalWS), (half)_TintStrength);
#else
    return color;
#endif
}

#endif
