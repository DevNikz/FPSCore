// StylizedLitInput.hlsl  (Phase 4)
// Derived from URP 17.3 (Unity 6.3) LitInput.hlsl. Phases 3-4 appended the Toon, Rim, Matcap and Outline properties.
// The UnityPerMaterial layout below is LOCKED for SRP Batcher: it must be identical in every pass.
// New stylized properties (Phase 3+) are appended to the CBUFFER and to the DOTS block here, never per-pass.
#ifndef STYLIZED_LIT_INPUT_INCLUDED
#define STYLIZED_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
#define _DETAIL
#endif

// NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _DetailAlbedoMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _DetailAlbedoMapScale;
half _DetailNormalMapScale;
half _Surface;
// ---- Stylized (appended; never reorder or remove) ----
half4 _ToonShadowColor;
half4 _ToonSpecularColor;
half _ToonStrength;
half _ToonSteps;
half _ToonStepSmoothness;
half _ToonThreshold;
half _ToonHalfLambert;
half _ToonAdditionalLights;
half _ToonSpecularSize;
half _ToonSpecularSmoothness;
half _ToonSpecularStrength;
half4 _RimColor;
half4 _MatcapTint;
half4 _OutlineColor;
half _RimThreshold;
half _RimSoftness;
half _RimLightMask;
half _RimShadowAware;
half _MatcapStrength;
half _MatcapBlend;
half _OutlineEnabled;
half _OutlineWidth;
half _OutlineDistanceScaling;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// NOTE: Do not ifdef the properties for dots instancing, but ifdef the actual usage.
// Otherwise you might break CPU-side as property constant-buffer offsets change per variant.
// NOTE: Dots instancing is orthogonal to the constant buffer above.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float , _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float , _Parallax)
    UNITY_DOTS_INSTANCED_PROP(float , _OcclusionStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _ClearCoatMask)
    UNITY_DOTS_INSTANCED_PROP(float , _ClearCoatSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _DetailAlbedoMapScale)
    UNITY_DOTS_INSTANCED_PROP(float , _DetailNormalMapScale)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
    UNITY_DOTS_INSTANCED_PROP(float4, _ToonShadowColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _ToonSpecularColor)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonSteps)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonStepSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonThreshold)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonHalfLambert)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonAdditionalLights)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonSpecularSize)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonSpecularSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _ToonSpecularStrength)
    UNITY_DOTS_INSTANCED_PROP(float4, _RimColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _MatcapTint)
    UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
    UNITY_DOTS_INSTANCED_PROP(float , _RimThreshold)
    UNITY_DOTS_INSTANCED_PROP(float , _RimSoftness)
    UNITY_DOTS_INSTANCED_PROP(float , _RimLightMask)
    UNITY_DOTS_INSTANCED_PROP(float , _RimShadowAware)
    UNITY_DOTS_INSTANCED_PROP(float , _MatcapStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _MatcapBlend)
    UNITY_DOTS_INSTANCED_PROP(float , _OutlineEnabled)
    UNITY_DOTS_INSTANCED_PROP(float , _OutlineWidth)
    UNITY_DOTS_INSTANCED_PROP(float , _OutlineDistanceScaling)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Here, we want to avoid overriding a property like e.g. _BaseColor with something like this:
