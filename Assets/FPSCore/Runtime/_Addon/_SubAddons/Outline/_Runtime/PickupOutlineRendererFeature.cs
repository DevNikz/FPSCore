using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class PickupOutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Look")]
        [Tooltip("Outline color (HDR picker).")]
        [ColorUsage(true, true)]
        public Color color = new Color(0.7567195f, 0.8962264f, 0.880237f, 1f);

        [Tooltip("Outline width in screen pixels.")]
        [Range(0.5f, 16f)]
        public float thickness = 7f;

        [Tooltip("HDR multiplier on the color. >1 feeds Bloom for a glow (needs HDR + Bloom).")]
        [Min(0f)]
        public float emission = 1.5f;

        [Header("Shape")]
        [Tooltip(
            "Min neighbour coverage (0-1) to draw. 0 = full-width outline; higher = tighter, hugs the silhouette."
        )]
        [Range(0f, 0.95f)]
        public float threshold = 0f;

        [Tooltip("Falloff width above the threshold. 0 = hard edge; higher = soft glow gradient.")]
        [Range(0f, 1f)]
        public float softness = 0.15f;

        [Header("Visibility")]
        [Tooltip(
            "Outline opacity where the object is behind scene geometry. 0 = hidden, 1 = see-through."
        )]
        [Range(0f, 1f)]
        public float occludedOpacity = 0f;

        [Tooltip(
            "Depth slack (m) for the occlusion test; raise if outlines flicker at contact points."
        )]
        [Min(0f)]
        public float depthTolerance = 0.1f;

        [Tooltip("Skip pickups farther than this from the camera (m). 0 = no limit.")]
        [Min(0f)]
        public float maxDistance = 0f;

        [Tooltip(
            "On = outline only the pickup the player is looking at in range (set by PickupOutlineTargeting). Off = outline every registered pickup."
        )]
        public bool onlyWhenTargeted = true;

        [Tooltip(
            "Hide the outline while the pickup is carried (matters when Only When Targeted is off)."
        )]
        public bool hideWhileHeld = true;

        [Header("Placement Ghost")]
        [Tooltip("Outline color of a valid placement preview (HDR).")]
        [ColorUsage(true, true)]
        public Color ghostValidColor = new Color(0.2f, 1f, 0.5f, 1f);

        [Tooltip("Outline color of an invalid placement preview (HDR).")]
        [ColorUsage(true, true)]
        public Color ghostInvalidColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Header("Advanced")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    [Tooltip(
        "Hidden/PickupOutline. Auto-assigned on creation; assign manually if empty (needed for builds)."
    )]
    [SerializeField]
    private Shader shader;

    private Material _material;
    private PickupOutlinePass _pass;

    public override void Create()
    {
        CoreUtils.Destroy(_material);
        _material = null;
        _pass = null;

        Shader s = shader != null ? shader : Shader.Find("Hidden/PickupOutline");
        if (s == null)
        {
            Debug.LogWarning("PickupOutline: shader 'Hidden/PickupOutline' not found.");
            return;
        }
        _material = CoreUtils.CreateEngineMaterial(s);
        _pass = new PickupOutlinePass(settings, _material);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData
    )
    {
        if (_pass == null || _material == null)
            return;
        int pending =
            (
                settings.onlyWhenTargeted
                    ? PickupOutlineRegistry.HighlightedCount
                    : PickupOutlineRegistry.Count
            ) + PickupOutlineRegistry.GhostCount;
        if (pending == 0)
            return; // nothing to outline: skip the whole pass
        var type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game && type != CameraType.SceneView)
            return;

        _pass.renderPassEvent = settings.renderPassEvent;
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        shader = Shader.Find("Hidden/PickupOutline");
    }
