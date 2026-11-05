using System.Collections.Generic;
using UnityEngine;

namespace SSGICore
{
    public static class SSGICoreUtil
    {
        public const int HzbLevels = 5;

        /// <summary>Size of one axis of HZB level (level 1 = half resolution).</summary>
        public static int HzbLevelSize(int fullSize, int level)
        {
            return Mathf.Max(1, (fullSize + (1 << level) - 1) >> level);
        }

        public static int LowSize(int fullSize, int downsample)
        {
            return Mathf.Max(1, (fullSize + downsample - 1) / downsample);
        }

        public static int GroupCount(int size) { return (size + 7) / 8; }

        /// <summary>Same layout as Unity's _ZBufferParams.</summary>
        public static Vector4 ComputeZParams(float near, float far, bool reversedZ)
        {
            if (reversedZ)
            {
                float x = -1f + far / near;
                return new Vector4(x, 1f, x / far, 1f / far);
            }
            else
            {
                float x = 1f - far / near;
                float y = far / near;
                return new Vector4(x, y, x / far, y / far);
            }
        }
    }

    internal static class SSGICoreLog
    {
        static readonly HashSet<string> s_Seen = new HashSet<string>();

        public static void WarnOnce(string key, string message)
        {
            if (s_Seen.Add(key))
                Debug.LogWarning("[SSGICore] " + message);
        }
    }

    internal static class SSGICoreIds
    {
        static int ID(string n) { return Shader.PropertyToID(n); }

        // frame constants
        public static readonly int View = ID("_SSGI_View");
        public static readonly int InvView = ID("_SSGI_InvView");
        public static readonly int PrevView = ID("_SSGI_PrevView");
        public static readonly int Proj = ID("_SSGI_Proj");
        public static readonly int InvProj = ID("_SSGI_InvProj");
        public static readonly int PrevViewProj = ID("_SSGI_PrevViewProj");
        public static readonly int ZParams = ID("_SSGI_ZParams");
        public static readonly int FullSize = ID("_SSGI_FullSize");
        public static readonly int LowSize = ID("_SSGI_LowSize");
        public static readonly int AmbientCube = ID("_SSGI_AmbientCube");
        public static readonly int Near = ID("_SSGI_Near");
        public static readonly int Thickness = ID("_SSGI_Thickness");
        public static readonly int MaxDistance = ID("_SSGI_MaxDistance");
        public static readonly int EdgeFade = ID("_SSGI_EdgeFade");
        public static readonly int PrevWeight = ID("_SSGI_PrevWeight");
        public static readonly int Firefly = ID("_SSGI_FireflyClamp");
        public static readonly int MotionReject = ID("_SSGI_MotionReject");
        public static readonly int FallbackIntensity = ID("_SSGI_FallbackIntensity");
        public static readonly int AlbedoLevel = ID("_SSGI_AlbedoLevel");
        public static readonly int TemporalClamp = ID("_SSGI_TemporalClamp");
        public static readonly int Downsample = ID("_SSGI_Downsample");
        public static readonly int RayCount = ID("_SSGI_RayCount");
        public static readonly int MaxSteps = ID("_SSGI_MaxSteps");
        public static readonly int Bounces = ID("_SSGI_Bounces");
        public static readonly int FrameIndex = ID("_SSGI_FrameIndex");
        public static readonly int NormalMode = ID("_SSGI_NormalMode");
        public static readonly int UsePrevColor = ID("_SSGI_UsePrevColor");
        public static readonly int HistoryValid = ID("_SSGI_HistoryValid");
        public static readonly int MaxAge = ID("_SSGI_MaxAge");
        public static readonly int HasMotion = ID("_SSGI_HasMotion");
        public static readonly int StepSize = ID("_SSGI_StepSize");

        // textures
        public static readonly int Depth = ID("_SSGI_Depth");
        public static readonly int Normals = ID("_SSGI_Normals");
        public static readonly int Motion = ID("_SSGI_Motion");
        public static readonly int Color = ID("_SSGI_Color");
        public static readonly int PrevColor = ID("_SSGI_PrevColor");
        public static readonly int[] Hzb =
        {
            0, ID("_SSGI_Hzb1"), ID("_SSGI_Hzb2"), ID("_SSGI_Hzb3"), ID("_SSGI_Hzb4"), ID("_SSGI_Hzb5")
        };
        public static readonly int HzbIn = ID("_SSGI_HzbIn");
        public static readonly int HzbOut = ID("_SSGI_HzbOut");
        public static readonly int TraceIn = ID("_SSGI_TraceIn");
        public static readonly int TraceOut = ID("_SSGI_TraceOut");
        public static readonly int HistColorPrev = ID("_SSGI_HistColorPrev");
        public static readonly int HistMomPrev = ID("_SSGI_HistMomPrev");
        public static readonly int HistGeoPrev = ID("_SSGI_HistGeoPrev");
        public static readonly int HistColorOut = ID("_SSGI_HistColorOut");
        public static readonly int HistMomOut = ID("_SSGI_HistMomOut");
        public static readonly int HistGeoOut = ID("_SSGI_HistGeoOut");
        public static readonly int FilterIn = ID("_SSGI_FilterIn");
        public static readonly int FilterOut = ID("_SSGI_FilterOut");
        public static readonly int GIOut = ID("_SSGI_GIOut");

        // composite
        public static readonly int CompGI = ID("_SSGICore_GI");
        public static readonly int CompOpaque = ID("_SSGICore_Opaque");
        public static readonly int CompDepth = ID("_SSGICore_Depth");
        public static readonly int CompRaw = ID("_SSGICore_Raw");
        public static readonly int CompAge = ID("_SSGICore_Age");
        public static readonly int CompParams = ID("_SSGICore_Params");
        public static readonly int CompSize = ID("_SSGICore_Size");
        public static readonly int UserGI = ID("_SSGICore_GITexture");
    }
}
