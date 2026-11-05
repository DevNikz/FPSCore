#ifndef GTAO_URP_INCLUDED
#define GTAO_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

#define GTAO_PI      3.14159265
#define GTAO_HALF_PI 1.57079633
#define GTAO_R1      0.6180339887

TEXTURE2D_X(_GTAO_History);
TEXTURE2D_X(_GTAO_MotionVectors);

float4 _GTAO_Params;    // x radius, y power, z falloffRange, w unused
float4 _GTAO_Params2;   // x slices, y steps, z maxPixelRadius, w multiBounceAlbedo
float4 _GTAO_Fade;      // x fadeStart, y fadeEnd, z downsample, w frame
float4 _GTAO_Proj;      // P00, P11, P02, P12
float4 _GTAO_ProjOrtho; // P03, P13
float4 _GTAO_Size;      // AO tex w,h,1/w,1/h
float4 _GTAO_FullSize;  // camera w,h,1/w,1/h
float4 _GTAO_Temporal;  // x historyWeight (0=off), y velocityFadeInvPx, z depthTolerance, w clampK
float4 _GTAO_Blur;      // x radius, y depthSharpness
float4x4 _GTAO_ViewToWorld;
float4x4 _GTAO_WorldToView;
float4x4 _GTAO_PrevViewProj;
float4x4 _GTAO_PrevView;

float RawToLinear(float raw)
{
#if defined(_ORTHOGRAPHIC)
    return LinearDepthToEyeDepth(raw);
#else
    return LinearEyeDepth(raw, _ZBufferParams);
#endif
}

bool IsFar(float raw)
{
#if UNITY_REVERSED_Z
    return raw <= 1e-7;
#else
    return raw >= 1.0 - 1e-7;
#endif
}

float LinDepth(float2 uv) { return RawToLinear(SampleSceneDepth(uv)); }

float3 ViewPos(float2 uv, float d)
{
    float2 ndc = uv * 2.0 - 1.0;
#if defined(_ORTHOGRAPHIC)
    return float3((ndc.x - _GTAO_ProjOrtho.x) / _GTAO_Proj.x, (ndc.y - _GTAO_ProjOrtho.y) / _GTAO_Proj.y, -d);
#else
    return float3((ndc.x + _GTAO_Proj.z) * d / _GTAO_Proj.x, (ndc.y + _GTAO_Proj.w) * d / _GTAO_Proj.y, -d);
#endif
}

float3 SamplePos(float2 uv) { return ViewPos(uv, LinDepth(uv)); }

float GTAO_FastACos(float x)
{
    x = clamp(x, -1.0, 1.0);
    float ax = abs(x);
    float r = (-0.156583 * ax + GTAO_HALF_PI) * sqrt(1.0 - ax);
    return x >= 0.0 ? r : GTAO_PI - r;
}

// Jimenez 2014 interleaved gradient noise, animated across 64 frames.
float IGN(float2 p) { return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715)))); }

float2 Noise(int2 p, uint f)
{
    float fr = (float)(f & 63u);
    float n1 = IGN(float2(p) + 5.588238 * fr);
    float n2 = frac(IGN(float2(p.yx) + 17.0) + fr * GTAO_R1);
    return float2(n1, n2);
}

float2 SnapUV(float2 o) { return round(o * _GTAO_FullSize.xy) * _GTAO_FullSize.zw; }

// Normal from depth: closer-neighbour method (atyuwen / Wicked Engine).
float3 ReconstructNormal(float2 uv, float3 P, float d)
{
    float2 t = _GTAO_FullSize.zw;
    float dl = LinDepth(uv - float2(t.x, 0)), dr = LinDepth(uv + float2(t.x, 0));
    float dd = LinDepth(uv - float2(0, t.y)), du = LinDepth(uv + float2(0, t.y));
    float3 dx = abs(dl - d) < abs(dr - d) ? P - ViewPos(uv - float2(t.x, 0), dl) : ViewPos(uv + float2(t.x, 0), dr) - P;
    float3 dy = abs(dd - d) < abs(du - d) ? P - ViewPos(uv - float2(0, t.y), dd) : ViewPos(uv + float2(0, t.y), du) - P;
    float3 n = cross(dx, dy);
    n *= rsqrt(max(dot(n, n), 1e-12));
    return dot(n, -P) < 0.0 ? -n : n;
}

float3 GetNormal(float2 uv, float3 P, float d)
{
#if defined(_SOURCE_DEPTH_NORMALS)
    float3 nw = SampleSceneNormals(uv);
    float l = dot(nw, nw);
    if (l < 0.25) return ReconstructNormal(uv, P, d); // empty normals texture -> fallback
    return mul((float3x3)_GTAO_WorldToView, nw * rsqrt(l));
#else
    return ReconstructNormal(uv, P, d);
#endif
}

