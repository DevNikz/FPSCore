#ifndef GRIDMASTER_LABELS_INCLUDED
#define GRIDMASTER_LABELS_INCLUDED

#include "GridMasterProjection.hlsl"

// Atlas layout constants. MUST match Tools~/GenerateLabelAtlas.py.
#define GRIDMASTER_LABEL_COLUMNS 8
#define GRIDMASTER_LABEL_ROWS 2
#define GRIDMASTER_LABEL_CELL_TEXELS 64.0
#define GRIDMASTER_LABEL_SDF_SPREAD 8.0
#define GRIDMASTER_LABEL_CHAR_ASPECT 0.6   // character box width / height
// Glyph indices: 0-9 digits, then:
#define GRIDMASTER_GLYPH_MINUS 10
#define GRIDMASTER_GLYPH_M 11
#define GRIDMASTER_GLYPH_C 12
#define GRIDMASTER_GLYPH_U 13

TEXTURE2D(_LabelAtlas);
SAMPLER(sampler_LabelAtlas);

// Label placement on one projection plane. uv is oriented so text reads upright and unmirrored
// (u = right, v = up as seen by a viewer in front of the face). uSign converts u back to the
// world coordinate along that axis.
struct LabelPlane
{
    float2 uv;
    float uSign;
};

LabelPlane GetLabelPlane(GridUV grid, float3 positionWS, float3 normalWS)
{
    LabelPlane plane;
#if defined(_PROJECTION_OBJECT)
    plane.uv = grid.uvX;
    plane.uSign = 1.0;
#else
    float3 gridPosition = positionWS / GetEffectiveCellSize();
    float3 absNormal = abs(normalWS);
    if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
    {
        float faceSign = normalWS.x >= 0.0 ? 1.0 : -1.0;
        plane.uv = float2(faceSign * gridPosition.z, gridPosition.y);
        plane.uSign = faceSign;
    }
    else if (absNormal.y >= absNormal.z)
    {
        float faceSign = normalWS.y >= 0.0 ? 1.0 : -1.0;
        plane.uv = float2(faceSign * gridPosition.x, gridPosition.z);
        plane.uSign = faceSign;
    }
    else
    {
        float faceSign = normalWS.z >= 0.0 ? 1.0 : -1.0;
        plane.uv = float2(-faceSign * gridPosition.x, gridPosition.y);
        plane.uSign = -faceSign;
    }
#endif
    return plane;
}

// Size of one screen pixel on the surface, in minor cells.
float GetPixelSizeInCells(float3 positionWS, float2 planeUV)
{
#if defined(_PROJECTION_OBJECT)
    float2 pixelSize = fwidth(planeUV);
    return max(max(pixelSize.x, pixelSize.y), 1e-6);
#else
    float pixelWorldSize = max(length(ddx(positionWS)), length(ddy(positionWS)));
    return max(pixelWorldSize / GetEffectiveCellSize(), 1e-6);
#endif
}

int IntPow10(int exponent)
{
    int result = 1;
    for (int i = 0; i < exponent; i++)
    {
        result *= 10;
    }
    return result;
}

int GetUnitLength(int unitMode)
{
    // 0 none, 1 "m", 2 "cm", 3 "u"
    return unitMode == 2 ? 2 : (unitMode == 0 ? 0 : 1);
}

// Returns the glyph index for one character slot, or -1 for none.
int GetLabelGlyphIndex(int charIndex, int absValue, bool isNegative, int digitCount, int unitMode)
{
    if (isNegative && charIndex == 0)
    {
        return GRIDMASTER_GLYPH_MINUS;
    }

    int digitIndex = charIndex - (isNegative ? 1 : 0);
    if (digitIndex < digitCount)
    {
        return (absValue / IntPow10(digitCount - 1 - digitIndex)) % 10;
    }

    int unitIndex = digitIndex - digitCount;
    if (unitMode == 1) return unitIndex == 0 ? GRIDMASTER_GLYPH_M : -1;
    if (unitMode == 2) return unitIndex == 0 ? GRIDMASTER_GLYPH_C : (unitIndex == 1 ? GRIDMASTER_GLYPH_M : -1);
    if (unitMode == 3) return unitIndex == 0 ? GRIDMASTER_GLYPH_U : -1;
    return -1;
}

