# HandOff – Phase 3: Toon Lighting

Shader: `Stylized/Master Lit` · Baseline: Unity 6.3 (URP 17.3), Forward+ only · Builds on `HandOff_Phase1.md` and `HandOff_Phase2.md`

## 1. Phase summary
- Toon lighting added as a per-light blend between the PBR result and a banded toon result (`_ToonStrength`: 0 = PBR, 1 = fully toon).
- Controls: procedural steps with edge smoothness, or an optional ramp texture; shadow threshold; half-lambert; shadow tint (main light); toggle for additional lights; stylized specular (color, strength, size, smoothness).
- Shadows pass through the ramp, so shadow edges are banded too.
- "Toon Lighting" foldout added after Detail Inputs.
- Not compiled in Unity yet. Run the checklist in section 6.

## 2. Files changed (all under `Assets/StylizedLit/Runtime/`)
| File | Change |
|---|---|
| `StylizedLit.shader` | Toon properties; `_TOON_ON` and `_TOON_RAMPMAP` pragmas (ForwardLit only). |
| `HLSL/StylizedLitInput.hlsl` | 11 toon values appended to the end of `UnityPerMaterial` and to the DOTS block; `_ToonRampMap` texture. Existing layout untouched. |
| `HLSL/StylizedLighting.hlsl` | `ToonRamp`, `ToonLighting`; `EvaluateLightContribution` gained an `isMainLight` parameter and the PBR/toon lerp. Call sites updated. |
| `StylizedLitShaderGUI.cs` | Toon foldout, `Draw` helper, `ToonStyles`, `SetStylizedKeywords`, `SetToonKeywords`. |
| `HandOff_Phase3.md` | This file. |

## 3. Properties and keywords added
- Keywords (`shader_feature_local_fragment`, ForwardLit pass only): `_TOON_ON`, `_TOON_RAMPMAP`. Set by the GUI from `_ToonEnabled` and from whether `_ToonRampMap` has a texture.
- `_ToonEnabled` (toggle, drives the keyword; not read by HLSL).
- In the CBUFFER and DOTS block: `_ToonShadowColor`, `_ToonSpecularColor`, `_ToonStrength`, `_ToonSteps`, `_ToonStepSmoothness`, `_ToonThreshold`, `_ToonHalfLambert`, `_ToonAdditionalLights`, `_ToonSpecularSize`, `_ToonSpecularSmoothness`, `_ToonSpecularStrength`.
- Texture: `_ToonRampMap` (not scale/offset).
- Defaults: strength 1, steps 3, step smoothness 0.05, threshold 0, half lambert off, shadow tint (0.35, 0.35, 0.45), additional lights on, specular strength 0.5, size 0.12, smoothness 0.02.

## 4. How the toon term works (per light)
1. `x = saturate(remap(N·L) - threshold) * shadowAttenuation` (half-lambert remap optional).
2. `ramp = ToonRamp(x)`: ramp texture sample, or N evenly spaced levels (0..1) with smoothed edges.
3. Main light: `tint = lerp(shadowTint, white, ramp)`. Additional lights: `tint = ramp` (they only add light).
4. `diffuse = brdf.diffuse * lightColor * distanceAttenuation * tint`.
5. Specular: Blinn half-vector smoothstep, masked to lit areas, multiplied by `_ToonSpecularColor` and strength.
6. `EvaluateLightContribution`: strength >= 0.999 skips the PBR evaluation; otherwise `lerp(pbr, toon, strength)`. Additional lights with the toggle off stay PBR.

## 5. Decisions, limits and risks
- **GI is not toon-shaded.** Ambient, lightmaps and reflection probes still use the PBR path, so shadow color on the main light is the only "shadow" tint.
- **Clear coat is ignored by the toon term.** It is only visible through the PBR share when strength < 1.
- **Metallic surfaces:** toon diffuse uses `brdf.diffuse`, which is near black for metals, and the toon highlight is the white specular color rather than a tinted one. Metals rely on environment reflections.
- **Per-vertex additional lights** (`_ADDITIONAL_LIGHTS_VERTEX`) stay PBR.
- **Ramp texture:** import with Wrap Mode = Clamp (the GUI warns otherwise); the ramp is read from the red channel only.
- **Cost:** with strength strictly between 0 and 1, both PBR and toon are evaluated per light. At 1, PBR is skipped; at 0 the toon code is skipped, but the variant is still compiled, so for pure PBR leave `Enable Toon Lighting` off.
- **Keyword count:** ForwardLit gained 2 keywords. Counted against the 64 local keyword limit in the Phase 6 audit.
- Not compiled in Unity. `ToonRamp` uses `SAMPLE_TEXTURE2D_LOD`; if it fails to compile on your target, switch to `SAMPLE_TEXTURE2D` with a `LOD` pragma.

## 6. Verification checklist (before Phase 4)
1. The Toon Lighting foldout appears after Detail Inputs; the controls grey out when toon is disabled.
2. Enable toon with the default settings on a sphere with one directional light: clear 3-band shading, blue-gray shadow tint.
3. Steps 2–8 change the band count; Step Smoothness 0 gives hard edges, 1 a smooth gradient.
4. Assign a ramp texture: Steps and Smoothness grey out, the shading follows the ramp, and a non-Clamp texture shows the warning.
5. Strength 0 matches a plain PBR material exactly; 0.5 is visibly a blend.
6. Shadow Threshold and Half Lambert move/soften the terminator.
7. Cast a shadow onto the object: the shadow edge is banded; disable shadows and the bands remain.
8. Forward+ test: 20+ point lights; toggle "Apply To Additional Lights" and compare.
9. Specular: strength 0 removes the highlight; size and smoothness change its shape; Specular Highlights off (Advanced) disables it.
10. Frame Debugger: SRP Batcher compatible; toon off produces no extra variants in the build log for materials that never enable it.

## 7. Next phase prerequisites (Phase 4: Rim, Matcap, Outline)
- Append rim, matcap and outline properties to the end of the CBUFFER and DOTS block in `StylizedLitInput.hlsl` (after the toon values).
- Add `_RIM_ON` and `_MATCAP_ON` as ForwardLit `shader_feature_local_fragment`; rim and matcap add to the lit color after `StylizedFragmentLit` (new helper in `StylizedCommon.hlsl` or `StylizedLighting.hlsl`).
- Outline needs a new inverted-hull pass (`LightMode = SRPDefaultUnlit`) with `HLSL/StylizedLitOutlinePass.hlsl`, and must not break SRP Batcher.
- Register new foldouts via `FillAdditionalFoldouts` using the next free bit (`1u << 5`, `1u << 6`, ...).
