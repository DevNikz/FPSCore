// StylizedEffects.hlsl  (Phase 4)
// Additive stylized color effects used by the ForwardLit pass only (rim light, matcap).
// Include after StylizedLitInput.hlsl. Both helpers are compiled out unless their keyword is set.
#ifndef STYLIZED_EFFECTS_INCLUDED
#define STYLIZED_EFFECTS_INCLUDED

#if defined(_RIM_ON)
// Fresnel rim with a hard-to-soft edge. Optionally masked toward the side facing the main light and by main light shadows.
half3 ComputeRim(InputData inputData, Light mainLight)
{
    half fresnel = 1.0h - saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half halfSoft = max(_RimSoftness, 1e-4h) * 0.5h;
    half rim = smoothstep(_RimThreshold - halfSoft, _RimThreshold + halfSoft, fresnel);

    half facingLight = saturate(dot(inputData.normalWS, mainLight.direction));
    rim *= lerp(1.0h, facingLight, _RimLightMask);

    if (_RimShadowAware > 0.5h)
        rim *= mainLight.shadowAttenuation;

    return _RimColor.rgb * rim;
}
#endif

#if defined(_MATCAP_ON)
// Matcap lookup from the view-space normal. _MatcapBlend: 0 = add, 1 = multiply, 2 = replace.
half3 ApplyMatcap(half3 color, InputData inputData)
{
    half3 normalVS = TransformWorldToViewDir(inputData.normalWS, true);
    float2 uv = normalVS.xy * 0.5 + 0.5;
    half3 matcap = SAMPLE_TEXTURE2D_LOD(_MatcapMap, sampler_MatcapMap, uv, 0).rgb * _MatcapTint.rgb;

    if (_MatcapBlend < 0.5h)
        return color + matcap * _MatcapStrength;
    if (_MatcapBlend < 1.5h)
        return color * lerp(half3(1, 1, 1), matcap, _MatcapStrength);
    return lerp(color, matcap, _MatcapStrength);
}
#endif

#endif // STYLIZED_EFFECTS_INCLUDED
