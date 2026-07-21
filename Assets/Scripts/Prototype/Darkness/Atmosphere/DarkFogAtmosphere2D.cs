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
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");

    [Header("World-Fixed References")]
    [SerializeField] private Renderer backgroundRenderer;
    [SerializeField] private Transform farSilhouetteRoot;
    [SerializeField] private Transform midSilhouetteRoot;
    [SerializeField] private Renderer[] farSilhouetteRenderers;
    [SerializeField] private Renderer[] midSilhouetteRenderers;

    [Header("Background")]
    [SerializeField] private bool showBackground = true;
    [SerializeField] private Vector2 backgroundSize = new Vector2(58f, 32f);
    [SerializeField] private Vector2 backgroundOffset = Vector2.zero;
    [SerializeField] private int backgroundSortingOrder = -100;
    [SerializeField] private Color topColor = new Color(0.002f, 0.008f, 0.045f, 1f);
    [SerializeField] private Color middleColor = new Color(0.012f, 0.07f, 0.21f, 1f);
    [SerializeField] private Color bottomColor = new Color(0.02f, 0.35f, 0.56f, 1f);

    [Header("Silhouette Layers")]
    [SerializeField] private bool autoCollectSilhouetteRenderers = true;
    [SerializeField] private bool applyAutomaticSilhouetteTint = true;
    [SerializeField] private Color farSilhouetteTint = new Color(0.18f, 0.34f, 0.52f, 0.55f);
    [SerializeField] private Color midSilhouetteTint = new Color(0.055f, 0.105f, 0.18f, 0.88f);
    [SerializeField] private bool applySilhouetteSortingOrders = true;
    [SerializeField] private int farSilhouetteSortingOrder = -88;
    [SerializeField] private int midSilhouetteSortingOrder = -68;

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
        backgroundSize.x = Mathf.Max(0.1f, backgroundSize.x);
        backgroundSize.y = Mathf.Max(0.1f, backgroundSize.y);
        overallBrightness = Mathf.Max(0f, overallBrightness);
        overallContrast = Mathf.Clamp(overallContrast, 0.25f, 2f);
        ApplyAtmosphere();
    }

    [ContextMenu("Apply World-Fixed Background")]
    public void ApplyAtmosphere()
    {
        EnsurePropertyBlock();
        if (autoCollectSilhouetteRenderers)
        {
            CollectSilhouetteRenderers();
        }

        ApplyBackground();
        ApplySilhouetteLayer(farSilhouetteRenderers, farSilhouetteTint, farSilhouetteSortingOrder);
        ApplySilhouetteLayer(midSilhouetteRenderers, midSilhouetteTint, midSilhouetteSortingOrder);
    }

    [ContextMenu("Refresh Silhouette Renderers")]
    public void RefreshSilhouetteRenderers()
    {
        CollectSilhouetteRenderers();
        ApplyAtmosphere();
    }

    private void ApplyBackground()
    {
        if (backgroundRenderer == null)
        {
            return;
        }

        backgroundRenderer.enabled = showBackground;
        backgroundRenderer.sortingOrder = backgroundSortingOrder;
        backgroundRenderer.transform.localPosition = new Vector3(
            backgroundOffset.x,
            backgroundOffset.y,
            backgroundRenderer.transform.localPosition.z);
        backgroundRenderer.transform.localScale = new Vector3(backgroundSize.x, backgroundSize.y, 1f);
        propertyBlock.Clear();
        propertyBlock.SetColor(TopColorId, topColor);
        propertyBlock.SetColor(MiddleColorId, middleColor);
        propertyBlock.SetColor(BottomColorId, bottomColor);
        propertyBlock.SetFloat(BrightnessId, overallBrightness);
        propertyBlock.SetFloat(ContrastId, overallContrast);
        backgroundRenderer.SetPropertyBlock(propertyBlock);
    }

    private void CollectSilhouetteRenderers()
    {
        farSilhouetteRenderers = farSilhouetteRoot != null
            ? farSilhouetteRoot.GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();
        midSilhouetteRenderers = midSilhouetteRoot != null
            ? midSilhouetteRoot.GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();
    }

    private void ApplySilhouetteLayer(Renderer[] renderers, Color tint, int sortingOrder)
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
            if (applySilhouetteSortingOrders)
            {
                renderer.sortingOrder = sortingOrder;
            }

            if (!applyAutomaticSilhouetteTint)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetColor(ColorId, tint);
            propertyBlock.SetColor(BaseColorId, tint);
            propertyBlock.SetColor(RendererColorId, tint);
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
