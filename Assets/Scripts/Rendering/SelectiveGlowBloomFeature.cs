using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class SelectiveGlowBloomFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask glowLayer = 1 << 8;
        [Range(0f, 4f)] public float threshold = 0.05f;
        [Range(0f, 8f)] public float intensity = 1.35f;
        [Range(1, 8)] public int iterations = 4;
        [Range(1f, 8f)] public float blurRadius = 2.2f;
        [Range(1, 4)] public int downsample = 2;
        [Range(0f, 4f)] public float compositeIntensity = 1f;
    }

    private sealed class SelectiveGlowBloomPass : ScriptableRenderPass
    {
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int CompositeIntensityId = Shader.PropertyToID("_CompositeIntensity");
        private static readonly Vector4 FullscreenScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private readonly Settings settings;
        private readonly Material material;
        private readonly ProfilingSampler selectiveGlowSampler = new ProfilingSampler("Selective Glow Bloom");
        private readonly List<ShaderTagId> shaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("Universal2D"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        private sealed class DrawGlowPassData
        {
            public RendererListHandle rendererList;
        }

        private sealed class CompositePassData
        {
            public TextureHandle glowTexture;
            public Material material;
        }

        public SelectiveGlowBloomPass(Settings settings, Material material)
        {
            this.settings = settings;
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null || settings.glowLayer.value == 0)
            {
                return;
            }

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            RenderTextureDescriptor glowDescriptor = cameraData.cameraTargetDescriptor;
            int downsample = Mathf.Max(1, settings.downsample);
            glowDescriptor.width = Mathf.Max(1, glowDescriptor.width / downsample);
            glowDescriptor.height = Mathf.Max(1, glowDescriptor.height / downsample);
            glowDescriptor.msaaSamples = 1;
            glowDescriptor.depthBufferBits = 0;
            glowDescriptor.depthStencilFormat = GraphicsFormat.None;

            TextureHandle glowTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                glowDescriptor,
                "_SelectiveGlowBloom_Source",
                true,
                FilterMode.Bilinear);

            TextureHandle pingTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                glowDescriptor,
                "_SelectiveGlowBloom_Ping",
                false,
                FilterMode.Bilinear);

            TextureHandle pongTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                glowDescriptor,
                "_SelectiveGlowBloom_Pong",
                false,
                FilterMode.Bilinear);

            RecordGlowLayerPass(renderGraph, frameData, glowTexture);
            RecordBlurPasses(renderGraph, glowTexture, ref pingTexture, ref pongTexture);
            RecordCompositePass(renderGraph, frameData, pingTexture);
        }

        private void RecordGlowLayerPass(RenderGraph renderGraph, ContextContainer frameData, TextureHandle glowTexture)
        {
            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<DrawGlowPassData>(
                       "Selective Glow Draw Layer",
                       out DrawGlowPassData passData,
                       selectiveGlowSampler))
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    shaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonTransparent);

                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.glowLayer);
                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings);

                passData.rendererList = renderGraph.CreateRendererList(rendererListParams);

                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(glowTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (DrawGlowPassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }

        private void RecordBlurPasses(RenderGraph renderGraph, TextureHandle sourceTexture, ref TextureHandle pingTexture, ref TextureHandle pongTexture)
        {
            material.SetFloat(ThresholdId, settings.threshold);
            material.SetFloat(IntensityId, settings.intensity);
            material.SetFloat(BlurRadiusId, settings.blurRadius);
            material.SetFloat(CompositeIntensityId, settings.compositeIntensity);

            RenderGraphUtils.BlitMaterialParameters prefilterParameters = new RenderGraphUtils.BlitMaterialParameters(
                sourceTexture,
                pingTexture,
                material,
                0);
            renderGraph.AddBlitPass(prefilterParameters, "Selective Glow Prefilter");

            int iterations = Mathf.Max(1, settings.iterations);
            for (int i = 0; i < iterations; i++)
            {
                RenderGraphUtils.BlitMaterialParameters blurToPong = new RenderGraphUtils.BlitMaterialParameters(
                    pingTexture,
                    pongTexture,
                    material,
                    1);
                renderGraph.AddBlitPass(blurToPong, $"Selective Glow Blur {i + 1}A");

                RenderGraphUtils.BlitMaterialParameters blurToPing = new RenderGraphUtils.BlitMaterialParameters(
                    pongTexture,
                    pingTexture,
                    material,
                    1);
                renderGraph.AddBlitPass(blurToPing, $"Selective Glow Blur {i + 1}B");
            }
        }

        private void RecordCompositePass(RenderGraph renderGraph, ContextContainer frameData, TextureHandle glowTexture)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                       "Selective Glow Composite",
                       out CompositePassData passData,
                       selectiveGlowSampler))
            {
                passData.glowTexture = glowTexture;
                passData.material = material;

                builder.UseTexture(passData.glowTexture);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.glowTexture, FullscreenScaleBias, data.material, 2);
                });
            }
        }
    }

    public Settings settings = new Settings();
    [SerializeField] private Shader shader;
    private Material material;
    private SelectiveGlowBloomPass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/WaterAndFire/SelectiveGlowBloom");
        }

        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new SelectiveGlowBloomPass(settings, material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass != null && material != null)
        {
            renderer.EnqueuePass(pass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }
}
