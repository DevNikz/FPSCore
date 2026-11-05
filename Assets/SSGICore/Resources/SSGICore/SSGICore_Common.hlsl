#ifndef SSGICORE_COMMON_INCLUDED
#define SSGICORE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#define SSGI_TWO_PI 6.28318530718

SamplerState sampler_linear_clamp;

// ---- per-frame constants (set from C#: SSGIFrameConstants.Apply) --------------------------------------
float4x4 _SSGI_View;
float4x4 _SSGI_InvView;
float4x4 _SSGI_PrevView;
float4x4 _SSGI_Proj;
float4x4 _SSGI_InvProj;
float4x4 _SSGI_PrevViewProj;
float4   _SSGI_ZParams;
float4   _SSGI_FullSize;      // w, h, 1/w, 1/h
float4   _SSGI_LowSize;       // w, h, 1/w, 1/h
float4   _SSGI_AmbientCube[6];// +X -X +Y -Y +Z -Z
float _SSGI_Near;
float _SSGI_Thickness;
float _SSGI_MaxDistance;
float _SSGI_EdgeFade;
float _SSGI_PrevWeight;
float _SSGI_FireflyClamp;
float _SSGI_MotionReject;
float _SSGI_FallbackIntensity;
float _SSGI_AlbedoLevel;
float _SSGI_TemporalClamp;
int _SSGI_Downsample;
int _SSGI_RayCount;
int _SSGI_MaxSteps;
int _SSGI_Bounces;
int _SSGI_FrameIndex;
int _SSGI_NormalMode;     // 0 raw, 1 octahedral, 2 reconstruct from depth
int _SSGI_UsePrevColor;
int _SSGI_HistoryValid;
int _SSGI_MaxAge;
int _SSGI_HasMotion;
int _SSGI_StepSize;

// ---- inputs ---------------------------------------------------------------------------------------------
Texture2D<float>  _SSGI_Depth;     // raw device depth
Texture2D<float4> _SSGI_Normals;
Texture2D<float4> _SSGI_Motion;
Texture2D<float4> _SSGI_Color;
Texture2D<float4> _SSGI_PrevColor;
Texture2D<float>  _SSGI_Hzb1;
Texture2D<float>  _SSGI_Hzb2;
Texture2D<float>  _SSGI_Hzb3;
Texture2D<float>  _SSGI_Hzb4;
Texture2D<float>  _SSGI_Hzb5;

// ---- helpers --------------------------------------------------------------------------------------------
float SSGILuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

float3 ClampLuma(float3 c)
{
    float l = SSGILuma(c);
    return l > _SSGI_FireflyClamp ? c * (_SSGI_FireflyClamp / l) : c;
}

float EyeDepthFromRaw(float d)
{
    return 1.0 / (_SSGI_ZParams.z * d + _SSGI_ZParams.w);
}

bool IsSkyRaw(float d)
{
#if UNITY_REVERSED_Z
    return d <= 0.0;
#else
    return d >= 1.0;
#endif
}

float2 PixelToUV(int2 px) { return (px + 0.5) * _SSGI_FullSize.zw; }

int2 LowToFull(int2 lp)
{
    return min(lp * _SSGI_Downsample + _SSGI_Downsample / 2, (int2)_SSGI_FullSize.xy - 1);
}

// View space: camera looks down -Z. Returns the point on the pixel's view ray at the given eye depth.
float3 ViewPosFromPixel(int2 px, float eye)
{
    float2 uv = PixelToUV(px);
    float2 ndc = uv * 2.0 - 1.0;
#if UNITY_UV_STARTS_AT_TOP
    ndc.y = -ndc.y;
#endif
    float4 h = mul(_SSGI_InvProj, float4(ndc, 0.5, 1.0));
    float3 r = h.xyz / h.w;
    return r * (eye / -r.z);
}

float2 ClipToPixel(float4 clip)
{
    float2 ndc = clip.xy / clip.w;
    float2 uv = ndc * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    return uv * _SSGI_FullSize.xy;
}

float3 ViewPosAt(int2 px)
{
    px = clamp(px, int2(0, 0), (int2)_SSGI_FullSize.xy - 1);
    return ViewPosFromPixel(px, EyeDepthFromRaw(_SSGI_Depth.Load(int3(px, 0))));
}

// ---- normals --------------------------------------------------------------------------------------------
float2 SSGIUnpack888ToFloat2(float3 x)
{
    uint3 i = (uint3)(x * 255.0 + 0.5);
    uint hi = i.z >> 4;
    uint lo = i.z & 15;
    uint2 cb = i.xy | uint2(lo << 8, hi << 8);
    return cb / 4095.0;
}

float3 SSGIDecodeOct(float2 f)
{
    float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
    float t = max(-n.z, 0.0);
    n.xy += n.xy >= 0.0 ? -t.xx : t.xx;
    return normalize(n);
}

