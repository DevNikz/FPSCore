using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SSGICore
{
    /// <summary>
    /// Records the whole SSGICore pipeline into the URP render graph:
    /// HZB build -> trace (low-res) -> temporal accumulate -> a-trous filter -> bilateral upsample -> composite.
    /// </summary>
    internal sealed class SSGICorePass : ScriptableRenderPass
    {
        // ---- pass data ------------------------------------------------------------------------------------

        sealed class StepData
        {
            public ComputeShader cs;
            public int kernel, groupsX, groupsY, stepSize, count;
            public SSGIFrameConstants constants;
            public readonly int[] ids = new int[16];
            public readonly TextureHandle[] tex = new TextureHandle[16];

            public void Begin(ComputeShader shader, int kernelIndex, int width, int height, SSGIFrameConstants c, int step)
            {
                cs = shader;
                kernel = kernelIndex;
                groupsX = SSGICoreUtil.GroupCount(width);
                groupsY = SSGICoreUtil.GroupCount(height);
                constants = c;
                stepSize = step;
                count = 0;
            }

            public void Add(int id, TextureHandle handle)
            {
                ids[count] = id;
                tex[count] = handle;
                count++;
            }

            public void Execute(ComputeCommandBuffer cmd)
            {
                constants.Apply(cmd, cs);
                cmd.SetComputeIntParam(cs, SSGICoreIds.StepSize, stepSize);
                for (int i = 0; i < count; i++)
                    cmd.SetComputeTextureParam(cs, kernel, ids[i], tex[i]);
                cmd.DispatchCompute(cs, kernel, groupsX, groupsY, 1);
            }
        }

        sealed class CompositeData
        {
            public TextureHandle gi, opaque, depth, raw, age;
            public Material material;
            public MaterialPropertyBlock block;
            public int passIndex;
            public Vector4 prm, size;
        }

        // ---- state ----------------------------------------------------------------------------------------

        SSGICoreVolume m_Volume;
        readonly SSGIFrameConstants m_C = new SSGIFrameConstants();
        readonly TextureHandle[] m_Hzb = new TextureHandle[SSGICoreUtil.HzbLevels + 1];
        readonly MaterialPropertyBlock m_Block = new MaterialPropertyBlock();

        static readonly Vector3[] s_AmbientDirs =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };
        static readonly Color[] s_AmbientColors = new Color[6];

        public void Setup(SSGICoreVolume volume)
        {
            m_Volume = volume;
        }

        static TextureHandle CreateUav(RenderGraph rg, int w, int h, GraphicsFormat format, string name)
        {
            var desc = new TextureDesc(w, h)
            {
                colorFormat = format,
                enableRandomWrite = true,
                clearBuffer = false,
                name = name,
            };
            return rg.CreateTexture(desc);
        }

        // ---- record ---------------------------------------------------------------------------------------

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            SSGICoreVolume vol = m_Volume;
            if (vol == null)
                return;

            if (!SSGICoreResources.TryLoad(out string error))
            {
                SSGICoreLog.WarnOnce("resources", error);
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            Camera camera = cameraData.camera;

            if (camera.orthographic)
            {
                SSGICoreLog.WarnOnce("ortho", "Orthographic cameras are not supported; SSGICore is skipped for them.");
                return;
            }
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle depth = resourceData.cameraDepthTexture;
            TextureHandle opaque = resourceData.cameraOpaqueTexture;
            if (!depth.IsValid() || !opaque.IsValid())
            {
                SSGICoreLog.WarnOnce("inputs", "Camera depth or opaque texture is unavailable at this pass event; SSGICore is skipped.");
                return;
            }

            TextureHandle normals = resourceData.cameraNormalsTexture;
            TextureHandle motion = resourceData.motionVectorColor;
            bool hasNormals = normals.IsValid() && vol.normalSource.value != SSGICoreNormalSource.ReconstructFromDepth;
            bool hasMotion = motion.IsValid();

            UniversalCameraHistory historyManager = cameraData.historyManager;
            if (historyManager == null)
                return;

            historyManager.RequestAccess<SSGICoreHistory>();
            SSGICoreHistory hist = historyManager.GetHistoryForWrite<SSGICoreHistory>();
            if (hist == null)
                return; // history becomes available next frame

            SSGICoreSettings cfg = SSGICoreQuality.Resolve(vol);

            // previous-frame colour (bounce feedback)
            RTHandle prevColorRT = null;
            if (vol.lightingSource.value != SSGICoreLightingSource.CurrentFrame)
            {
                historyManager.RequestAccess<RawColorHistory>();
                RawColorHistory rawHistory = historyManager.GetHistoryForRead<RawColorHistory>();
                prevColorRT = rawHistory?.GetPreviousTexture(0);
                if (prevColorRT != null && prevColorRT.rt == null)
                    prevColorRT = null;
            }
            bool usePrevColor = prevColorRT != null;

            // ---- sizes -----------------------------------------------------------------------------------
            RenderTextureDescriptor camDesc = cameraData.cameraTargetDescriptor;
            int fullW = camDesc.width;
            int fullH = camDesc.height;
            int ds = Mathf.Clamp(cfg.downsample, 1, 4);
            int lowW = SSGICoreUtil.LowSize(fullW, ds);
            int lowH = SSGICoreUtil.LowSize(fullH, ds);

            hist.Update(lowW, lowH);
            bool historyValid = cfg.temporal && hist.IsPreviousValid(Time.frameCount);

            // ---- constants -------------------------------------------------------------------------------
            Matrix4x4 view = cameraData.GetViewMatrix(0);
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(0), true);
            SSGIFrameConstants c = m_C;
            c.view = view;
            c.invView = view.inverse;
            c.proj = proj;
            c.invProj = proj.inverse;
            Matrix4x4 viewProj = proj * view;
            c.prevView = historyValid ? hist.PrevView : view;
            c.prevViewProj = historyValid ? hist.PrevViewProj : viewProj;
            c.zParams = SSGICoreUtil.ComputeZParams(camera.nearClipPlane, camera.farClipPlane, SystemInfo.usesReversedZBuffer);
            c.fullSize = new Vector4(fullW, fullH, 1f / fullW, 1f / fullH);
            c.lowSize = new Vector4(lowW, lowH, 1f / lowW, 1f / lowH);
            c.near = camera.nearClipPlane;
            c.thickness = vol.thickness.value;
            c.maxDistance = vol.maxDistance.value;
            c.edgeFade = vol.edgeFade.value;
            c.prevWeight = vol.lightingSource.value == SSGICoreLightingSource.PreviousFrame ? 1f : vol.previousFrameWeight.value;
            c.firefly = vol.fireflyClamp.value;
            c.motionReject = vol.motionRejection.value;
            c.fallbackIntensity = vol.fallbackIntensity.value;
            c.albedo = vol.albedoLevel.value;
            c.temporalClamp = vol.temporalClamp.value;
            c.downsample = ds;
            c.rayCount = cfg.rayCount;
            c.maxSteps = cfg.maxSteps;
            c.bounces = cfg.bounces;
            c.frame = Time.frameCount & 0xFFFF;
            c.normalMode = hasNormals ? (int)vol.normalSource.value : (int)SSGICoreNormalSource.ReconstructFromDepth;
            c.usePrevColor = usePrevColor ? 1 : 0;
            c.historyValid = historyValid ? 1 : 0;
            c.maxAge = cfg.maxAge;
            c.hasMotion = hasMotion ? 1 : 0;
            FillAmbient(c.ambient, 1f);

            // ---- resources -------------------------------------------------------------------------------
            TextureHandle black = renderGraph.defaultResources.blackTexture;
            TextureHandle normalsIn = hasNormals ? normals : black;
            TextureHandle motionIn = hasMotion ? motion : black;
            TextureHandle prevColorIn = usePrevColor ? renderGraph.ImportTexture(prevColorRT) : black;

            for (int level = 1; level <= SSGICoreUtil.HzbLevels; level++)
            {
                m_Hzb[level] = CreateUav(renderGraph,
                    SSGICoreUtil.HzbLevelSize(fullW, level), SSGICoreUtil.HzbLevelSize(fullH, level),
                    GraphicsFormat.R32_SFloat, "SSGICore_Hzb" + level);
            }

            const GraphicsFormat kRgba = GraphicsFormat.R16G16B16A16_SFloat;
            TextureHandle trace = CreateUav(renderGraph, lowW, lowH, kRgba, "SSGICore_Trace");
            TextureHandle filterA = CreateUav(renderGraph, lowW, lowH, kRgba, "SSGICore_FilterA");
            TextureHandle filterB = CreateUav(renderGraph, lowW, lowH, kRgba, "SSGICore_FilterB");
            TextureHandle giFull = CreateUav(renderGraph, fullW, fullH, kRgba, "SSGICore_GI");

            TextureHandle histColorCur = renderGraph.ImportTexture(hist.GetCurrent(0));
            TextureHandle histMomCur = renderGraph.ImportTexture(hist.GetCurrent(1));
            TextureHandle histGeoCur = renderGraph.ImportTexture(hist.GetCurrent(2));
            TextureHandle histColorPrev = renderGraph.ImportTexture(hist.GetPrevious(0));
            TextureHandle histMomPrev = renderGraph.ImportTexture(hist.GetPrevious(1));
            TextureHandle histGeoPrev = renderGraph.ImportTexture(hist.GetPrevious(2));

            // ---- 1. HZB (min eye depth pyramid, levels 1..5) --------------------------------------------
            using (var b = renderGraph.AddComputePass("SSGICore HZB 1", out StepData d))
            {
                d.Begin(SSGICoreResources.Hzb, SSGICoreResources.KHzbFromDepth,
                    SSGICoreUtil.HzbLevelSize(fullW, 1), SSGICoreUtil.HzbLevelSize(fullH, 1), c, 0);
                d.Add(SSGICoreIds.Depth, depth);        b.UseTexture(depth, AccessFlags.Read);
                d.Add(SSGICoreIds.HzbOut, m_Hzb[1]);    b.UseTexture(m_Hzb[1], AccessFlags.Write);
                b.SetRenderFunc(static (StepData data, ComputeGraphContext ctx) => data.Execute(ctx.cmd));
            }

            for (int level = 2; level <= SSGICoreUtil.HzbLevels; level++)
            {
                using (var b = renderGraph.AddComputePass("SSGICore HZB " + level, out StepData d))
                {
                    d.Begin(SSGICoreResources.Hzb, SSGICoreResources.KHzbDown,
                        SSGICoreUtil.HzbLevelSize(fullW, level), SSGICoreUtil.HzbLevelSize(fullH, level), c, 0);
                    d.Add(SSGICoreIds.HzbIn, m_Hzb[level - 1]);   b.UseTexture(m_Hzb[level - 1], AccessFlags.Read);
                    d.Add(SSGICoreIds.HzbOut, m_Hzb[level]);      b.UseTexture(m_Hzb[level], AccessFlags.Write);
                    b.SetRenderFunc(static (StepData data, ComputeGraphContext ctx) => data.Execute(ctx.cmd));
                }
            }

            // ---- 2. Trace (low-res, dithered, multi-bounce) ---------------------------------------------
            using (var b = renderGraph.AddComputePass("SSGICore Trace", out StepData d))
            {
                d.Begin(SSGICoreResources.Trace, SSGICoreResources.KTrace, lowW, lowH, c, 0);
                d.Add(SSGICoreIds.Depth, depth);            b.UseTexture(depth, AccessFlags.Read);
                d.Add(SSGICoreIds.Normals, normalsIn);      b.UseTexture(normalsIn, AccessFlags.Read);
                d.Add(SSGICoreIds.Motion, motionIn);        b.UseTexture(motionIn, AccessFlags.Read);
                d.Add(SSGICoreIds.Color, opaque);           b.UseTexture(opaque, AccessFlags.Read);
                d.Add(SSGICoreIds.PrevColor, prevColorIn);  b.UseTexture(prevColorIn, AccessFlags.Read);
                for (int level = 1; level <= SSGICoreUtil.HzbLevels; level++)
                {
                    d.Add(SSGICoreIds.Hzb[level], m_Hzb[level]);
                    b.UseTexture(m_Hzb[level], AccessFlags.Read);
                }
                d.Add(SSGICoreIds.TraceOut, trace);         b.UseTexture(trace, AccessFlags.Write);
                b.SetRenderFunc(static (StepData data, ComputeGraphContext ctx) => data.Execute(ctx.cmd));
            }

            // ---- 3. Temporal accumulation + variance ----------------------------------------------------
            using (var b = renderGraph.AddComputePass("SSGICore Temporal", out StepData d))
            {
                d.Begin(SSGICoreResources.Temporal, SSGICoreResources.KTemporal, lowW, lowH, c, 0);
                d.Add(SSGICoreIds.TraceIn, trace);              b.UseTexture(trace, AccessFlags.Read);
                d.Add(SSGICoreIds.Depth, depth);                b.UseTexture(depth, AccessFlags.Read);
                d.Add(SSGICoreIds.Normals, normalsIn);          b.UseTexture(normalsIn, AccessFlags.Read);
                d.Add(SSGICoreIds.Motion, motionIn);            b.UseTexture(motionIn, AccessFlags.Read);
                d.Add(SSGICoreIds.HistColorPrev, histColorPrev); b.UseTexture(histColorPrev, AccessFlags.Read);
                d.Add(SSGICoreIds.HistMomPrev, histMomPrev);     b.UseTexture(histMomPrev, AccessFlags.Read);
                d.Add(SSGICoreIds.HistGeoPrev, histGeoPrev);     b.UseTexture(histGeoPrev, AccessFlags.Read);
                d.Add(SSGICoreIds.HistColorOut, histColorCur);   b.UseTexture(histColorCur, AccessFlags.Write);
                d.Add(SSGICoreIds.HistMomOut, histMomCur);       b.UseTexture(histMomCur, AccessFlags.Write);
                d.Add(SSGICoreIds.HistGeoOut, histGeoCur);       b.UseTexture(histGeoCur, AccessFlags.Write);
                d.Add(SSGICoreIds.FilterOut, filterA);           b.UseTexture(filterA, AccessFlags.Write);
                b.AllowPassCulling(false);
                b.SetRenderFunc(static (StepData data, ComputeGraphContext ctx) => data.Execute(ctx.cmd));
            }

            // ---- 4. Variance-guided a-trous filter (ping-pong A <-> B) ----------------------------------
            TextureHandle filtered = filterA;
            int iterations = Mathf.Clamp(cfg.filterIterations, 0, 4);
            for (int i = 0; i < iterations; i++)
            {
                TextureHandle src = (i % 2 == 0) ? filterA : filterB;
                TextureHandle dst = (i % 2 == 0) ? filterB : filterA;
                using (var b = renderGraph.AddComputePass("SSGICore Filter " + i, out StepData d))
                {
                    d.Begin(SSGICoreResources.Filter, SSGICoreResources.KAtrous, lowW, lowH, c, 1 << i);
                    d.Add(SSGICoreIds.Depth, depth);        b.UseTexture(depth, AccessFlags.Read);
                    d.Add(SSGICoreIds.Normals, normalsIn);  b.UseTexture(normalsIn, AccessFlags.Read);
                    d.Add(SSGICoreIds.FilterIn, src);       b.UseTexture(src, AccessFlags.Read);
                    d.Add(SSGICoreIds.FilterOut, dst);      b.UseTexture(dst, AccessFlags.Write);
                    b.SetRenderFunc(static (StepData data, ComputeGraphContext ctx) => data.Execute(ctx.cmd));
                }
                filtered = dst;
            }

            // ---- 5. Depth/normal-aware bilateral upsample -----------------------------------------------
            using (var b = renderGraph.AddComputePass("SSGICore Upsample", out StepData d))
            {
                d.Begin(SSGICoreResources.Upsample, SSGICoreResources.KUpsample, fullW, fullH, c, 0);
                d.Add(SSGICoreIds.Depth, depth);        b.UseTexture(depth, AccessFlags.Read);
                d.Add(SSGICoreIds.Normals, normalsIn);  b.UseTexture(normalsIn, AccessFlags.Read);
                d.Add(SSGICoreIds.FilterIn, filtered);  b.UseTexture(filtered, AccessFlags.Read);
                d.Add(SSGICoreIds.GIOut, giFull);       b.UseTexture(giFull, AccessFlags.Write);
                b.SetRenderFunc(static (StepData data, ComputeGraphContext ctx) => data.Execute(ctx.cmd));
            }

            // ---- 6. Composite (additive, approximate albedo) + user-facing global GI texture ------------
            SSGICoreDebugView debug = vol.debugView.value;
            using (var b = renderGraph.AddRasterRenderPass("SSGICore Composite", out CompositeData d))
            {
                d.gi = giFull;          b.UseTexture(giFull, AccessFlags.Read);
                d.opaque = opaque;      b.UseTexture(opaque, AccessFlags.Read);
                d.depth = depth;        b.UseTexture(depth, AccessFlags.Read);
                d.raw = default;
                d.age = default;
                if (debug == SSGICoreDebugView.RawTrace) { d.raw = trace;         b.UseTexture(trace, AccessFlags.Read); }
                if (debug == SSGICoreDebugView.HistoryAge) { d.age = histColorCur; b.UseTexture(histColorCur, AccessFlags.Read); }

                d.material = SSGICoreResources.Composite;
                d.block = m_Block;
                d.passIndex = debug == SSGICoreDebugView.None ? 0 : 1;
                d.prm = new Vector4(vol.intensity.value, vol.albedoLevel.value, (float)debug, ds);
                d.size = c.fullSize;

                b.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                b.SetGlobalTextureAfterPass(giFull, SSGICoreIds.UserGI);
                b.AllowGlobalStateModification(true);
                b.AllowPassCulling(false);
                b.SetRenderFunc(static (CompositeData data, RasterGraphContext ctx) =>
                {
                    RasterCommandBuffer cmd = ctx.cmd;
                    cmd.SetGlobalTexture(SSGICoreIds.CompGI, data.gi);
                    cmd.SetGlobalTexture(SSGICoreIds.CompOpaque, data.opaque);
                    cmd.SetGlobalTexture(SSGICoreIds.CompDepth, data.depth);
                    if (data.raw.IsValid()) cmd.SetGlobalTexture(SSGICoreIds.CompRaw, data.raw);
                    if (data.age.IsValid()) cmd.SetGlobalTexture(SSGICoreIds.CompAge, data.age);
                    data.block.SetVector(SSGICoreIds.CompParams, data.prm);
                    data.block.SetVector(SSGICoreIds.CompSize, data.size);
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, data.passIndex, MeshTopology.Triangles, 3, 1, data.block);
                });
            }

            // history bookkeeping for next frame
            hist.MarkWritten(Time.frameCount, view, viewProj);
        }

        static void FillAmbient(Vector4[] dst, float intensity)
        {
            RenderSettings.ambientProbe.Evaluate(s_AmbientDirs, s_AmbientColors);
            for (int i = 0; i < 6; i++)
            {
                Color col = s_AmbientColors[i];
                dst[i] = new Vector4(col.r, col.g, col.b, 0f) * intensity;
            }
        }
    }
}