void ScreenRadius(float3 P, out float2 uvR, out float pxR)
{
    float R = _GTAO_Params.x;
#if defined(_ORTHOGRAPHIC)
    uvR = 0.5 * R * _GTAO_Proj.xy;
#else
    uvR = 0.5 * R * _GTAO_Proj.xy / max(-P.z, 1e-3);
#endif
    float2 px = uvR * _GTAO_FullSize.xy;
    pxR = max(px.x, px.y);
    float mx = _GTAO_Params2.z;
    if (pxR > mx) { uvR *= mx / pxR; pxR = mx; }
}

// GTAO (Jimenez et al. 2016), following the public XeGTAO reference implementation.
float GTAO(float2 uv, float3 P, float3 N, float2 nz)
{
    float3 V = normalize(-P);
    float2 uvR; float pxR;
    ScreenRadius(P, uvR, pxR);
    if (pxR < 1.3) return 1.0;

    int slices = max((int)_GTAO_Params2.x, 1);
    int steps  = max((int)_GTAO_Params2.y, 1);
    float R = _GTAO_Params.x;
    float fRange = max(_GTAO_Params.z, 0.01) * R;
    float fMul = -1.0 / fRange;
    float fAdd = (R * (1.0 - _GTAO_Params.z)) / fRange + 1.0;
    float minS = 1.3 / pxR;
    float vis = 0.0;

    [loop] for (int sl = 0; sl < slices; sl++)
    {
        float phi = (sl + nz.x) / slices * GTAO_PI;
        float sp, cp; sincos(phi, sp, cp);
        float2 omega = float2(cp, sp) * uvR;
        float3 dir3 = float3(cp, sp, 0);
        float3 ortho = dir3 - dot(dir3, V) * V;
        float3 ax = cross(ortho, V);
        float3 axis = ax * rsqrt(max(dot(ax, ax), 1e-10));
        float3 pn = N - axis * dot(N, axis);
        float pl = length(pn);
        float sgn = sign(dot(ortho, pn));
        float cosN = saturate(dot(pn, V) / max(pl, 1e-4));
        float n = sgn * GTAO_FastACos(cosN);
        float low0 = cos(n + GTAO_HALF_PI), low1 = cos(n - GTAO_HALF_PI);
        float hc0 = low0, hc1 = low1;

        [loop] for (int st = 0; st < steps; st++)
        {
            float sn = frac(nz.y + (float)(sl + st * slices) * GTAO_R1);
            float s = (st + sn) / steps;
            s = s * s + minS; // quadratic distribution, denser near the pixel
            float2 off = SnapUV(s * omega);
            float3 D0 = SamplePos(uv + off) - P;
            float3 D1 = SamplePos(uv - off) - P;
            float l0 = length(D0), l1 = length(D1);
            float w0 = saturate(l0 * fMul + fAdd), w1 = saturate(l1 * fMul + fAdd);
            hc0 = max(hc0, lerp(low0, dot(D0, V) / max(l0, 1e-5), w0));
            hc1 = max(hc1, lerp(low1, dot(D1, V) / max(l1, 1e-5), w1));
        }

        pl = lerp(pl, 1.0, 0.05);
        float h0 = -GTAO_FastACos(hc1), h1 = GTAO_FastACos(hc0);
        h0 = n + clamp(h0 - n, -GTAO_HALF_PI, GTAO_HALF_PI);
        h1 = n + clamp(h1 - n, -GTAO_HALF_PI, GTAO_HALF_PI);
        float ia0 = (cosN + 2.0 * h0 * sin(n) - cos(2.0 * h0 - n)) * 0.25;
        float ia1 = (cosN + 2.0 * h1 * sin(n) - cos(2.0 * h1 - n)) * 0.25;
        vis += pl * (ia0 + ia1);
    }
    return saturate(vis / slices);
}

// Ground-Truth Ambient Occlusion multi-bounce approximation (Jimenez 2016 GDC talk).
float MultiBounce(float x, float albedo)
{
    float a = 2.0404 * albedo - 0.3324;
    float b = -4.7951 * albedo + 0.6417;
    float c = 2.7552 * albedo + 0.6903;
    return max(x, ((x * a + b) * x + c) * x);
}

