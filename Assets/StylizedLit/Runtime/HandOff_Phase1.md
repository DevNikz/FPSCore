# HandOff – Phase 1: Lit Parity Core

Shader: `Stylized/Master Lit` · Target: Unity 6.3+ (URP 17.3+), Forward+ only · Authoring: hand-written HLSL + `ShaderGUI`

## 1. Phase summary
- Forward-only port of URP Lit/ComplexLit with the same property names, keywords and pass layout.
- Lighting runs through our own `StylizedFragmentLit`, a copy of `UniversalFragmentPBR`. Every light goes through `EvaluateLightContribution`, the single hook where Phase 3 adds toon.
- Editor GUI subclasses `BaseShaderGUI` and reuses the public `LitGUI` helpers.
- Lit materials can be switched to this shader (shared property names). Switching back also works.
- **Not compiled in Unity yet.** Written without an editor available. First step of Phase 2 is the verification checklist in section 6.

## 2. Files (all under `Assets/StylizedLit/Runtime/`)
| File | Purpose |
|---|---|
| `StylizedLit.shader` | Properties, SubShader tags (`UniversalMaterialType=ComplexLit`), passes: ForwardLit (`UniversalForwardOnly`), ShadowCaster, DepthOnly, DepthNormalsOnly, Meta. FallBack = URP FallbackError. CustomEditor = `StylizedLit.Editor.StylizedLitShaderGUI`. |
| `HLSL/StylizedLitInput.hlsl` | `UnityPerMaterial` CBUFFER (LOCKED layout), DOTS block, textures, `InitializeStylizedSurfaceData`, and the Lit sampling helpers (metallic/spec, occlusion, clear coat, parallax, detail). |
| `HLSL/StylizedCommon.hlsl` | `ApplyStylizedClip(uv)` and `STYLIZED_CLIP_REQUIRES_UV`. Currently alpha clip only. |
| `HLSL/StylizedLighting.hlsl` | `EvaluateLightContribution` and `StylizedFragmentLit` (main + Forward+ directional + additional loops, GI, vertex lights). |
| `HLSL/StylizedLitForwardPass.hlsl` | `StylizedLitPassVertex/Fragment`; Lit's InputData/GI setup unchanged. |
| `HLSL/StylizedLitShadowCasterPass.hlsl` | `StylizedShadowPassVertex/Fragment`. |
| `HLSL/StylizedLitDepthOnlyPass.hlsl` | `StylizedDepthOnlyVertex/Fragment`. |
| `HLSL/StylizedLitDepthNormalsPass.hlsl` | `StylizedDepthNormalsVertex/Fragment` (incl. `_GBUFFER_NORMALS_OCT`, parallax, detail normals). |
| `HLSL/StylizedLitMetaPass.hlsl` | `StylizedFragmentMeta` for lightmap baking (uses package `UniversalVertexMeta`). |
| `StylizedLitShaderGUI.cs` (wrapped in `#if UNITY_EDITOR`) | Surface Options, Surface Inputs, Advanced; keyword validation; legacy upgrade path. |
| `HandOff_Phase1.md` | This file. |

Includes between our files use project-absolute paths (`#include "Assets/StylizedLit/Runtime/HLSL/..."`) because relative includes failed to resolve in Unity. If you move the folder, find-and-replace `Assets/StylizedLit/Runtime/HLSL/` in the `.shader` and `HLSL/*.hlsl` files. URP files are included by package path (package is read-only).

## 3. Properties and keywords added
- Properties: identical to URP Lit (`_BaseMap`, `_BaseColor`, `_Cutoff`, `_Smoothness`, `_Metallic`, `_SpecColor`, `_BumpMap`, `_Parallax`, `_OcclusionMap`, `_EmissionColor`, `_Detail*`, blend/cull/zwrite state, `_ReceiveShadows`, `_QueueOffset`, obsolete legacy props).
- Keywords (same as Lit): `_NORMALMAP`, `_PARALLAXMAP`, `_RECEIVE_SHADOWS_OFF`, `_DETAIL_MULX2/_DETAIL_SCALED`, `_SURFACE_TYPE_TRANSPARENT`, `_ALPHATEST_ON`, `_ALPHAPREMULTIPLY_ON/_ALPHAMODULATE_ON`, `_EMISSION`, `_METALLICSPECGLOSSMAP`, `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`, `_OCCLUSIONMAP`, `_SPECULARHIGHLIGHTS_OFF`, `_ENVIRONMENTREFLECTIONS_OFF`, `_SPECULAR_SETUP`, plus the full URP multi_compile block (Forward+, light layers, cookies, DBuffer, SSAO, probe volumes, lightmaps, fog, LOD crossfade, debug display).
- Extra vs Lit: `_CLEARCOAT/_CLEARCOATMAP` pragma (from ComplexLit). Nothing sets it yet.
- No stylized properties or keywords yet.

