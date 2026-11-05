#ifndef GRIDMASTER_PATTERN_INCLUDED
#define GRIDMASTER_PATTERN_INCLUDED

#include "GridMasterProjection.hlsl"
#include "GridMasterTint.hlsl"
#include "GridMasterLabels.hlsl"

#define GRIDMASTER_KIND_LINES 0
#define GRIDMASTER_KIND_DOTS 1
#define GRIDMASTER_KIND_CHECKER 2

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

// Anti-aliased round dots centered on integer uv values (cell corners).
// diameter: dot diameter in cell units.
float GetGridDotMask(float2 uv, float diameter, float softness)
{
    float2 pixelSize = fwidth(uv);
    float maxPixelSize = max(pixelSize.x, pixelSize.y);
    float footprint = max(maxPixelSize * softness, 1e-5);
    float radius = diameter * 0.5;
    float distanceToDot = length(abs(frac(uv + 0.5) - 0.5));
    float coverage = saturate((radius - distanceToDot) / footprint + 0.5);

    // Blend to the dot's average area coverage when cells get tiny.
    float averageBlend = saturate(maxPixelSize * 2.0 - 1.0);
    return lerp(coverage, saturate(PI * radius * radius), averageBlend);
}

// Filtered checker: 0 or 1 per cell, fades smoothly to 0.5 when cells shrink below a pixel.
float GetGridCheckerMask(float2 uv, float softness)
{
    float2 filterWidth = max(abs(ddx(uv)), abs(ddy(uv))) * softness + 1e-4;
    float2 integral = 2.0 * (abs(frac((uv - 0.5 * filterWidth) * 0.5) - 0.5)
        - abs(frac((uv + 0.5 * filterWidth) * 0.5) - 0.5)) / filterWidth;
    return 0.5 - 0.5 * integral.x * integral.y;
}

// Evaluates one pattern kind on one plane. cellsPerFeature = 1 gives one feature per cell,
// N gives one every N cells. size stays in minor-cell units so minor and major are comparable.
float EvaluatePatternPlane(float2 uv, float cellsPerFeature, float size, float softness, int kind)
{
    float scale = rcp(max(cellsPerFeature, 1.0));
    float2 scaledUV = uv * scale;
    float scaledSize = size * scale;

    if (kind == GRIDMASTER_KIND_DOTS)
    {
        return GetGridDotMask(scaledUV, scaledSize, softness);
    }
    if (kind == GRIDMASTER_KIND_CHECKER)
    {
        return GetGridCheckerMask(scaledUV, softness);
    }
    return GetGridLineMask(scaledUV, scaledSize, softness);
}

// Pattern mask across the triplanar planes (a single plane in Object UV mode).
float GetBlendedMask(GridUV grid, float cellsPerFeature, float size, float softness, int kind)
{
#if defined(_PROJECTION_OBJECT)
    return EvaluatePatternPlane(grid.uvX, cellsPerFeature, size, softness, kind);
#else
    float maskX = EvaluatePatternPlane(grid.uvX, cellsPerFeature, size, softness, kind);
    float maskY = EvaluatePatternPlane(grid.uvY, cellsPerFeature, size, softness, kind);
    float maskZ = EvaluatePatternPlane(grid.uvZ, cellsPerFeature, size, softness, kind);
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
    half3 color = _BaseColor.rgb;

    // Derivative-based masks stay outside any dynamic branch.
#if defined(_PATTERN_CHECKER)
    float checkerMask = GetBlendedMask(grid, 1.0, 0.0, _LineSoftness, GRIDMASTER_KIND_CHECKER);
    color = lerp(color, _CheckerColor.rgb, (half)checkerMask * _CheckerColor.a);
#endif

#if defined(_PATTERN_DOTS)
    float minorMask = GetBlendedMask(grid, 1.0, _MinorDotSize, _LineSoftness, GRIDMASTER_KIND_DOTS);
    float majorMask = GetBlendedMask(grid, _MajorInterval, _MajorDotSize, _LineSoftness, GRIDMASTER_KIND_DOTS);
#else
    float minorMask = GetBlendedMask(grid, 1.0, _MinorLineWidth, _LineSoftness, GRIDMASTER_KIND_LINES);
    float majorMask = GetBlendedMask(grid, _MajorInterval, _MajorLineWidth, _LineSoftness, GRIDMASTER_KIND_LINES);
#endif

#if defined(_PATTERN_CHECKER)
    // Lines over a checker can be switched off; the toggle is uniform per material.
    float lineVisibility = _CheckerLines > 0.5 ? 1.0 : 0.0;
    minorMask *= lineVisibility;
    majorMask *= lineVisibility;
#endif

    float minorFade = 1.0;
    float majorFade = 1.0;
    if (_DistanceFade > 0.5)
    {
        minorFade = GetDistanceFade(positionWS, _FadeStart, _FadeEnd);
        majorFade = GetDistanceFade(positionWS, _FadeStart * _MajorFadeMultiplier, _FadeEnd * _MajorFadeMultiplier);
    }

    color = lerp(color, _LineColor.rgb, (half)(minorMask * minorFade) * _LineColor.a);
    color = lerp(color, _MajorLineColor.rgb, (half)(majorMask * majorFade) * _MajorLineColor.a);
    color = ApplyNormalTint(color, normalWS);

#if defined(_LABELS)
    // Labels are drawn after the tint so they keep their own color.
    float labelMask = GetLabelMask(grid, positionWS, normalWS);
    color = lerp(color, _LabelColor.rgb, (half)(labelMask * majorFade) * _LabelColor.a);
#endif
    return color;
}

#endif