// Pass 0: estimate AO + store linear eye depth for later reprojection/blur.
float2 FragEstimate(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 uv = i.texcoord;
    int2 ap = (int2)floor(uv * _GTAO_Size.xy);
    uint f = (uint)_GTAO_Fade.w;
    int down = max((int)_GTAO_Fade.z, 1);
    int2 jit = down > 1 ? int2(f & 1u, (f >> 1) & 1u) : int2(0, 0); // temporal sub-pixel jitter when downsampled
    float2 uvC = ((float2)(ap * down + jit) + 0.5) * _GTAO_FullSize.zw;

    float raw = SampleSceneDepth(uvC);
    float d = RawToLinear(raw);
    if (IsFar(raw)) return float2(1.0, d);
    if (_GTAO_Fade.y > 0.0 && d >= _GTAO_Fade.y) return float2(1.0, d); // beyond fade distance: skip

    float3 P = ViewPos(uvC, d);
    float3 N = GetNormal(uvC, P, d);
    float2 nz = Noise(ap, f);

    float ao = GTAO(uvC, P, N, nz);
    ao = pow(saturate(ao), _GTAO_Params.y);
    if (_GTAO_Params2.w > 0.0) ao = MultiBounce(ao, saturate(_GTAO_Params2.w));
    if (_GTAO_Fade.y > 0.0)
    {
        float fade = saturate((_GTAO_Fade.y - d) / max(_GTAO_Fade.y - _GTAO_Fade.x, 1e-3));
        ao = lerp(1.0, ao, fade);
    }
    return float2(ao, d);
}

// Pass 1: temporal resolve. Variance-clamped 3x3 history box, reprojected by motion
// vectors when available, else by camera matrices; fades out on disocclusion/motion.
float2 FragTemporal(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 uv = i.texcoord;
    float2 cur = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).xy;
    if (_GTAO_Temporal.x <= 0.0) return cur; // no history yet: never sample uninitialised texture

    float2 t = _GTAO_Size.zw;
    float m1 = 0, m2 = 0, mn = cur.x, mx = cur.x;
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float a = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(x, y) * t, 0).x;
        m1 += a; m2 += a * a; mn = min(mn, a); mx = max(mx, a);
    }
    float mean = m1 / 9.0;
    float sigma = sqrt(max(m2 / 9.0 - mean * mean, 0.0));
    float lo = max(mn, mean - _GTAO_Temporal.w * sigma);
    float hi = min(mx, mean + _GTAO_Temporal.w * sigma);

    float3 P = ViewPos(uv, cur.y);
    float3 Wp = mul(_GTAO_ViewToWorld, float4(P, 1.0)).xyz;
    float4 pc = mul(_GTAO_PrevViewProj, float4(Wp, 1.0));
    float2 prevUV = pc.xy / max(abs(pc.w), 1e-5) * 0.5 + 0.5;
    float expected = -mul(_GTAO_PrevView, float4(Wp, 1.0)).z;
#if defined(_MOTION_VECTORS)
    prevUV = uv - SAMPLE_TEXTURE2D_X_LOD(_GTAO_MotionVectors, sampler_PointClamp, uv, 0).xy; // URP convention: prev = uv - mv
#endif

    bool ok = all(prevUV > 0.0) && all(prevUV < 1.0);
    float2 h = SAMPLE_TEXTURE2D_X_LOD(_GTAO_History, sampler_LinearClamp, prevUV, 0).xy;
    if (h.x != h.x || h.y != h.y) ok = false; // NaN guard

    float depthErr = abs(h.y - expected) / max(expected, 1e-3);
    float conf = saturate(1.0 - depthErr / max(_GTAO_Temporal.z, 1e-4)); // disocclusion rejection
    float velPx = length((uv - prevUV) * _GTAO_FullSize.xy);
    float velFade = saturate(1.0 - velPx * _GTAO_Temporal.y); // fade history under motion
    float w = ok ? _GTAO_Temporal.x * conf * velFade : 0.0;
    return float2(lerp(cur.x, clamp(h.x, lo, hi), w), cur.y);
}

// Depth-aware separable blur, shared by the H and V+upsample passes.
float2 DoBlur(float2 uv, float2 dir, bool fullResDepth)
{
    int R = (int)_GTAO_Blur.x;
    float2 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).xy;
    float dc = fullResDepth ? LinDepth(uv) : c.y;
    float sg = max(R * 0.5, 0.5);
    float sum = 0.0, wsum = 0.0;
    [loop] for (int k = -R; k <= R; k++)
    {
        float2 s = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + dir * (float)k * _GTAO_Size.zw, 0).xy;
        float w = exp(-(k * k) / (2.0 * sg * sg)) * exp(-abs(s.y - dc) / max(dc, 1e-3) * _GTAO_Blur.y);
        sum += s.x * w; wsum += w;
    }
    return float2(wsum > 1e-5 ? sum / wsum : c.x, c.y);
}

float2 FragBlurH(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    return DoBlur(i.texcoord, float2(1, 0), false);
}

float ResolveAO(Varyings i)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    return DoBlur(i.texcoord, float2(0, 1), true).x; // vertical blur doubles as bilateral upsample
}

float4 FragResolve(Varyings i) : SV_Target   { float a = ResolveAO(i); return float4(a, a, a, a); }
float4 FragComposite(Varyings i) : SV_Target { return float4(0, 0, 0, ResolveAO(i)); } // Blend Zero SrcAlpha
float4 FragDebug(Varyings i) : SV_Target     { float a = ResolveAO(i); return float4(a, a, a, 1); }

#endif
