using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class ShockwaveDistortionRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class Settings
    {
        public bool enabled = true;
        public bool showInSceneView = true;
        [Range(0f, 2f)] public float globalStrength = 1f;
        [Range(0.0001f, 0.03f)] public float darkZoneEdgeSoftness = 0.004f;
    }

    private sealed class ShockwaveDistortionPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private readonly Material material;

        public ShockwaveDistortionPass(Settings settings, Material material)
        {
            this.settings = settings;
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (!settings.enabled || material == null)
            {
                return;
            }

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game &&
                (!settings.showInSceneView ||
                 cameraData.cameraType != CameraType.SceneView))
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer ||
                !ShockwaveDistortionManager2D.ApplyToMaterial(
                    material,
                    cameraData.camera,
                    settings.globalStrength,
                    settings.darkZoneEdgeSoftness))
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
            destinationDescriptor.name = "_ShockwaveDistortionCameraColor";
            destinationDescriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);

            var parameters = new RenderGraphUtils.BlitMaterialParameters(
                source,
                destination,
                material,
                0);
            renderGraph.AddBlitPass(parameters, "Shockwave Screen Distortion");
            resourceData.cameraColor = destination;
        }
    }

    public Settings settings = new Settings();
    [SerializeField] private Shader shader;

    private Material material;
    private ShockwaveDistortionPass pass;

    public Shader Shader
    {
        get => shader;
        set => shader = value;
    }

    public override void Create()
    {
        CoreUtils.Destroy(material);
        if (shader == null)
        {
            shader = Shader.Find("Hidden/WaterAndFire/ShockwaveDistortion2D");
        }

        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new ShockwaveDistortionPass(settings, material);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (pass != null && material != null && settings.enabled)
        {
            renderer.EnqueuePass(pass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }
}