// #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor0)
//
// It would be simpler, but it can cause the compiler to regenerate the property loading code for each use of _BaseColor.
//
// To avoid this, the property loads are cached in some static values at the beginning of the shader.
// The properties such as _BaseColor are then overridden so that it expand directly to the static value like this:
// #define _BaseColor unity_DOTS_Sampled_BaseColor
//
// This simple fix happened to improve GPU performances by ~10% on Meta Quest 2 with URP on some scenes.
static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_BumpScale;
static float  unity_DOTS_Sampled_Parallax;
static float  unity_DOTS_Sampled_OcclusionStrength;
static float  unity_DOTS_Sampled_ClearCoatMask;
static float  unity_DOTS_Sampled_ClearCoatSmoothness;
static float  unity_DOTS_Sampled_DetailAlbedoMapScale;
static float  unity_DOTS_Sampled_DetailNormalMapScale;
static float  unity_DOTS_Sampled_Surface;
static float4 unity_DOTS_Sampled_ToonShadowColor;
static float4 unity_DOTS_Sampled_ToonSpecularColor;
static float  unity_DOTS_Sampled_ToonStrength;
static float  unity_DOTS_Sampled_ToonSteps;
static float  unity_DOTS_Sampled_ToonStepSmoothness;
static float  unity_DOTS_Sampled_ToonThreshold;
static float  unity_DOTS_Sampled_ToonHalfLambert;
static float  unity_DOTS_Sampled_ToonAdditionalLights;
static float  unity_DOTS_Sampled_ToonSpecularSize;
static float  unity_DOTS_Sampled_ToonSpecularSmoothness;
static float  unity_DOTS_Sampled_ToonSpecularStrength;
static float4 unity_DOTS_Sampled_RimColor;
static float4 unity_DOTS_Sampled_MatcapTint;
static float4 unity_DOTS_Sampled_OutlineColor;
static float  unity_DOTS_Sampled_RimThreshold;
static float  unity_DOTS_Sampled_RimSoftness;
static float  unity_DOTS_Sampled_RimLightMask;
static float  unity_DOTS_Sampled_RimShadowAware;
static float  unity_DOTS_Sampled_MatcapStrength;
static float  unity_DOTS_Sampled_MatcapBlend;
static float  unity_DOTS_Sampled_OutlineEnabled;
static float  unity_DOTS_Sampled_OutlineWidth;
static float  unity_DOTS_Sampled_OutlineDistanceScaling;

void SetupDOTSStylizedMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_SpecColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor);
    unity_DOTS_Sampled_EmissionColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_Cutoff               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Smoothness           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
    unity_DOTS_Sampled_Metallic             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_BumpScale            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale);
    unity_DOTS_Sampled_Parallax             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Parallax);
    unity_DOTS_Sampled_OcclusionStrength    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OcclusionStrength);
    unity_DOTS_Sampled_ClearCoatMask        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ClearCoatMask);
    unity_DOTS_Sampled_ClearCoatSmoothness  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ClearCoatSmoothness);
    unity_DOTS_Sampled_DetailAlbedoMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _DetailAlbedoMapScale);
    unity_DOTS_Sampled_DetailNormalMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _DetailNormalMapScale);
    unity_DOTS_Sampled_Surface              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
    unity_DOTS_Sampled_ToonShadowColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ToonShadowColor);
    unity_DOTS_Sampled_ToonSpecularColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ToonSpecularColor);
    unity_DOTS_Sampled_ToonStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonStrength);
    unity_DOTS_Sampled_ToonSteps = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonSteps);
    unity_DOTS_Sampled_ToonStepSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonStepSmoothness);
    unity_DOTS_Sampled_ToonThreshold = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonThreshold);
    unity_DOTS_Sampled_ToonHalfLambert = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonHalfLambert);
    unity_DOTS_Sampled_ToonAdditionalLights = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonAdditionalLights);
    unity_DOTS_Sampled_ToonSpecularSize = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonSpecularSize);
    unity_DOTS_Sampled_ToonSpecularSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonSpecularSmoothness);
    unity_DOTS_Sampled_ToonSpecularStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ToonSpecularStrength);
    unity_DOTS_Sampled_RimColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _RimColor);
    unity_DOTS_Sampled_MatcapTint = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _MatcapTint);
    unity_DOTS_Sampled_OutlineColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OutlineColor);
    unity_DOTS_Sampled_RimThreshold = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _RimThreshold);
    unity_DOTS_Sampled_RimSoftness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _RimSoftness);
    unity_DOTS_Sampled_RimLightMask = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _RimLightMask);
    unity_DOTS_Sampled_RimShadowAware = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _RimShadowAware);
    unity_DOTS_Sampled_MatcapStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _MatcapStrength);
    unity_DOTS_Sampled_MatcapBlend = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _MatcapBlend);
    unity_DOTS_Sampled_OutlineEnabled = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OutlineEnabled);
    unity_DOTS_Sampled_OutlineWidth = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OutlineWidth);
    unity_DOTS_Sampled_OutlineDistanceScaling = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OutlineDistanceScaling);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSStylizedMaterialPropertyCaches()

