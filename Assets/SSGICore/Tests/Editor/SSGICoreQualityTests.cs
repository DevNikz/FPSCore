using NUnit.Framework;
using UnityEngine;

namespace SSGICore.Tests
{
    public class SSGICoreQualityTests
    {
        [Test]
        public void Presets_ScaleDownsampleAndCost()
        {
            var low = SSGICoreQuality.Preset(SSGICoreQualityPreset.Low);
            var med = SSGICoreQuality.Preset(SSGICoreQualityPreset.Medium);
            var high = SSGICoreQuality.Preset(SSGICoreQualityPreset.High);

            Assert.AreEqual(4, low.downsample);
            Assert.AreEqual(2, med.downsample);
            Assert.AreEqual(2, high.downsample);
            Assert.Less(low.maxSteps, high.maxSteps);
            Assert.LessOrEqual(low.bounces, high.bounces);
            Assert.LessOrEqual(low.rayCount, high.rayCount);
        }

        [Test]
        public void Resolve_Custom_UsesVolumeValues()
        {
            var v = ScriptableObject.CreateInstance<SSGICoreVolume>();
            v.quality.value = SSGICoreQualityPreset.Custom;
            v.resolution.value = SSGICoreResolution.Full;
            v.rayCount.value = 3;
            v.maxSteps.value = 40;
            v.bounces.value = 2;
            v.filterIterations.value = 1;
            v.temporalFrames.value = 8;

            var s = SSGICoreQuality.Resolve(v);
            Assert.AreEqual(1, s.downsample);
            Assert.AreEqual(3, s.rayCount);
            Assert.AreEqual(40, s.maxSteps);
            Assert.AreEqual(1, s.filterIterations);
            Assert.AreEqual(8, s.maxAge);
            Object.DestroyImmediate(v);
        }

        [Test]
        public void HzbLevelSize_RoundsUpAndNeverZero()
        {
            Assert.AreEqual(960, SSGICoreUtil.HzbLevelSize(1920, 1));
            Assert.AreEqual(61, SSGICoreUtil.HzbLevelSize(1921, 5));
            Assert.AreEqual(1, SSGICoreUtil.HzbLevelSize(1, 5));
        }

        [Test]
        public void LowSize_RoundsUp()
        {
            Assert.AreEqual(961, SSGICoreUtil.LowSize(1921, 2));
            Assert.AreEqual(480, SSGICoreUtil.LowSize(1920, 4));
        }

        [Test]
        public void ZParams_MatchUnityLayout()
        {
            Vector4 z = SSGICoreUtil.ComputeZParams(0.1f, 100f, true);
            // eye depth of raw depth 1 (near plane, reversed Z) must equal near
            float eye = 1f / (z.z * 1f + z.w);
            Assert.AreEqual(0.1f, eye, 1e-4f);
            // raw depth 0 (far plane) must equal far
            Assert.AreEqual(100f, 1f / (z.z * 0f + z.w), 1e-2f);
        }
    }
}
