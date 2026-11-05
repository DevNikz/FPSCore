using NUnit.Framework;
using UnityEngine;

namespace SSGICore.Tests
{
    public class SSGICoreSmokeTests
    {
        [Test]
        public void Shaders_LoadFromResources_AndKernelsAreSupported()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders unsupported on this platform.");

            string[] names = { "Hzb", "Trace", "Temporal", "Filter", "Upsample" };
            foreach (string n in names)
            {
                var cs = Resources.Load<ComputeShader>("SSGICore/SSGICore_" + n);
                Assert.IsNotNull(cs, "Missing compute shader " + n);
            }
            Assert.IsNotNull(Resources.Load<Shader>("SSGICore/SSGICore_Composite"));
        }

        [Test]
        public void Volume_DefaultsAreInactive()
        {
            var v = ScriptableObject.CreateInstance<SSGICoreVolume>();
            Assert.IsFalse(v.IsActive());
            Object.DestroyImmediate(v);
        }
    }
}
