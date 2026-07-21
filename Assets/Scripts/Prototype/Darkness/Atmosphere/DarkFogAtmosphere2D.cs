using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DarkFogAtmosphere2D : MonoBehaviour
{
    private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    private static readonly int MiddleColorId = Shader.PropertyToID("_MiddleColor");
    private static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    [Header("World-Fixed References")]
    [SerializeField] private Renderer backgroundRenderer;
    [SerializeField] private Renderer[] farSilhouetteRenderers;
    [SerializeField] private Renderer[] midSilhouetteRenderers;

    [Header("Background")]
    [SerializeField] private bool showBackground = true;
    [SerializeField] private Color topColor = new Color(0.002f, 0.008f, 0.045f, 1f);
    [SerializeField] private Color middleColor = new Color(0.012f, 0.07f, 0.21f, 1f);
    [SerializeField] private Color bottomColor = new Color(0.02f, 0.35f, 0.56f, 1f);

    [Header("General")]
    [SerializeField, Min(0f)] private float overallBrightness = 1f;
    [SerializeField, Range(0.25f, 2f)] private float overallContrast = 1.08f;

    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        ApplyAtmosphere();
    }

    private void OnValidate()
    {
        overallBrightness = Mathf.Max(0f, overallBrightness);
        overallContrast = Mathf.Clamp(overallContrast, 0.25f, 2f);
        ApplyAtmosphere();
    }

    [ContextMenu("Apply World-Fixed Background")]
    public void ApplyAtmosphere()
    {
        EnsurePropertyBlock();
        ApplyBackground();

        Color farSilhouette = Color.Lerp(middleColor, topColor, 0.18f) * 0.62f;
        farSilhouette.a = 0.50f;
        Color midSilhouette = Color.Lerp(topColor, Color.black, 0.55f);
        midSilhouette.a = 0.96f;
        ApplySilhouettes(farSilhouetteRenderers, farSilhouette);
        ApplySilhouettes(midSilhouetteRenderers, midSilhouette);
    }

    private void ApplyBackground()
    {
        if (backgroundRenderer == null)
        {
            return;
        }

        backgroundRenderer.enabled = showBackground;
        propertyBlock.Clear();
        propertyBlock.SetColor(TopColorId, topColor);
        propertyBlock.SetColor(MiddleColorId, middleColor);
        propertyBlock.SetColor(BottomColorId, bottomColor);
        propertyBlock.SetFloat(BrightnessId, overallBrightness);
        propertyBlock.SetFloat(ContrastId, overallContrast);
        backgroundRenderer.SetPropertyBlock(propertyBlock);
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

            renderer.enabled = showBackground;
            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, color);
            propertyBlock.SetFloat(BrightnessId, overallBrightness);
            propertyBlock.SetFloat(ContrastId, overallContrast);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }
}
