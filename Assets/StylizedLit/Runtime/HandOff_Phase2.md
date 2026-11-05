# HandOff – Phase 2: Full Lit Inputs

Shader: `Stylized/Master Lit` · Baseline: Unity 6.3 (URP 17.3), Forward+ only · Builds on `HandOff_Phase1.md`

## 1. Phase summary
- Detail Inputs foldout added (mask, detail albedo + scale, detail normal + scale, tiling/offset, scale and sRGB warnings).
- Clear Coat added: the four ComplexLit properties are now declared, so `LitGUI.Inputs` draws the section and `LitGUI.SetMaterialKeywords` drives `_CLEARCOAT` / `_CLEARCOATMAP`.
- `MotionVectors` pass added (TAA, motion blur, STP).
- Per your answers: no `XRMotionVectors`, no `Universal2D`, no `_CastShadows` / `_ZTest`, no parity-check tool.
- Not compiled in Unity yet. Run the checklist in section 6.

## 2. Files changed (all under `Assets/StylizedLit/Runtime/`)
| File | Change |
|---|---|
| `StylizedLit.shader` | `_ClearCoat`, `_ClearCoatMap`, visible `_ClearCoatMask` / `_ClearCoatSmoothness`; removed hidden `_XRMotionVectorsPass`; new `MotionVectors` pass. |
| `StylizedLitShaderGUI.cs` | `FillAdditionalFoldouts`, `DrawDetailInputs`, `DetailStyles`, detail property lookup in `FindProperties`. |
| `HandOff_Phase2.md` | This file. |
No HLSL files changed. The detail and clear coat sampling code was already in `StylizedLitInput.hlsl` from Phase 1.

## 3. Properties and keywords added
- Properties: `_ClearCoat` (toggle), `_ClearCoatMap`, `_ClearCoatMask` (0–1, default 0), `_ClearCoatSmoothness` (0–1, default 1). `_ClearCoatMask` and `_ClearCoatSmoothness` were already in the CBUFFER, so the SRP Batcher layout is unchanged.
- Keywords: `_CLEARCOAT` / `_CLEARCOATMAP` (pragma already in ForwardLit since Phase 1; now settable). New in MotionVectors: `_ALPHATEST_ON`, `LOD_FADE_CROSSFADE`, `_ADD_PRECOMPUTED_VELOCITY`.

## 4. Decisions and deviations
- **Version scope:** baseline is 6000.3. The 6000.4 Lit.shader is identical, and 6000.5 only adds platform-specific Meta Quest pragmas and `APPLICATION_SPACE_WARP_MOTION_TRANSPARENT`, so the shader should work on 6.5+ but this is unverified. Screen space reflections, `_ReceiveFog`, volumetric fog and light falloff (6.5+/master only) are deferred to the Phase 6 audit.
- **`_XRMotionVectorsPass` removed.** `BaseShaderGUI` draws an XR Motion Vectors toggle whenever that property exists, and with no XR pass it would do nothing. Re-add it together with an `XRMotionVectors` pass if XR is needed.
- **No `_ZTest` / `_CastShadows`:** strict Lit parity, so the Depth Test dropdown and Cast Shadows toggle are not shown.
- **Motion vectors use the package `ObjectMotionVectors.hlsl`** with our input. It calls `Alpha()` itself, so dither and dissolve will NOT affect motion vectors until Phase 5 swaps in a copy that calls `ApplyStylizedClip`.
- Detail GUI is a reproduction of the internal `LitDetailGUI` (verified identical between 6000.0 and 6000.3).

## 5. Known issues / risks
- Never compiled in Unity. `RegisterHeaderScope(..., Expandable.Details, ...)` mirrors `LitShader` but uses a protected enum; if it fails to compile, cast to `(uint)Expandable.Details`.
- Clear coat requires `_ClearCoat` > 0 on the material to enable the keyword; unchecked materials create no clear-coat variants (by design, `shader_feature`).

## 6. Verification checklist (before Phase 3)
1. Material inspector shows foldouts in order: Surface Options, Surface Inputs, Detail Inputs, Advanced Options.
2. Detail: assign a detail albedo and normal map with a mask; scale = 1 uses `_DETAIL_MULX2`, any other scale uses `_DETAIL_SCALED` and shows the info box. An sRGB detail albedo shows the warning.
3. Clear Coat: toggle on, set mask and smoothness, assign a clear coat map; compare with a URP ComplexLit material side by side.
4. Motion vectors: enable Motion Vectors / TAA in the renderer; moving objects show no ghosting. Frame Debugger shows the `MotionVectors` pass.
5. Add Precomputed Velocity toggle appears under Advanced Options.
6. Side-by-side against stock Lit's inspector: every option present (except the known Phase 1 and 2 exclusions above).
7. Decals: add a Decal Projector with DBuffer enabled; the decal appears on the material (`_DBUFFER_MRT*` variants).
8. Rendering Debugger material views (Albedo, Metallic, Normal, etc.) render correctly (`DEBUG_DISPLAY`).
9. SRP Batcher still shows "compatible".

## 7. Next phase prerequisites (Phase 3: Toon lighting)
- Append toon properties to the end of the CBUFFER and DOTS block in `StylizedLitInput.hlsl`, never in a pass file.
- Add `_TOON_ON` as `shader_feature_local_fragment` in ForwardLit only.
- Extend `EvaluateLightContribution` in `StylizedLighting.hlsl`: PBR radiance and toon radiance, lerped by `_ToonStrength`.
- Add a "Toon Lighting" foldout through `FillAdditionalFoldouts` and a `SetMaterialKeywords` hook.
