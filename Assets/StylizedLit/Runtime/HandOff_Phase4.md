# HandOff – Phase 4: Rim Light, Matcap, Outline

Shader: `Stylized/Master Lit` · Baseline: Unity 6.3 (URP 17.3), Forward+ only · Builds on `HandOff_Phase1.md` to `HandOff_Phase3.md`

## 1. Phase summary
- **Rim light:** fresnel rim with threshold and softness, optional mask toward the main light's side, optional fade inside the main light's shadow.
- **Matcap:** view-space normal lookup with tint, strength and Additive / Multiply / Replace blend.
- **Outline:** inverted-hull pass with color, width, and a world-space to screen-space width blend.
- Three new foldouts after Toon Lighting: Rim Light, Matcap, Outline.
- Not compiled in Unity yet. Run the checklist in section 6.

## 2. Files (all under `Assets/StylizedLit/Runtime/`)
| File | Change |
|---|---|
| `StylizedLit.shader` | Rim, Matcap and Outline properties; `_RIM_ON` and `_MATCAP_ON` pragmas (ForwardLit only); new `Outline` pass (`LightMode = SRPDefaultUnlit`, `Cull Front`). |
| `HLSL/StylizedLitInput.hlsl` | 12 values appended to the end of `UnityPerMaterial` and the DOTS block (after the toon values); `_MatcapMap` texture. |
| `HLSL/StylizedEffects.hlsl` | NEW. `ComputeRim`, `ApplyMatcap` (compiled out unless the keyword is set). |
| `HLSL/StylizedLighting.hlsl` | Includes `StylizedEffects.hlsl`; rim added to `lightingData.emissionColor`; matcap applied to the final color before fog. |
| `HLSL/StylizedLitOutlinePass.hlsl` | NEW. `StylizedOutlineVertex/Fragment`. |
| `StylizedLitShaderGUI.cs` | Three foldouts (bits `1u << 5` to `1u << 7`), `DrawSection` helper, `SetToggleKeyword`, `SetOutlinePass`. |
| `HandOff_Phase4.md` | This file. |

## 3. Properties and keywords added
- Keywords (`shader_feature_local_fragment`, ForwardLit only): `_RIM_ON`, `_MATCAP_ON`. Set from the toggles `_RimEnabled` and `_MatcapEnabled`.
- Outline has no keyword. `_OutlineEnabled` enables or disables the `SRPDefaultUnlit` pass via `Material.SetShaderPassEnabled`, and the vertex shader also clips the outline when it is 0.
- Rim: `_RimColor` (HDR), `_RimThreshold` 0.6, `_RimSoftness` 0.1, `_RimLightMask` 0, `_RimShadowAware` off.
- Matcap: `_MatcapMap`, `_MatcapTint`, `_MatcapStrength` 1, `_MatcapBlend` (0 add, 1 multiply, 2 replace).
- Outline: `_OutlineColor` black, `_OutlineWidth` 2, `_OutlineDistanceScaling` 0.
- `_OutlineWidth` units: centimeters at scaling 0, pixels at scaling 1 (lerp in between). Orthographic cameras are handled.

## 4. Design notes
- **Rim goes through the emission term** (`lightingData.emissionColor`), so it is fogged, appears with Emission in Rendering Debugger views, and is not multiplied by albedo or shadowed by default.
- **Matcap runs after `CalculateFinalColor`** and before fog (the forward pass applies fog afterwards).
- **Outline reuses `ApplyStylizedClip`**, so alpha-clipped meshes get clipped outlines and Phase 5 dissolve and dither will clip them automatically.
- Outline fragment is unlit flat color plus fog; it is not affected by toon or lighting.
- The outline pass does not cast shadows and is not in the depth or normals prepass.

## 5. Known issues and risks
- **Hard-edge meshes:** inverted hull uses vertex normals, so meshes with split normals show gaps at hard edges. A baked smooth-normal channel (vertex color or UV) is a candidate for Phase 6.
- **Transparent materials:** the outline pass writes depth and uses opaque blending, so transparent surfaces get an opaque outline.
- **Pass toggle default:** a brand-new material has all passes enabled until `ValidateMaterial` runs; the vertex clip guard makes the outline invisible meanwhile.
- **SRPDefaultUnlit:** Forward and Forward+ draw it in the opaque and transparent object passes. Confirm in the Frame Debugger (section 6, item 8). Outlines are not drawn in the depth prepass, so SSAO and depth-based effects don't see them.
- **Matcap default texture is white**, so Additive at strength 1 washes out the object until a matcap is assigned.
- **Keyword count:** ForwardLit gained 2 more keywords (Phase 3 added 2). Counted in the Phase 6 audit.
- Not compiled in Unity. `unity_CameraProjection._m11`, `TransformWorldToViewDir` and the Fog include are standard in 17.3 but unverified on your install.

## 6. Verification checklist (before Phase 5)
1. Foldouts appear in order: Surface Options, Surface Inputs, Detail Inputs, Toon Lighting, Rim Light, Matcap, Outline, Advanced Options. Controls grey out while their toggle is off.
2. Rim: enable on a sphere. Threshold changes the rim thickness; softness 0 is a hard edge; Light Direction Mask 1 confines it to the lit side; Shadow-aware hides it in the main light's shadow.
3. Rim with toon on: still visible and unaffected by toon bands.
4. Matcap: assign a matcap texture. Check Additive, Multiply and Replace; strength 0 is no effect.
5. Outline: enable. A clean outline appears on a smooth mesh; width changes the thickness; color applies; scene fog tints it.
6. Screen Space Width 1: thickness stays about the same pixel size while the camera moves away.
7. Disable outline: the outline disappears and the Frame Debugger shows no `Outline` draw for that material.
8. Frame Debugger (Forward+): the `Outline` pass is drawn in the opaque pass list together with ForwardLit.
9. Alpha-clipped material: the outline follows the clipped shape.
10. Check SRP Batcher compatibility; regression: Phases 1–3 checklists (toon, detail, motion vectors) still pass.

## 7. Next phase prerequisites (Phase 5: Dither, Pixelation, Dissolve)
- Append the new properties to the end of the CBUFFER and DOTS block in `StylizedLitInput.hlsl` (after the outline values).
- Extend `ApplyStylizedClip` and `STYLIZED_CLIP_REQUIRES_UV` in `StylizedCommon.hlsl`. ShadowCaster, DepthOnly, DepthNormals and the Outline pass already call them.
- The forward pass clips inside `InitializeStylizedSurfaceData` (via `Alpha()`); it needs the same dissolve and dither hook.
- Add `ComputeStylizedUV` (pixelation) at every texture sample site in `StylizedLitInput.hlsl`.
- Replace the package `ObjectMotionVectors.hlsl` in the MotionVectors pass with a copy that calls `ApplyStylizedClip`.
- New foldouts use the next free bits (`1u << 8` and up).
