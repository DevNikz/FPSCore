using UnityEngine;

namespace SSGICore
{
    /// <summary>Loads the compute shaders and composite material from Resources/SSGICore.</summary>
    internal static class SSGICoreResources
    {
        public static ComputeShader Hzb, Trace, Temporal, Filter, Upsample;
        public static Material Composite;
        public static int KHzbFromDepth, KHzbDown, KTrace, KTemporal, KAtrous, KUpsample;

        public static bool TryLoad(out string error)
        {
            error = null;
            if (Hzb != null && Trace != null && Temporal != null && Filter != null && Upsample != null && Composite != null)
                return true;

            if (!SystemInfo.supportsComputeShaders)
            {
                error = "Compute shaders are not supported on this device. The fragment fallback (phase 7) is not implemented yet.";
                return false;
            }

            Hzb = Resources.Load<ComputeShader>("SSGICore/SSGICore_Hzb");
            Trace = Resources.Load<ComputeShader>("SSGICore/SSGICore_Trace");
            Temporal = Resources.Load<ComputeShader>("SSGICore/SSGICore_Temporal");
            Filter = Resources.Load<ComputeShader>("SSGICore/SSGICore_Filter");
            Upsample = Resources.Load<ComputeShader>("SSGICore/SSGICore_Upsample");
            Shader composite = Resources.Load<Shader>("SSGICore/SSGICore_Composite");

            if (Hzb == null || Trace == null || Temporal == null || Filter == null || Upsample == null || composite == null)
            {
                error = "Shaders are missing from Resources/SSGICore. Re-import the SSGICore package.";
                return false;
            }

            KHzbFromDepth = Hzb.FindKernel("HzbFromDepth");
            KHzbDown = Hzb.FindKernel("HzbDown");
            KTrace = Trace.FindKernel("Trace");
            KTemporal = Temporal.FindKernel("Temporal");
            KAtrous = Filter.FindKernel("Atrous");
            KUpsample = Upsample.FindKernel("Upsample");

            if (!Hzb.IsSupported(KHzbFromDepth) || !Hzb.IsSupported(KHzbDown) || !Trace.IsSupported(KTrace) ||
                !Temporal.IsSupported(KTemporal) || !Filter.IsSupported(KAtrous) || !Upsample.IsSupported(KUpsample) ||
                !composite.isSupported)
            {
                error = "One or more SSGICore shaders are not supported on this GPU / graphics API.";
                return false;
            }

            Composite = new Material(composite) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }
    }
}