#endif

    private sealed class PickupOutlinePass : ScriptableRenderPass
    {
        private struct DrawItem
        {
            public Renderer renderer; // null for ghosts
            public Mesh mesh; // ghosts only
            public Matrix4x4 matrix; // ghosts only
            public int subMeshes;
            public int shaderPass; // 0 default, 1 ghost valid, 2 ghost invalid
        }

        private class MaskData
        {
            public List<DrawItem> items;
            public Material material;
            public Matrix4x4 view,
                viewProj;
        }

        private class CompositeData
        {
            public TextureHandle mask;
            public Material material;
            public Vector4 color,
                p1,
                p2,
                texel,
                ghostValid,
                ghostInvalid;
        }

        private static readonly int ViewId = Shader.PropertyToID("_PickupOutlineView");
        private static readonly int VpId = Shader.PropertyToID("_PickupOutlineVP");
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int Params1Id = Shader.PropertyToID("_OutlineParams");
        private static readonly int Params2Id = Shader.PropertyToID("_OutlineParams2");
        private static readonly int TexelId = Shader.PropertyToID("_PickupOutlineTexel");
        private static readonly int GhostValidId = Shader.PropertyToID("_GhostValidColor");
        private static readonly int GhostInvalidId = Shader.PropertyToID("_GhostInvalidColor");
        private static readonly Plane[] s_planes = new Plane[6];

        private readonly Settings _s;
        private readonly Material _m;
        private readonly List<DrawItem> _items = new();

        public PickupOutlinePass(Settings s, Material m)
        {
            _s = s;
            _m = m;
            profilingSampler = new ProfilingSampler("Pickup Outline");
        }

        private void Gather(Camera cam)
        {
            _items.Clear();
            GeometryUtility.CalculateFrustumPlanes(cam, s_planes);
            Vector3 camPos = cam.transform.position;
            float maxSqr = _s.maxDistance > 0f ? _s.maxDistance * _s.maxDistance : float.MaxValue;

            var entries = _s.onlyWhenTargeted
                ? PickupOutlineRegistry.HighlightedEntries
                : PickupOutlineRegistry.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Owner == null && e.SubOwner == null)
                    continue; // both destroyed
                if (_s.hideWhileHeld && e.SubOwner != null && e.SubOwner.IsHeld)
                    continue;
                for (int k = 0; k < e.Renderers.Length; k++)
                {
                    var r = e.Renderers[k];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                        continue;
                    Bounds b = r.bounds;
                    if ((b.center - camPos).sqrMagnitude > maxSqr)
                        continue;
                    if (!GeometryUtility.TestPlanesAABB(s_planes, b))
                        continue;
                    _items.Add(new DrawItem { renderer = r, subMeshes = e.SubMeshCounts[k] });
                }
            }
            GatherGhosts();
        }

        private void GatherGhosts()
        {
            if (PickupOutlineRegistry.GhostCount == 0)
                return;
            var ghosts = PickupOutlineRegistry.Ghosts;
            for (int i = 0; i < ghosts.Count; i++)
            {
                var g = ghosts[i];
                _items.Add(
                    new DrawItem
                    {
                        mesh = g.Mesh,
                        matrix = g.Matrix,
                        subMeshes = g.SubMeshCount,
                        shaderPass = g.Style == PreviewValidity.Valid ? 1 : 2,
                    }
                );
            }
        }

        private static Vector4 ToHdr(Color c, float emission)
        {
            Color l = c.linear;
            return new Vector4(l.r * emission, l.g * emission, l.b * emission, l.a);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var res = frameData.Get<UniversalResourceData>();
            var cam = frameData.Get<UniversalCameraData>();

            Gather(cam.camera);
            if (_items.Count == 0)
                return; // zero cost when nothing is visible

            var desc = cam.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            desc.graphicsFormat = GraphicsFormat.R16G16_SFloat;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                desc,
                "_PickupOutlineMask",
                true,
                FilterMode.Point
            );

            // Camera.projectionMatrix is unjittered (TAA jitter is applied by URP separately),
            // so the mask stays stable. 'true' = we render into a texture (flip handling).
            Matrix4x4 view = cam.camera.worldToCameraMatrix;
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(cam.camera.projectionMatrix, true);

            using (
                var builder = renderGraph.AddRasterRenderPass<MaskData>(
                    "Pickup Outline Mask",
                    out var data
                )
            )
            {
                data.items = _items;
                data.material = _m;
                data.view = view;
                data.viewProj = proj * view;

                builder.SetRenderAttachment(mask, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    (MaskData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetMatrix(ViewId, d.view);
                        d.material.SetMatrix(VpId, d.viewProj);
                        for (int i = 0; i < d.items.Count; i++)
                        {
                            var it = d.items[i];
                            if (it.renderer != null)
                            {
                                for (int s = 0; s < it.subMeshes; s++)
                                    ctx.cmd.DrawRenderer(it.renderer, d.material, s, 0);
                            }
                            else if (it.mesh != null)
                            {
                                for (int s = 0; s < it.subMeshes; s++)
                                    ctx.cmd.DrawMesh(
                                        it.mesh,
                                        it.matrix,
                                        d.material,
                                        s,
                                        it.shaderPass
                                    );
                            }
                        }
                    }
                );
            }

            using (
                var builder = renderGraph.AddRasterRenderPass<CompositeData>(
                    "Pickup Outline Composite",
                    out var data
                )
            )
            {
                data.mask = mask;
                data.material = _m;
                data.color = ToHdr(_s.color, _s.emission);
                data.ghostValid = ToHdr(_s.ghostValidColor, _s.emission);
                data.ghostInvalid = ToHdr(_s.ghostInvalidColor, _s.emission);
                data.p1 = new Vector4(_s.thickness, _s.threshold, _s.softness, _s.occludedOpacity);
                data.p2 = new Vector4(_s.depthTolerance, 0, 0, 0);
                data.texel = new Vector4(1f / desc.width, 1f / desc.height, 0, 0);

                builder.UseTexture(mask);
                if (res.cameraDepthTexture.IsValid())
                    builder.UseTexture(res.cameraDepthTexture); // global _CameraDepthTexture
                builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    (CompositeData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetVector(ColorId, d.color);
                        d.material.SetVector(Params1Id, d.p1);
                        d.material.SetVector(Params2Id, d.p2);
                        d.material.SetVector(TexelId, d.texel);
                        d.material.SetVector(GhostValidId, d.ghostValid);
                        d.material.SetVector(GhostInvalidId, d.ghostInvalid);
                        Blitter.BlitTexture(
                            ctx.cmd,
                            d.mask,
                            new Vector4(1, 1, 0, 0),
                            d.material,
                            3
                        );
                    }
                );
            }
        }
    }
}
