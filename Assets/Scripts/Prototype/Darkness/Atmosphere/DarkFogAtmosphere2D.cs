using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DarkFogAtmosphere2D : MonoBehaviour
{
    private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    private static readonly int MiddleColorId = Shader.PropertyToID("_MiddleColor");
    private static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");
    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int FogAlphaId = Shader.PropertyToID("_FogAlpha");
    private static readonly int VerticalBiasId = Shader.PropertyToID("_VerticalBias");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform backgroundRoot;
    [SerializeField] private Transform farLayerRoot;
    [SerializeField] private Transform midLayerRoot;
    [SerializeField] private Transform nearLayerRoot;
    [SerializeField] private Renderer backgroundRenderer;
    [SerializeField] private Renderer[] farFogRenderers;
    [SerializeField] private Renderer[] midFogRenderers;
    [SerializeField] private Renderer[] nearFogRenderers;
    [SerializeField] private Renderer[] farSilhouetteRenderers;
    [SerializeField] private Renderer[] midSilhouetteRenderers;

    [Header("Background")]
    [SerializeField] private Color topColor = new Color(0.002f, 0.008f, 0.045f, 1f);
    [SerializeField] private Color middleColor = new Color(0.012f, 0.07f, 0.21f, 1f);
    [SerializeField] private Color bottomColor = new Color(0.02f, 0.35f, 0.56f, 1f);

    [Header("Fog")]
    [SerializeField] private Color farFogColor = new Color(0.06f, 0.22f, 0.45f, 1f);
    [SerializeField] private Color midFogColor = new Color(0.06f, 0.38f, 0.58f, 1f);
    [SerializeField] private Color nearFogColor = new Color(0.08f, 0.52f, 0.65f, 1f);
    [SerializeField, Range(0f, 1f)] private float farFogAlpha = 0.22f;
    [SerializeField, Range(0f, 1f)] private float midFogAlpha = 0.22f;
    [SerializeField, Range(0f, 1f)] private float nearFogAlpha = 0.26f;
    [SerializeField, Range(0f, 1f)] private float fogVerticalBias = 0.90f;
    [SerializeField] private float fogScrollSpeedX = 0.012f;
    [SerializeField] private float fogScrollSpeedY = 0.004f;

    [Header("Parallax")]
    [SerializeField, Range(0f, 1f)] private float farParallaxMultiplier = 0.04f;
    [SerializeField, Range(0f, 1f)] private float midParallaxMultiplier = 0.10f;
    [SerializeField, Range(0f, 1f)] private float nearParallaxMultiplier = 0.18f;

    [Header("General")]
    [SerializeField, Min(0f)] private float overallBrightness = 1f;
    [SerializeField, Range(0.25f, 2f)] private float overallContrast = 1.08f;
    [SerializeField] private bool followCamera = true;

    private MaterialPropertyBlock propertyBlock;
    private Vector3 cameraOrigin;
    private Vector3 backgroundOrigin;
    private Vector3 farOrigin;
    private Vector3 midOrigin;
    private Vector3 nearOrigin;
    private Camera cachedCamera;
    private bool originsCaptured;

    private void OnEnable()
    {
        ResolveCamera();
        CaptureOrigins();
        ApplyAtmosphere();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveCamera();
        if (!originsCaptured)
        {
            CaptureOrigins();
        }

        ApplyAtmosphere();
        ApplyParallax();
    }

    private void OnValidate()
    {
        farFogAlpha = Mathf.Clamp01(farFogAlpha);
        midFogAlpha = Mathf.Clamp01(midFogAlpha);
        nearFogAlpha = Mathf.Clamp01(nearFogAlpha);
        fogVerticalBias = Mathf.Clamp01(fogVerticalBias);
        overallBrightness = Mathf.Max(0f, overallBrightness);
        overallContrast = Mathf.Clamp(overallContrast, 0.25f, 2f);
        ApplyAtmosphere();
    }

    [ContextMenu("Apply Atmosphere Now")]
    public void ApplyAtmosphere()
    {
        EnsurePropertyBlock();
        ApplyBackground();
        Vector4 scroll = new Vector4(fogScrollSpeedX, fogScrollSpeedY, 0f, 0f);
        ApplyFog(farFogRenderers, farFogColor, farFogAlpha, scroll);
        ApplyFog(midFogRenderers, midFogColor, midFogAlpha, scroll * 1.35f);
        ApplyFog(nearFogRenderers, nearFogColor, nearFogAlpha, scroll * 1.8f);

        Color farSilhouette = Color.Lerp(middleColor, farFogColor, 0.28f) * 0.62f;
        farSilhouette.a = 0.50f;
        Color midSilhouette = Color.Lerp(topColor, Color.black, 0.55f);
        midSilhouette.a = 0.96f;
        ApplySilhouettes(farSilhouetteRenderers, farSilhouette);
        ApplySilhouettes(midSilhouetteRenderers, midSilhouette);
    }

    [ContextMenu("Recapture Parallax Origin")]
    public void RecaptureParallaxOrigin()
    {
        originsCaptured = false;
        CaptureOrigins();
    }

    private void ApplyBackground()
    {
        if (backgroundRenderer == null)
        {
            return;
        }

        propertyBlock.Clear();
        propertyBlock.SetColor(TopColorId, topColor);
        propertyBlock.SetColor(MiddleColorId, middleColor);
        propertyBlock.SetColor(BottomColorId, bottomColor);
        propertyBlock.SetFloat(BrightnessId, overallBrightness);
        propertyBlock.SetFloat(ContrastId, overallContrast);
        backgroundRenderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ApplyFog(Renderer[] renderers, Color color, float alpha, Vector4 scroll)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(FogColorId, color);
            propertyBlock.SetFloat(FogAlphaId, alpha);
            propertyBlock.SetFloat(VerticalBiasId, fogVerticalBias);
            propertyBlock.SetVector(ScrollSpeedId, scroll);
            propertyBlock.SetFloat(BrightnessId, overallBrightness);
            propertyBlock.SetFloat(ContrastId, overallContrast);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplySilhouettes(Renderer[] renderers, Color color)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, color);
            propertyBlock.SetFloat(BrightnessId, overallBrightness);
            propertyBlock.SetFloat(ContrastId, overallContrast);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyParallax()
    {
        if (!followCamera || cachedCamera == null || !originsCaptured)
        {
            return;
        }

        Vector3 cameraDelta = cachedCamera.transform.position - cameraOrigin;
        cameraDelta.z = 0f;
        SetLayerPosition(backgroundRoot, backgroundOrigin, cameraDelta);
        SetLayerPosition(farLayerRoot, farOrigin, cameraDelta * farParallaxMultiplier);
        SetLayerPosition(midLayerRoot, midOrigin, cameraDelta * midParallaxMultiplier);
        SetLayerPosition(nearLayerRoot, nearOrigin, cameraDelta * nearParallaxMultiplier);
    }

    private static void SetLayerPosition(Transform layer, Vector3 origin, Vector3 offset)
    {
        if (layer != null)
        {
            layer.localPosition = origin + offset;
        }
    }

    private void ResolveCamera()
    {
        Camera desired = targetCamera != null ? targetCamera : Camera.main;
        if (desired == cachedCamera)
        {
            return;
        }

        cachedCamera = desired;
        originsCaptured = false;
    }

    private void CaptureOrigins()
    {
        if (cachedCamera == null)
        {
            return;
        }

        cameraOrigin = cachedCamera.transform.position;
        backgroundOrigin = backgroundRoot != null ? backgroundRoot.localPosition : Vector3.zero;
        farOrigin = farLayerRoot != null ? farLayerRoot.localPosition : Vector3.zero;
        midOrigin = midLayerRoot != null ? midLayerRoot.localPosition : Vector3.zero;
        nearOrigin = nearLayerRoot != null ? nearLayerRoot.localPosition : Vector3.zero;
        originsCaptured = true;
    }
}
