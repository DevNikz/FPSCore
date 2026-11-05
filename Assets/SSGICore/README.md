# SSGICore 0.1.0

Screen-space global illumination for **Unity 6.3 and 6.5+**, **URP Forward+**, Render Graph only.

## Install
1. Import `SSGICore_v0.1.0.unitypackage` (requires `com.unity.render-pipelines.universal` 17.x).
2. Select your Forward+ **Universal Renderer** asset -> *Add Renderer Feature* -> **SSGICore Feature**.
3. Add a global **Volume** and add the override **Lighting > SSGICore**; tick *Enable*.
4. Optional: *Tools > SSGICore > Create Demo Content* builds a test room + Volume.

The feature requests Depth, Normals (DepthNormals prepass), Motion Vectors and the Opaque texture itself.

## Pipeline
HZB min-depth pyramid -> low-res dithered ray march (1-3 bounces) -> temporal accumulation (motion-vector
reprojection, depth/normal rejection, variance clipping) -> variance-guided a-trous filter -> depth/normal-aware
bilateral upsample -> additive composite.

## Settings (Volume)
* **Quality**: Low (1/4 res), Medium (1/2 res), High (1/2 res, 2 rays), Custom.
* **Lighting Source**: Current frame, Previous frame (free extra bounces by feedback), or Blended.
* **Bounces**: extra explicit bounces inside the trace (albedo is approximated).
* **Debug View**: GI, Raw Trace, History Age.
* **Normal Source**: Raw (default), Octahedral (if "Accurate G-buffer normals" is on), Reconstruct from depth.

## Forward+ albedo note
Forward+ has no albedo buffer. The built-in composite estimates albedo from scene-colour chroma x *Albedo Level*.
For exact results in your own shaders include `Shaders/SSGICoreSampling.hlsl` and use
`SSGICoreSampleGI(screenUV) * albedo`. The texture is exposed as global `_SSGICore_GITexture` after the composite.

## Known limitations (v0.1.0)
* Compute-shader path only; the fragment fallback (phase 7) is not implemented, unsupported devices skip the effect.
* Perspective cameras only. Transparents do not contribute or receive GI.
* Modes B (GTAO-style) and C (probe fallback for off-screen hits) are planned, not implemented. Off-screen rays use the ambient probe.
* Written against the URP 17.x Render Graph API; not compiled against 6.5 in CI yet. Please report API drift.