## 4. Decisions and deviations from the plan
- **CBUFFER and input code are the full Lit set now** (including detail and clear coat data paths). The SRP Batcher layout must never change per pass, so it was fixed up front instead of in Phase 2.
- **Source of truth:** copied from Unity-Technologies/Graphics `6000.3/staging` (URP 17.3), not from your installed package. Lit.shader is identical in 6000.4, and 6000.5 only adds Meta Quest and application-space-warp pragmas. The first import (6000.0 sources) failed because Unity 6.1+ deprecated `_FORWARD_PLUS`; the shader now uses `_CLUSTER_LIGHT_LOOP`, `USE_CLUSTER_LIGHT_LOOP` and `CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK`. **Not compatible with Unity 6.0.** If your URP differs, diff your installed `Lit.shader`, `LitForwardPass.hlsl` and `Lighting.hlsl` against ours.
- **Also synced from 6.3:** new keywords (`_REFLECTION_PROBE_ATLAS`, `_SCREEN_SPACE_IRRADIANCE`, `LIGHTMAP_BICUBIC_SAMPLING`, `REFLECTION_PROBE_ROTATION`), Fog via `Fog.hlsl` include instead of `multi_compile_fog`, screen-space irradiance GI sample, `uint` rendering-layers output, hidden `_XRMotionVectorsPass` property.
- **Not mirrored from newer branches (6000.5+/master):** screen space reflections, volumetric fog, `_ReceiveFog`, light falloff keyword. Add later if your Unity version has them.
- **`UniversalMaterialType = ComplexLit`** and `LightMode = UniversalForwardOnly`, copied from ComplexLit. This is how forward-only shaders are marked for Deferred renderers.
- **Depth Test:** the GUI only shows it if a `_ZTest` property exists. We don't declare one, so it is not shown (same as Lit). Depth Write is shown.
- **Cast Shadows toggle:** not shown, same as Lit (no `_CastShadows` property declared).
- **`LitDetailGUI` is internal** in URP, so its keyword rule is reproduced in `SetDetailKeywords` (private, in the GUI). The Detail foldout UI is not drawn until Phase 2.
- **Not included yet:** MotionVectors, XRMotionVectors and Universal2D passes (Phase 2). The GBuffer pass is excluded by design.

## 5. Known issues / risks
- Never compiled in Unity. Expect possible small fixes on first import (typos, version drift in URP macros).
- Editor script assumes `Assembly-CSharp-Editor` can see the URP editor assembly (`BaseShaderGUI`, `LitGUI`). If not, add an `.asmdef` in `Editor/` referencing `Unity.RenderPipelines.Universal.Editor` and `Unity.RenderPipelines.Core.Editor`.
- Keyword count is already near Lit's, and stylized features will add more. See the Phase 6 audit.
- Fully custom light loop duplicates ~60 lines of `UniversalFragmentPBR`; re-diff when upgrading URP.

## 6. Verification checklist (do before starting Phase 2)
1. Import. The shader compiles with no errors (check the Console and the shader inspector).
2. Create a material with `Stylized/Master Lit`. All Surface Options and Surface Inputs controls appear.
3. Set Renderer to Forward+. Place 20+ point lights and 2 extra directional lights. Compare side by side with a URP Lit material: no visible difference.
4. Test metallic and specular workflows, normal map, height map, occlusion map, emission (HDR), alpha clip, and transparent surface with all four blend modes.
5. Shadows: main light cascades, additional light shadows, soft shadows, receive shadows off.
6. Enable SSAO and Depth Texture (DepthOnly pass); check `_CameraNormalsTexture` usage.
7. Bake a lightmap with an emissive material (Meta pass).
8. Frame Debugger: SRP Batcher compatible (shader inspector shows "SRP Batcher: compatible").
9. Switch the Renderer to Deferred: the object still renders through the forward-only pass.

## 7. Next phase prerequisites (Phase 2: Full Lit inputs)
- Add `Detail Inputs` foldout (`FillAdditionalFoldouts`) using our own detail GUI.
- Add `_ClearCoat` (toggle) and `_ClearCoatMap` properties and the Clear Coat UI (`LitGUI.DoClearCoat` is public).
- Add MotionVectors, XRMotionVectors and Universal2D passes (copy pragmas from Lit).
- Optional `_ZTest` property for the Depth Test control.
- Verify DBuffer decals and debug display views.