#define _BaseColor              unity_DOTS_Sampled_BaseColor
#define _SpecColor              unity_DOTS_Sampled_SpecColor
#define _EmissionColor          unity_DOTS_Sampled_EmissionColor
#define _Cutoff                 unity_DOTS_Sampled_Cutoff
#define _Smoothness             unity_DOTS_Sampled_Smoothness
#define _Metallic               unity_DOTS_Sampled_Metallic
#define _BumpScale              unity_DOTS_Sampled_BumpScale
#define _Parallax               unity_DOTS_Sampled_Parallax
#define _OcclusionStrength      unity_DOTS_Sampled_OcclusionStrength
#define _ClearCoatMask          unity_DOTS_Sampled_ClearCoatMask
#define _ClearCoatSmoothness    unity_DOTS_Sampled_ClearCoatSmoothness
#define _DetailAlbedoMapScale   unity_DOTS_Sampled_DetailAlbedoMapScale
#define _DetailNormalMapScale   unity_DOTS_Sampled_DetailNormalMapScale
#define _Surface                unity_DOTS_Sampled_Surface
#define _ToonShadowColor        unity_DOTS_Sampled_ToonShadowColor
#define _ToonSpecularColor      unity_DOTS_Sampled_ToonSpecularColor
#define _ToonStrength           unity_DOTS_Sampled_ToonStrength
#define _ToonSteps              unity_DOTS_Sampled_ToonSteps
#define _ToonStepSmoothness     unity_DOTS_Sampled_ToonStepSmoothness
#define _ToonThreshold          unity_DOTS_Sampled_ToonThreshold
#define _ToonHalfLambert        unity_DOTS_Sampled_ToonHalfLambert
#define _ToonAdditionalLights   unity_DOTS_Sampled_ToonAdditionalLights
#define _ToonSpecularSize       unity_DOTS_Sampled_ToonSpecularSize
#define _ToonSpecularSmoothness unity_DOTS_Sampled_ToonSpecularSmoothness
#define _ToonSpecularStrength   unity_DOTS_Sampled_ToonSpecularStrength
#define _RimColor               unity_DOTS_Sampled_RimColor
#define _MatcapTint             unity_DOTS_Sampled_MatcapTint
#define _OutlineColor           unity_DOTS_Sampled_OutlineColor
#define _RimThreshold           unity_DOTS_Sampled_RimThreshold
#define _RimSoftness            unity_DOTS_Sampled_RimSoftness
#define _RimLightMask           unity_DOTS_Sampled_RimLightMask
#define _RimShadowAware         unity_DOTS_Sampled_RimShadowAware
#define _MatcapStrength         unity_DOTS_Sampled_MatcapStrength
#define _MatcapBlend            unity_DOTS_Sampled_MatcapBlend
#define _OutlineEnabled         unity_DOTS_Sampled_OutlineEnabled
#define _OutlineWidth           unity_DOTS_Sampled_OutlineWidth
#define _OutlineDistanceScaling unity_DOTS_Sampled_OutlineDistanceScaling

#endif

TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailMask);         SAMPLER(sampler_DetailMask);
TEXTURE2D(_ToonRampMap);        SAMPLER(sampler_ToonRampMap); // import with Wrap Mode = Clamp
TEXTURE2D(_MatcapMap);           SAMPLER(sampler_MatcapMap);
TEXTURE2D(_DetailAlbedoMap);    SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_ClearCoatMap);       SAMPLER(sampler_ClearCoatMap);

