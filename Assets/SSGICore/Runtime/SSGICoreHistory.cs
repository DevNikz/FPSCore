using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSGICore
{
    /// <summary>
    /// Per-camera history: accumulated GI (rgb + age), luminance moments, and geometry (world normal + eye depth).
    /// All three are low-resolution (trace resolution) double-buffered RTs.
    /// </summary>
    public sealed class SSGICoreHistory : CameraHistoryItem
    {
        int m_Color, m_Moments, m_Geo;
        int m_Width, m_Height;
        int m_LastFrame = -1000;

        public Matrix4x4 PrevView = Matrix4x4.identity;
        public Matrix4x4 PrevViewProj = Matrix4x4.identity;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_Color = MakeId(0);
            m_Moments = MakeId(1);
            m_Geo = MakeId(2);
        }

        public RTHandle GetCurrent(int index) { return GetCurrentFrameRT(IdOf(index)); }
        public RTHandle GetPrevious(int index) { return GetPreviousFrameRT(IdOf(index)); }

        int IdOf(int index) { return index == 0 ? m_Color : (index == 1 ? m_Moments : m_Geo); }

        public void Update(int width, int height)
        {
            if (width == m_Width && height == m_Height && GetCurrentFrameRT(m_Color) != null)
                return;

            Reset();

            var desc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, 0);
            desc.enableRandomWrite = true;
            desc.msaaSamples = 1;
            desc.useMipMap = false;

            AllocHistoryFrameRT(m_Color, 2, ref desc, "SSGICore_HistColor");
            AllocHistoryFrameRT(m_Moments, 2, ref desc, "SSGICore_HistMoments");
            AllocHistoryFrameRT(m_Geo, 2, ref desc, "SSGICore_HistGeo");

            m_Width = width;
            m_Height = height;
            m_LastFrame = -1000;
        }

        /// <summary>True when the previous buffers were written on the immediately preceding frame.</summary>
        public bool IsPreviousValid(int frame)
        {
            int delta = frame - m_LastFrame;
            return delta >= 0 && delta <= 1;
        }

        public void MarkWritten(int frame, Matrix4x4 view, Matrix4x4 viewProj)
        {
            m_LastFrame = frame;
            PrevView = view;
            PrevViewProj = viewProj;
        }

        public override void Reset()
        {
            ReleaseHistoryFrameRT(m_Color);
            ReleaseHistoryFrameRT(m_Moments);
            ReleaseHistoryFrameRT(m_Geo);
            m_Width = 0;
            m_Height = 0;
            m_LastFrame = -1000;
        }
    }
}