float3 ReconstructNormalVS(int2 px)
{
    float3 c = ViewPosAt(px);
    float3 l = ViewPosAt(px + int2(-1, 0));
    float3 r = ViewPosAt(px + int2(1, 0));
    float3 u = ViewPosAt(px + int2(0, -1));
    float3 d = ViewPosAt(px + int2(0, 1));
    float3 dx = abs(r.z - c.z) < abs(c.z - l.z) ? r - c : c - l;
    float3 dy = abs(d.z - c.z) < abs(c.z - u.z) ? d - c : c - u;
    float3 n = cross(dx, dy);
    float len = dot(n, n);
    if (len < 1e-12) return float3(0, 0, 1);
    n *= rsqrt(len);
    return dot(n, c) > 0.0 ? -n : n; // face the camera
}

float3 LoadNormalVS(int2 px)
{
    if (_SSGI_NormalMode == 2)
        return ReconstructNormalVS(px);

    float4 packed = _SSGI_Normals.Load(int3(px, 0));
    float3 nWS;
    if (_SSGI_NormalMode == 1)
        nWS = SSGIDecodeOct(SSGIUnpack888ToFloat2(packed.xyz) * 2.0 - 1.0);
    else
        nWS = packed.xyz;

    float len = dot(nWS, nWS);
    if (len < 1e-4) return ReconstructNormalVS(px);
    nWS *= rsqrt(len);
    return normalize(mul((float3x3)_SSGI_View, nWS));
}

float3 ViewToWorldDir(float3 dirVS) { return mul(dirVS, (float3x3)_SSGI_View); }

// ---- reprojection ---------------------------------------------------------------------------------------
float2 AnalyticPrevUV(float3 posVS)
{
    float3 wp = mul(_SSGI_InvView, float4(posVS, 1.0)).xyz;
    float4 c = mul(_SSGI_PrevViewProj, float4(wp, 1.0));
    float2 ndc = c.xy / c.w;
    float2 uv = ndc * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    return uv;
}

float2 PrevUV(int2 px, float3 posVS)
{
    if (_SSGI_HasMotion != 0)
        return PixelToUV(px) - _SSGI_Motion.Load(int3(px, 0)).xy;
    return AnalyticPrevUV(posVS);
}

// ---- noise & sampling -----------------------------------------------------------------------------------
float IGN(float2 p, int frame)
{
    p += 5.588238 * (float)(frame & 63);
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}

float2 Sample2D(int2 px, int seq)
{
    float a = IGN((float2)px, _SSGI_FrameIndex);
    float b = IGN((float2)px + float2(47.0, 13.0), _SSGI_FrameIndex);
    return frac(float2(a, b) + (float)seq * float2(0.7548776662, 0.5698402910));
}

float3 CosineHemisphere(float2 xi, float3 n)
{
    float phi = SSGI_TWO_PI * xi.x;
    float sinT = sqrt(xi.y);
    float cosT = sqrt(1.0 - xi.y);
    float3 h = float3(cos(phi) * sinT, sin(phi) * sinT, cosT);
    float3 up = abs(n.z) < 0.999 ? float3(0, 0, 1) : float3(1, 0, 0);
    float3 t = normalize(cross(up, n));
    float3 b = cross(n, t);
    return normalize(t * h.x + b * h.y + n * h.z);
}

// ---- HZB (min eye-depth pyramid; level 0 = depth buffer) ------------------------------------------------
float LoadHzb(int level, int2 p)
{
    if (level == 0)
    {
        p = clamp(p, int2(0, 0), (int2)_SSGI_FullSize.xy - 1);
        return EyeDepthFromRaw(_SSGI_Depth.Load(int3(p, 0)));
    }
    int2 dims = max(((int2)_SSGI_FullSize.xy + ((1 << level) - 1)) >> level, int2(1, 1));
    p = clamp(p, int2(0, 0), dims - 1);
    if (level == 1) return _SSGI_Hzb1.Load(int3(p, 0));
    if (level == 2) return _SSGI_Hzb2.Load(int3(p, 0));
    if (level == 3) return _SSGI_Hzb3.Load(int3(p, 0));
    if (level == 4) return _SSGI_Hzb4.Load(int3(p, 0));
    return _SSGI_Hzb5.Load(int3(p, 0));
}

// Geometry of a low-res pixel, taken from its representative full-res pixel.
bool LoadLowGeo(int2 lp, out float eye, out float3 posVS, out float3 nVS)
{
    int2 fp = LowToFull(lp);
    float raw = _SSGI_Depth.Load(int3(fp, 0));
    eye = 1e6;
    posVS = 0;
    nVS = float3(0, 0, 1);
    if (IsSkyRaw(raw)) return false;
    eye = EyeDepthFromRaw(raw);
    posVS = ViewPosFromPixel(fp, eye);
    nVS = LoadNormalVS(fp);
    return true;
}

#endif
