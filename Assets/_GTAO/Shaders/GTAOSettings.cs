using System;
using UnityEngine;

namespace GTAO_URP
{
    public enum GTAOApply { Lighting, AfterOpaque }
    public enum GTAOQuality { Low, Medium, High, Ultra, Custom }
    public enum GTAONormals { DepthNormals, ReconstructFromDepth }
    public enum GTAOBlur { Off, Low, Medium, High }

    [Serializable]
    public class GTAOSettings
    {
        [Header("Apply")]
        [Tooltip("Lighting: occludes ambient (+ some direct) in URP lit shaders, same path as URP's built-in SSAO. AfterOpaque: multiplies final camera color.")]
        public GTAOApply apply = GTAOApply.Lighting;
        public GTAONormals normals = GTAONormals.DepthNormals;

        [Header("Shape")]
        [Range(0.05f, 10f)] public float radius = 0.5f;
        [Range(0.1f, 6f)] public float intensity = 1.5f;
        [Range(0.05f, 1f)] public float falloffRange = 0.615f;
        [Range(0f, 0.95f)] public float multiBounce = 0f;
        [Range(0f, 1f)] public float directLightingStrength = 0.25f;

        [Header("Performance")]
        public GTAOQuality quality = GTAOQuality.Medium;
        [Range(1, 6)] public int customSlices = 2;
        [Range(1, 16)] public int customSteps = 4;
        [Range(8f, 512f)] public float maxPixelRadius = 128f;
        public bool downsample = false;
        [Tooltip("AO fades out between start and end. End = 0 disables fading (and skips the far-pixel shortcut).")]
        [Min(0f)] public float fadeStart = 60f;
        [Min(0f)] public float fadeEnd = 100f;

        [Header("Temporal")]
        public bool temporal = true;
        [Range(0f, 0.98f)] public float temporalWeight = 0.9f;
        [Tooltip("Motion (in pixels) at which history fades out completely.")]
        [Min(1f)] public float velocityFadePixels = 20f;
        [Range(0.001f, 0.2f)] public float depthTolerance = 0.03f;
        [Range(0.5f, 3f)] public float clampStrength = 1.25f;
        public bool useMotionVectors = true;

        [Header("Spatial")]
        public GTAOBlur blur = GTAOBlur.Medium;
        [Range(1f, 500f)] public float blurDepthSharpness = 100f;

        [Header("Debug")]
        public bool debugView = false;

        public void GetSampleCounts(out int slices, out int steps)
        {
            switch (quality)
            {
                case GTAOQuality.Low:    slices = 1; steps = 3; break;
                case GTAOQuality.Medium: slices = 2; steps = 4; break;
                case GTAOQuality.High:   slices = 3; steps = 6; break;
                case GTAOQuality.Ultra:  slices = 4; steps = 8; break;
                default:                 slices = customSlices; steps = customSteps; break;
            }
            slices = Mathf.Clamp(slices, 1, 6);
            steps = Mathf.Clamp(steps, 1, 16);
        }

        public int GetBlurRadius()
        {
            int r = blur == GTAOBlur.Low ? 1 : blur == GTAOBlur.Medium ? 2 : blur == GTAOBlur.High ? 3 : 0;
            return downsample ? Mathf.Max(r, 1) : r; // bilateral upsample needs at least one tap
        }

        // Clamped copy; guards against bad serialized/inspector values reaching the shader.
        public GTAOSettings Sanitized()
        {
            var s = (GTAOSettings)MemberwiseClone();
            s.radius = Mathf.Clamp(NanSafe(s.radius, 0.5f), 0.05f, 10f);
            s.intensity = Mathf.Clamp(NanSafe(s.intensity, 1.5f), 0.1f, 6f);
            s.falloffRange = Mathf.Clamp(NanSafe(s.falloffRange, 0.615f), 0.05f, 1f);
            s.multiBounce = Mathf.Clamp(NanSafe(s.multiBounce, 0f), 0f, 0.95f);
            s.directLightingStrength = Mathf.Clamp01(NanSafe(s.directLightingStrength, 0.25f));
            s.maxPixelRadius = Mathf.Clamp(NanSafe(s.maxPixelRadius, 128f), 8f, 512f);
            s.fadeStart = Mathf.Max(0f, NanSafe(s.fadeStart, 0f));
            s.fadeEnd = Mathf.Max(0f, NanSafe(s.fadeEnd, 0f));
            if (s.fadeEnd > 0f && s.fadeStart >= s.fadeEnd) s.fadeStart = s.fadeEnd * 0.5f;
            s.temporalWeight = Mathf.Clamp(NanSafe(s.temporalWeight, 0.9f), 0f, 0.98f);
            s.velocityFadePixels = Mathf.Max(1f, NanSafe(s.velocityFadePixels, 20f));
            s.depthTolerance = Mathf.Clamp(NanSafe(s.depthTolerance, 0.03f), 0.001f, 0.2f);
            s.clampStrength = Mathf.Clamp(NanSafe(s.clampStrength, 1.25f), 0.5f, 3f);
            s.blurDepthSharpness = Mathf.Clamp(NanSafe(s.blurDepthSharpness, 100f), 1f, 500f);
            return s;
        }

        static float NanSafe(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
    }
}
