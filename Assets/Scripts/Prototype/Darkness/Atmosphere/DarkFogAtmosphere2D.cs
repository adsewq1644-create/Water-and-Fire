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
    [SerializeField] private Color topColor = new Color(0.018f, 0.055f, 0.13f, 1f);
    [SerializeField] private Color middleColor = new Color(0.035f, 0.18f, 0.34f, 1f);
    [SerializeField] private Color bottomColor = new Color(0.08f, 0.43f, 0.58f, 1f);

    [Header("Silhouette Layers")]
    [SerializeField] private bool autoCollectSilhouetteRenderers = true;
    [SerializeField] private bool applyAutomaticSilhouetteTint = true;
    [SerializeField] private Color farSilhouetteTint = new Color(0.36f, 0.56f, 0.70f, 0.68f);
    [SerializeField] private Color midSilhouetteTint = new Color(0.16f, 0.32f, 0.48f, 0.90f);
    [SerializeField] private bool applySilhouetteSortingOrders = true;
    [SerializeField] private int farSilhouetteSortingOrder = -88;
    [SerializeField] private int midSilhouetteSortingOrder = -68;
    [SerializeField] private bool useHierarchyOrderWithinLayer = true;
    [SerializeField, Min(1)] private int silhouetteSortingOrderStep = 1;

    [Header("General")]
    [SerializeField, Min(0f)] private float overallBrightness = 1.08f;
    [SerializeField, Range(0.25f, 2f)] private float overallContrast = 1.03f;

    private MaterialPropertyBlock propertyBlock;
    private int cachedSilhouetteSignature = int.MinValue;
    private bool applyRequested = true;
    private bool isApplying;

    private void OnEnable()
    {
        applyRequested = true;
        cachedSilhouetteSignature = int.MinValue;
    }

    private void Update()
    {
        if (applyRequested)
        {
            applyRequested = false;
            ApplyAtmosphere();
            return;
        }

        if (Application.isPlaying || !autoCollectSilhouetteRenderers)
        {
            return;
        }

        int signature = CalculateSilhouetteSignature();
        if (signature != cachedSilhouetteSignature)
        {
            ApplyAtmosphere();
        }
    }

    private void OnValidate()
    {
        backgroundSize.x = Mathf.Max(0.1f, backgroundSize.x);
        backgroundSize.y = Mathf.Max(0.1f, backgroundSize.y);
        overallBrightness = Mathf.Max(0f, overallBrightness);
        overallContrast = Mathf.Clamp(overallContrast, 0.25f, 2f);
        silhouetteSortingOrderStep = Mathf.Max(1, silhouetteSortingOrderStep);
        applyRequested = true;
        cachedSilhouetteSignature = int.MinValue;
    }

    [ContextMenu("Apply World-Fixed Background")]
    public void ApplyAtmosphere()
    {
        if (isApplying)
        {
            return;
        }

        isApplying = true;
        try
        {
            EnsurePropertyBlock();
            if (autoCollectSilhouetteRenderers)
            {
                CollectSilhouetteRenderers();
            }

            ApplyBackground();
            ApplySilhouetteLayer(farSilhouetteRenderers, farSilhouetteTint, farSilhouetteSortingOrder);
            ApplySilhouetteLayer(midSilhouetteRenderers, midSilhouetteTint, midSilhouetteSortingOrder);
            cachedSilhouetteSignature = CalculateSilhouetteSignature();
        }
        finally
        {
            isApplying = false;
        }
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

        if (backgroundRenderer.enabled != showBackground)
        {
            backgroundRenderer.enabled = showBackground;
        }

        if (backgroundRenderer.sortingOrder != backgroundSortingOrder)
        {
            backgroundRenderer.sortingOrder = backgroundSortingOrder;
        }
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

            int targetSortingOrder = sortingOrder;
            if (useHierarchyOrderWithinLayer)
            {
                // The first renderer in the hierarchy is drawn in front of later siblings.
                targetSortingOrder += (renderers.Length - 1 - i) * silhouetteSortingOrderStep;
            }

            if (applySilhouetteSortingOrders && renderer.sortingOrder != targetSortingOrder)
            {
                renderer.sortingOrder = targetSortingOrder;
            }

            if (!applyAutomaticSilhouetteTint)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            propertyBlock.Clear();
            if (renderer is SpriteRenderer)
            {
                propertyBlock.SetColor(RendererColorId, tint);
            }
            else
            {
                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId))
                {
                    propertyBlock.SetColor(BaseColorId, tint);
                }
                else if (material != null && material.HasProperty(ColorId))
                {
                    propertyBlock.SetColor(ColorId, tint);
                }
                else
                {
                    propertyBlock.SetColor(TintId, tint);
                }
            }
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

    private int CalculateSilhouetteSignature()
    {
        unchecked
        {
            int signature = 17;
            signature = AddRendererSignature(signature, farSilhouetteRoot);
            signature = AddRendererSignature(signature, midSilhouetteRoot);
            return signature;
        }
    }

    private static int AddRendererSignature(int signature, Transform root)
    {
        if (root == null)
        {
            return signature * 31;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        signature = signature * 31 + renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
        {
            signature = signature * 31 + renderers[i].GetInstanceID();
        }

        return signature;
    }
}