// charUV: 0..1 inside the character box, v up. The atlas image's row 0 is the top row.
float SampleGlyphSDF(int glyphIndex, float2 charUV)
{
    float column = glyphIndex % GRIDMASTER_LABEL_COLUMNS;
    float rowFromTop = floor(glyphIndex / (float)GRIDMASTER_LABEL_COLUMNS);
    float2 atlasUV = float2(
        (column + charUV.x) / GRIDMASTER_LABEL_COLUMNS,
        (GRIDMASTER_LABEL_ROWS - 1.0 - rowFromTop + charUV.y) / GRIDMASTER_LABEL_ROWS);
    return SAMPLE_TEXTURE2D_LOD(_LabelAtlas, sampler_LabelAtlas, atlasUV, 0).r;
}

// Draws the world coordinate of each major line (along the plane's horizontal axis)
// at the lower-left corner of every major cell.
float GetLabelMask(GridUV grid, float3 positionWS, float3 normalWS)
{
    LabelPlane plane = GetLabelPlane(grid, positionWS, normalWS);
    float pixelCell = GetPixelSizeInCells(positionWS, plane.uv);   // derivatives stay outside branches

    float interval = max(_MajorInterval, 1.0);
    float2 majorUV = plane.uv / interval;
    float2 majorIndex = floor(majorUV);
    float2 local = (majorUV - majorIndex) * interval - _LabelOffset.xy;   // minor cells from the label origin

    float labelHeight = max(_LabelSize, 1e-3);
    float charWidth = labelHeight * GRIDMASTER_LABEL_CHAR_ASPECT;

    // Fade out when the text would be only a few pixels tall (unreadable and shimmery).
    float glyphPixelHeight = labelHeight / pixelCell;
    float halfMin = max(_LabelMinPixelHeight * 0.5, 1e-3);
    float visibility = saturate((glyphPixelHeight - halfMin) / halfMin);

    // Value text: world coordinate of the major line, rounded to an integer.
    int unitMode = (int)round(_LabelUnit);
#if defined(_PROJECTION_OBJECT)
    float unitSize = max(_CellSize, 1e-4);   // Object UV mode ignores the global scale
#else
    float unitSize = GetEffectiveCellSize();
#endif
    float worldCoordinate = majorIndex.x * interval * unitSize * plane.uSign;
    float scaledValue = unitMode == 2 ? worldCoordinate * 100.0 : worldCoordinate;
    int value = (int)round(scaledValue);
    bool isNegative = value < 0;
    int absValue = min(abs(value), 99999);
    int digitCount = absValue >= 10000 ? 5 : (absValue >= 1000 ? 4 : (absValue >= 100 ? 3 : (absValue >= 10 ? 2 : 1)));
    int charCount = (isNegative ? 1 : 0) + digitCount + GetUnitLength(unitMode);

    int charIndex = (int)floor(local.x / charWidth);
    bool isInsideBox = local.x >= 0.0 && local.y >= 0.0 && local.y <= labelHeight && charIndex < charCount;
    if (!isInsideBox || visibility <= 0.0)
    {
        return 0.0;
    }

    int glyphIndex = GetLabelGlyphIndex(charIndex, absValue, isNegative, digitCount, unitMode);
    if (glyphIndex < 0)
    {
        return 0.0;
    }

    float2 charUV = float2(frac(local.x / charWidth), local.y / labelHeight);
    float sdf = SampleGlyphSDF(glyphIndex, saturate(charUV));

    // One pixel spans (pixelCell / labelHeight) of the cell height = that many * CELL_TEXELS atlas texels;
    // one texel is 1 / (2 * SPREAD) in SDF units.
    float pixelInSdfUnits = (pixelCell / labelHeight) * GRIDMASTER_LABEL_CELL_TEXELS / (2.0 * GRIDMASTER_LABEL_SDF_SPREAD);
    float edgeHalfWidth = max(0.5 * pixelInSdfUnits * _LabelSoftness, 0.01);
    return smoothstep(0.5 - edgeHalfWidth, 0.5 + edgeHalfWidth, sdf) * visibility;
}

#endif
