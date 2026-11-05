using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSGICore
{
    public enum SSGICoreQualityPreset { Low, Medium, High, Custom }
    public enum SSGICoreResolution { Full = 1, Half = 2, Quarter = 4 }
    public enum SSGICoreLightingSource { CurrentFrame, PreviousFrame, Blended }
    public enum SSGICoreNormalSource { Raw, Octahedral, ReconstructFromDepth }
    public enum SSGICoreDebugView { None, GI, RawTrace, HistoryAge }

    [Serializable]
    public sealed class SSGICoreQualityParameter : VolumeParameter<SSGICoreQualityPreset>
    {
        public SSGICoreQualityParameter(SSGICoreQualityPreset value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class SSGICoreResolutionParameter : VolumeParameter<SSGICoreResolution>
    {
        public SSGICoreResolutionParameter(SSGICoreResolution value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class SSGICoreLightingSourceParameter : VolumeParameter<SSGICoreLightingSource>
    {
        public SSGICoreLightingSourceParameter(SSGICoreLightingSource value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class SSGICoreNormalSourceParameter : VolumeParameter<SSGICoreNormalSource>
    {
        public SSGICoreNormalSourceParameter(SSGICoreNormalSource value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class SSGICoreDebugViewParameter : VolumeParameter<SSGICoreDebugView>
    {
        public SSGICoreDebugViewParameter(SSGICoreDebugView value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>Volume override holding every SSGICore setting.</summary>
    [Serializable]
    [VolumeComponentMenu("Lighting/SSGICore")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class SSGICoreVolume : VolumeComponent
    {
        [Header("General")]
        public BoolParameter enable = new BoolParameter(false);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 8f);
        public SSGICoreQualityParameter quality = new SSGICoreQualityParameter(SSGICoreQualityPreset.Medium);

        [Header("Custom Quality (used when Quality = Custom)")]
        public SSGICoreResolutionParameter resolution = new SSGICoreResolutionParameter(SSGICoreResolution.Half);
        public ClampedIntParameter rayCount = new ClampedIntParameter(1, 1, 4);
        public ClampedIntParameter maxSteps = new ClampedIntParameter(24, 8, 96);
        public ClampedIntParameter bounces = new ClampedIntParameter(2, 1, 3);
        public BoolParameter temporal = new BoolParameter(true);
        public ClampedIntParameter temporalFrames = new ClampedIntParameter(16, 2, 64);
        public ClampedIntParameter filterIterations = new ClampedIntParameter(3, 0, 4);

        [Header("Ray March")]
        [Tooltip("Maximum ray length in world units.")]
        public ClampedFloatParameter maxDistance = new ClampedFloatParameter(20f, 1f, 100f);
        [Tooltip("Depth thickness in world units (grows slightly with distance).")]
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.2f, 0.01f, 2f);
        [Tooltip("Screen-edge fade. Higher = narrower fade zone.")]
        public ClampedFloatParameter edgeFade = new ClampedFloatParameter(8f, 0f, 32f);

        [Header("Lighting")]
        public SSGICoreLightingSourceParameter lightingSource = new SSGICoreLightingSourceParameter(SSGICoreLightingSource.Blended);
        [Tooltip("Weight of the previous frame colour when Lighting Source = Blended.")]
        public ClampedFloatParameter previousFrameWeight = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("Multiplier on the ambient (sky / probe) fallback used when a ray leaves the screen.")]
        public ClampedFloatParameter fallbackIntensity = new ClampedFloatParameter(1f, 0f, 4f);
        [Tooltip("Approximate albedo level. Forward+ has no albedo buffer, so albedo is estimated from scene colour chroma.")]
        public ClampedFloatParameter albedoLevel = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("Per-sample luminance clamp that suppresses fireflies.")]
        public ClampedFloatParameter fireflyClamp = new ClampedFloatParameter(20f, 1f, 100f);

        [Header("Temporal")]
        [Tooltip("Variance clipping strength of the history. 0 disables it.")]
        public ClampedFloatParameter temporalClamp = new ClampedFloatParameter(2f, 0f, 4f);
        [Tooltip("Pixels of object motion (beyond camera motion) at which history is mostly rejected. 0 disables.")]
        public ClampedFloatParameter motionRejection = new ClampedFloatParameter(8f, 0f, 64f);

        [Header("Advanced")]
        public SSGICoreNormalSourceParameter normalSource = new SSGICoreNormalSourceParameter(SSGICoreNormalSource.Raw);
        public SSGICoreDebugViewParameter debugView = new SSGICoreDebugViewParameter(SSGICoreDebugView.None);

        public bool IsActive() => enable.value && intensity.value > 0f;
    }
}
