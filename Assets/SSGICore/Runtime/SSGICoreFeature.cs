using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSGICore
{
    /// <summary>
    /// URP Renderer Feature that adds screen-space global illumination (SSGICore).
    /// Add it to the Forward+ Universal Renderer, then add the Lighting/SSGICore override to a Volume.
    /// </summary>
    [DisallowMultipleRendererFeature("SSGICore")]
    [Tooltip("Screen-space global illumination (HZB ray march, temporal + spatial denoise).")]
    public sealed class SSGICoreFeature : ScriptableRendererFeature
    {
        [Tooltip("Where GI is composited. Must be after opaques and before transparents.")]
        [SerializeField] RenderPassEvent m_PassEvent = RenderPassEvent.BeforeRenderingTransparents;

        [Tooltip("Render the effect in the Scene view.")]
        [SerializeField] bool m_ShowInSceneView = true;

        [Tooltip("Request the DepthNormals prepass for accurate normals. Off = reconstruct normals from depth.")]
        [SerializeField] bool m_UseNormalsTexture = true;

        SSGICorePass m_Pass;

        public override void Create()
        {
            m_Pass = new SSGICorePass { renderPassEvent = m_PassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Pass == null)
                return;

            CameraType type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection)
                return;
            if (type == CameraType.SceneView && !m_ShowInSceneView)
                return;

            var volume = VolumeManager.instance.stack.GetComponent<SSGICoreVolume>();
            if (volume == null || !volume.IsActive())
                return;

            SSGICoreSettings cfg = SSGICoreQuality.Resolve(volume);

            ScriptableRenderPassInput inputs = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color;
            if (m_UseNormalsTexture && volume.normalSource.value != SSGICoreNormalSource.ReconstructFromDepth)
                inputs |= ScriptableRenderPassInput.Normal;
            if (cfg.temporal || volume.lightingSource.value != SSGICoreLightingSource.CurrentFrame)
                inputs |= ScriptableRenderPassInput.Motion;

            m_Pass.renderPassEvent = m_PassEvent;
            m_Pass.ConfigureInput(inputs);
            m_Pass.Setup(volume);
            renderer.EnqueuePass(m_Pass);
        }
    }
}
