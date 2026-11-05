#ifndef SSGICORE_SAMPLING_INCLUDED
#define SSGICORE_SAMPLING_INCLUDED

// Include from a custom URP shader to read SSGICore's full-resolution GI (indirect radiance, E/pi).
// Multiply by your own albedo for exact results:  color += albedo * SSGICoreSampleGI(screenUV);
// The automatic composite already lights opaques; use this mainly for transparents or custom materials.
// The texture is valid after the SSGICore composite pass (e.g. for transparents).

TEXTURE2D(_SSGICore_GITexture);
SAMPLER(sampler_SSGICore_LinearClamp);

float3 SSGICoreSampleGI(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_SSGICore_GITexture, sampler_SSGICore_LinearClamp, screenUV, 0).rgb;
}

#endif
