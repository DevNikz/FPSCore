#ifndef GRIDMASTER_PATTERN_INCLUDED
#define GRIDMASTER_PATTERN_INCLUDED

#include "GridMasterProjection.hlsl"
#include "GridMasterTint.hlsl"

// Anti-aliased coverage of lines centered on integer uv values.
// width: line width in cell units. softness: scales the AA footprint.
float GetGridLineMask(float2 uv, float width, float softness)
{
    float2 pixelSize = fwidth(uv);
    float2 footprint = max(pixelSize * softness, 1e-5);
    float2 distanceToLine = abs(frac(uv + 0.5) - 0.5);
    float2 coverage = saturate((width * 0.5 - distanceToLine) / footprint + 0.5);

    // Cells near or below ~2 pixels would alias (moire): blend to average line coverage.
    float2 averageBlend = saturate(pixelSize * 2.0 - 1.0);
    coverage = lerp(coverage, saturate(width), averageBlend);

    return coverage.x + coverage.y - coverage.x * coverage.y;
}

// Line mask across the triplanar planes. cellsPerLine = 1 gives minor lines, N gives a major line every N cells.
// width stays in minor-cell units so minor and major thickness are comparable.
float GetBlendedLineMask(GridUV grid, float cellsPerLine, float width, float softness)
{
    float scale = rcp(max(cellsPerLine, 1.0));
    float scaledWidth = width * scale;
#if defined(_PROJECTION_OBJECT)
    return GetGridLineMask(grid.uvX * scale, scaledWidth, softness);
#else
    float maskX = GetGridLineMask(grid.uvX * scale, scaledWidth, softness);
    float maskY = GetGridLineMask(grid.uvY * scale, scaledWidth, softness);
    float maskZ = GetGridLineMask(grid.uvZ * scale, scaledWidth, softness);
    return maskX * grid.weights.x + maskY * grid.weights.y + maskZ * grid.weights.z;
#endif
}

float GetDistanceFade(float3 positionWS, float fadeStart, float fadeEnd)
{
    float cameraDistance = distance(_WorldSpaceCameraPos, positionWS);
    return 1.0 - saturate((cameraDistance - fadeStart) / max(fadeEnd - fadeStart, 1e-4));
}

half3 GetGridAlbedo(GridUV grid, float3 positionWS, float3 normalWS)
{
    // Derivative-based masks stay outside the fade branch.
    float minorMask = GetBlendedLineMask(grid, 1.0, _MinorLineWidth, _LineSoftness);
    float majorMask = GetBlendedLineMask(grid, _MajorInterval, _MajorLineWidth, _LineSoftness);

    float minorFade = 1.0;
    float majorFade = 1.0;
    if (_DistanceFade > 0.5)
    {
        minorFade = GetDistanceFade(positionWS, _FadeStart, _FadeEnd);
        majorFade = GetDistanceFade(positionWS, _FadeStart * _MajorFadeMultiplier, _FadeEnd * _MajorFadeMultiplier);
    }

    half3 color = _BaseColor.rgb;
    color = lerp(color, _LineColor.rgb, (half)(minorMask * minorFade) * _LineColor.a);
    color = lerp(color, _MajorLineColor.rgb, (half)(majorMask * majorFade) * _MajorLineColor.a);
    color = ApplyNormalTint(color, normalWS);
    return color;
}

#endif
