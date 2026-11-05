using UnityEngine;
using UnityEngine.Rendering;

namespace SSGICore
{
    /// <summary>Per-camera, per-frame shader constants shared by every compute step.</summary>
    internal sealed class SSGIFrameConstants
    {
        public Matrix4x4 view, invView, prevView, proj, invProj, prevViewProj;
        public Vector4 zParams, fullSize, lowSize;
        public readonly Vector4[] ambient = new Vector4[6];
        public float near, thickness, maxDistance, edgeFade, prevWeight, firefly, motionReject,
                     fallbackIntensity, albedo, temporalClamp;
        public int downsample, rayCount, maxSteps, bounces, frame, normalMode,
                   usePrevColor, historyValid, maxAge, hasMotion;

        public void Apply(ComputeCommandBuffer cmd, ComputeShader cs)
        {
            cmd.SetComputeMatrixParam(cs, SSGICoreIds.View, view);
            cmd.SetComputeMatrixParam(cs, SSGICoreIds.InvView, invView);
            cmd.SetComputeMatrixParam(cs, SSGICoreIds.PrevView, prevView);
            cmd.SetComputeMatrixParam(cs, SSGICoreIds.Proj, proj);
            cmd.SetComputeMatrixParam(cs, SSGICoreIds.InvProj, invProj);
            cmd.SetComputeMatrixParam(cs, SSGICoreIds.PrevViewProj, prevViewProj);
            cmd.SetComputeVectorParam(cs, SSGICoreIds.ZParams, zParams);
            cmd.SetComputeVectorParam(cs, SSGICoreIds.FullSize, fullSize);
            cmd.SetComputeVectorParam(cs, SSGICoreIds.LowSize, lowSize);
            cmd.SetComputeVectorArrayParam(cs, SSGICoreIds.AmbientCube, ambient);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.Near, near);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.Thickness, thickness);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.MaxDistance, maxDistance);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.EdgeFade, edgeFade);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.PrevWeight, prevWeight);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.Firefly, firefly);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.MotionReject, motionReject);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.FallbackIntensity, fallbackIntensity);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.AlbedoLevel, albedo);
            cmd.SetComputeFloatParam(cs, SSGICoreIds.TemporalClamp, temporalClamp);
            cmd.SetComputeIntParam(cs, SSGICoreIds.Downsample, downsample);
            cmd.SetComputeIntParam(cs, SSGICoreIds.RayCount, rayCount);
            cmd.SetComputeIntParam(cs, SSGICoreIds.MaxSteps, maxSteps);
            cmd.SetComputeIntParam(cs, SSGICoreIds.Bounces, bounces);
            cmd.SetComputeIntParam(cs, SSGICoreIds.FrameIndex, frame);
            cmd.SetComputeIntParam(cs, SSGICoreIds.NormalMode, normalMode);
            cmd.SetComputeIntParam(cs, SSGICoreIds.UsePrevColor, usePrevColor);
            cmd.SetComputeIntParam(cs, SSGICoreIds.HistoryValid, historyValid);
            cmd.SetComputeIntParam(cs, SSGICoreIds.MaxAge, maxAge);
            cmd.SetComputeIntParam(cs, SSGICoreIds.HasMotion, hasMotion);
        }
    }
}