#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
    #endif
#else // _METALLICSPECGLOSSMAP
    #if _SPECULAR_SETUP
        specGloss.rgb = _SpecColor.rgb;
    #else
        specGloss.rgb = _Metallic.rrr;
    #endif

    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a = _Smoothness;
    #endif
#endif

    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #ifdef _OCCLUSIONMAP
        half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
        return LerpWhiteTo(occ, _OcclusionStrength);
    #else
        return half(1.0);
    #endif
}


// Returns clear coat parameters
// .x/.r == mask
// .y/.g == smoothness
half2 SampleClearCoat(float2 uv)
{
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoatMaskSmoothness = half2(_ClearCoatMask, _ClearCoatSmoothness);

#if defined(_CLEARCOATMAP)
    clearCoatMaskSmoothness *= SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, uv).rg;
#endif

    return clearCoatMaskSmoothness;
#else
    return half2(0.0, 1.0);
#endif  // _CLEARCOAT
}

void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
{
#if defined(_PARALLAXMAP)
    uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);
#endif
}

// Used for scaling detail albedo. Main features:
// - Depending if detailAlbedo brightens or darkens, scale magnifies effect.
// - No effect is applied if detailAlbedo is 0.5.
half3 ScaleDetailAlbedo(half3 detailAlbedo, half scale)
{
    // detailAlbedo = detailAlbedo * 2.0h - 1.0h;
    // detailAlbedo *= _DetailAlbedoMapScale;
    // detailAlbedo = detailAlbedo * 0.5h + 0.5h;
    // return detailAlbedo * 2.0f;

    // A bit more optimized
    return half(2.0) * detailAlbedo * scale - scale + half(1.0);
}

half3 ApplyDetailAlbedo(float2 detailUv, half3 albedo, half detailMask)
{
#if defined(_DETAIL)
    half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUv).rgb;

    // In order to have same performance as builtin, we do scaling only if scale is not 1.0 (Scaled version has 6 additional instructions)
#if defined(_DETAIL_SCALED)
    detailAlbedo = ScaleDetailAlbedo(detailAlbedo, _DetailAlbedoMapScale);
#else
    detailAlbedo = half(2.0) * detailAlbedo;
#endif

    return albedo * LerpWhiteTo(detailAlbedo, detailMask);
#else
    return albedo;
#endif
}

half3 ApplyDetailNormal(float2 detailUv, half3 normalTS, half detailMask)
{
#if defined(_DETAIL)
#if BUMP_SCALE_NOT_SUPPORTED
    half3 detailNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailUv));
#else
    half3 detailNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailUv), _DetailNormalMapScale);
#endif

    // With UNITY_NO_DXT5nm unpacked vector is not normalized for BlendNormalRNM
    // For visual consistancy we going to do in all cases
    detailNormalTS = normalize(detailNormalTS);

    return lerp(normalTS, BlendNormalRNM(normalTS, detailNormalTS), detailMask); // todo: detailMask should lerp the angle of the quaternion rotation, not the normals
#else
    return normalTS;
#endif
}

inline void InitializeStylizedSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

#if _SPECULAR_SETUP
    outSurfaceData.metallic = half(1.0);
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoat = SampleClearCoat(uv);
    outSurfaceData.clearCoatMask       = clearCoat.r;
    outSurfaceData.clearCoatSmoothness = clearCoat.g;
#else
    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
#endif

#if defined(_DETAIL)
    half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
    float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    outSurfaceData.albedo = ApplyDetailAlbedo(detailUv, outSurfaceData.albedo, detailMask);
    outSurfaceData.normalTS = ApplyDetailNormal(detailUv, outSurfaceData.normalTS, detailMask);
#endif
}

#endif // STYLIZED_LIT_INPUT_INCLUDED
