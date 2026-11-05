// StylizedLighting.hlsl  (Phase 4: PBR + toon + rim/matcap hooks)
// Mirrors URP 17.3 (Unity 6.3) UniversalFragmentPBR (ShaderLibrary/Lighting.hlsl) but routes every light through
// EvaluateLightContribution, so main, Forward+ directional and additional lights share one code path.
// Toon lighting lives in ToonLighting; EvaluateLightContribution lerps PBR and toon per light by _ToonStrength.
#ifndef STYLIZED_LIGHTING_INCLUDED
#define STYLIZED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/StylizedLit/Runtime/HLSL/StylizedEffects.hlsl"

// ---------------------------------------------------------------------------------------------
// Toon lighting (Phase 3). Compiled only when _TOON_ON is set (ForwardLit pass only).
// ---------------------------------------------------------------------------------------------
#if defined(_TOON_ON)

// Maps a 0..1 light term (n.l remapped, threshold and shadow applied) to a banded 0..1 value.
half ToonRamp(half x)
{
    x = saturate(x);
#if defined(_TOON_RAMPMAP)
    return SAMPLE_TEXTURE2D_LOD(_ToonRampMap, sampler_ToonRampMap, float2(x, 0.5), 0).r;
#else
    // N bands, evenly spaced output levels 0..1. Each band edge blends over _ToonStepSmoothness of a band.
    half steps = max(2.0h, round(_ToonSteps));
    half scaled = x * steps;
    half band = floor(scaled);
    half t = smoothstep(0.0h, max(_ToonStepSmoothness, 1e-4h), scaled - band);
    half lo = clamp(band - 1.0h, 0.0h, steps - 1.0h);
    half hi = min(band, steps - 1.0h);
    return lerp(lo, hi, t) / (steps - 1.0h);
#endif
}

// Toon radiance of one light. The main light blends from the shadow tint to white across the ramp;
// additional lights ramp from black so they only add light.
half3 ToonLighting(BRDFData brdfData, Light light, InputData inputData, bool specularHighlightsOff, bool isMainLight)
{
    half ndl = dot(inputData.normalWS, light.direction);
    half x = (_ToonHalfLambert > 0.5h) ? (ndl * 0.5h + 0.5h) : ndl;
    x = saturate(x - _ToonThreshold) * light.shadowAttenuation;   // shadows pass through the ramp

    half ramp = ToonRamp(x);
    half3 tint = isMainLight ? lerp(_ToonShadowColor.rgb, half3(1, 1, 1), ramp) : half3(ramp, ramp, ramp);
    half3 radiance = light.color * light.distanceAttenuation;
    half3 color = brdfData.diffuse * radiance * tint;

    if (!specularHighlightsOff && _ToonSpecularStrength > 0.0h)
    {
        half3 halfDir = SafeNormalize(light.direction + inputData.viewDirectionWS);
        half ndh = saturate(dot(inputData.normalWS, halfDir));
        half edge = 1.0h - _ToonSpecularSize;
        half spec = smoothstep(edge, edge + max(_ToonSpecularSmoothness, 1e-4h), ndh);
        spec *= saturate(x * 10.0h);                               // no highlight where the surface is unlit
        color += _ToonSpecularColor.rgb * (_ToonSpecularStrength * spec) * radiance;
    }
    return color;
}

#endif // _TOON_ON

// Radiance contributed by one light (already includes attenuation and shadow).
half3 EvaluateLightContribution(BRDFData brdfData, BRDFData brdfDataClearCoat, Light light,
    InputData inputData, SurfaceData surfaceData, bool specularHighlightsOff, bool isMainLight)
{
#if defined(_TOON_ON)
    if (_ToonStrength > 0.001h && (isMainLight || _ToonAdditionalLights > 0.5h))
    {
        half3 toon = ToonLighting(brdfData, light, inputData, specularHighlightsOff, isMainLight);
        UNITY_BRANCH
        if (_ToonStrength >= 0.999h)
            return toon;

        half3 pbr = LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                            inputData.normalWS, inputData.viewDirectionWS,
                                            surfaceData.clearCoatMask, specularHighlightsOff);
        return lerp(pbr, toon, _ToonStrength);
    }
#endif
    return LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                   inputData.normalWS, inputData.viewDirectionWS,
                                   surfaceData.clearCoatMask, specularHighlightsOff);
}

half4 StylizedFragmentLit(InputData inputData, SurfaceData surfaceData)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = EvaluateLightContribution(brdfData, brdfDataClearCoat, mainLight,
                                                                inputData, surfaceData, specularHighlightsOff, true);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    // Forward+ (cluster loop): non-main directional lights are looped separately from the clustered punctual lights.
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += EvaluateLightContribution(brdfData, brdfDataClearCoat, light,
                                                                            inputData, surfaceData, specularHighlightsOff, false);
        }
    }
    #endif

    // Additional lights. In the cluster loop the count is 0 and LIGHT_LOOP_BEGIN iterates the cluster instead.
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += EvaluateLightContribution(brdfData, brdfDataClearCoat, light,
                                                                            inputData, surfaceData, specularHighlightsOff, false);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

    // Rim is added with the emission term so it is fogged and shown by debug views like any other additive light.
    #if defined(_RIM_ON)
    lightingData.emissionColor += ComputeRim(inputData, mainLight);
    #endif

    half4 finalColor = CalculateFinalColor(lightingData, surfaceData.alpha);

    #if defined(_MATCAP_ON)
    finalColor.rgb = ApplyMatcap(finalColor.rgb, inputData);
    #endif

#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    return min(finalColor, HALF_MAX);
#else
    return finalColor;
#endif
}

#endif // STYLIZED_LIGHTING_INCLUDED
