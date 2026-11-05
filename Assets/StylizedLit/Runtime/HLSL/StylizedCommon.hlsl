// StylizedCommon.hlsl  (Phase 1)
// Shared helpers for all passes. Include AFTER StylizedLitInput.hlsl.
// Extension points for later phases:
//   Phase 5: ApplyStylizedClip gains dither + dissolve; STYLIZED_CLIP_REQUIRES_UV widens.
//   Phase 5: ComputeStylizedUV (pixelation) is added here and used at every texture sample site.
#ifndef STYLIZED_COMMON_INCLUDED
#define STYLIZED_COMMON_INCLUDED

// Passes that clip without full surface data (ShadowCaster, DepthOnly, DepthNormals) need a UV interpolator
// whenever any clip feature is active. Keep the condition in this one place.
#if defined(_ALPHATEST_ON)
    #define STYLIZED_CLIP_REQUIRES_UV 1
#endif

// Discards the fragment for any enabled clip feature. Used by ShadowCaster, DepthOnly and DepthNormals
// so that every depth-like pass agrees with the forward pass.
// Phase 1: alpha clip only. Alpha() performs the clip() when _ALPHATEST_ON is defined.
void ApplyStylizedClip(float2 uv)
{
#if defined(_ALPHATEST_ON)
    Alpha(SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif
}

#endif // STYLIZED_COMMON_INCLUDED
