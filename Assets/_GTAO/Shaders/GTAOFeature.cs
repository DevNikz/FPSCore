using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GTAO_URP
{
    // Add to a Universal Renderer set to Forward or Forward+. Screen-space only (camera depth/normals).
    [DisallowMultipleRendererFeature("GTAO (Screen Space)")]
    public class GTAOFeature : ScriptableRendererFeature
    {
        public GTAOSettings settings = new GTAOSettings();
        [SerializeField] Shader shader;

        Material m_Material;
        GTAOPass m_Pass;
        GTAOKeywordResetPass m_Reset;
        bool m_WarnedShader;

#if UNITY_EDITOR
        void Reset() { AutoAssignShader(); }

        void AutoAssignShader()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("GTAO t:Shader"))
            {
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name == GTAOPass.ShaderName) { shader = s; return; }
            }
        }
#endif

        public override void Create()
        {
            if (settings == null) settings = new GTAOSettings();
#if UNITY_EDITOR
            if (shader == null) AutoAssignShader();
#endif
            if (shader == null) shader = Shader.Find(GTAOPass.ShaderName); // last resort, not build-safe

            if (m_Material != null && m_Material.shader != shader) { CoreUtils.Destroy(m_Material); m_Material = null; }
            if (m_Material == null && shader != null && shader.isSupported)
                m_Material = CoreUtils.CreateEngineMaterial(shader);

            m_Pass ??= new GTAOPass();
            m_Reset ??= new GTAOKeywordResetPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderer == null || settings == null) return;

            Camera cam = renderingData.cameraData.camera;
            if (cam == null) return;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if (UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData)) return;

            if (renderer is UniversalRenderer ur && IsDeferred(ur))
                Debug.LogWarning($"[{name}] This feature is designed for Forward/Forward+. The attached renderer is Deferred; it will still run screen-space off camera depth, but a Deferred-aware G-Buffer path is not implemented here.");

            if (m_Material == null || m_Pass == null)
            {
                if (!m_WarnedShader)
                {
                    Debug.LogWarning($"[{name}] GTAO shader/material missing or unsupported. Assign '{GTAOPass.ShaderName}' on the feature.");
                    m_WarnedShader = true;
                }
                return;
            }

            var s = settings.Sanitized();
            if (!m_Pass.Setup(s, m_Material)) return;
            renderer.EnqueuePass(m_Pass);

            if (s.apply == GTAOApply.Lighting && !s.debugView)
                renderer.EnqueuePass(m_Reset); // clears _SCREEN_SPACE_OCCLUSION so it can't leak to other cameras
        }

        static bool IsDeferred(UniversalRenderer ur)
        {
            var f = typeof(UniversalRenderer).GetField("m_RenderingMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null && f.GetValue(ur) is RenderingMode m && m == RenderingMode.Deferred;
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            m_Pass = null;
            m_Reset = null;
            CoreUtils.Destroy(m_Material);
            m_Material = null;
        }
    }
}
