using UnityEngine;

[DisallowMultipleComponent]
public class PlantGrowthEdgeEffect : MonoBehaviour
{
    private const string ShaderName = "WaterAndFire/SpritePlantGrowthEdge";

    private static readonly int GrowthProgressId = Shader.PropertyToID("_GrowthProgress");
    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly int GrowthColorId = Shader.PropertyToID("_GrowthColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int FragmentScaleId = Shader.PropertyToID("_FragmentScale");
    private static readonly int FragmentEdgeWidthId = Shader.PropertyToID("_FragmentEdgeWidth");
    private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
    private static readonly int EdgeNoiseAmountId = Shader.PropertyToID("_EdgeNoiseAmount");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int OuterGlowWidthId = Shader.PropertyToID("_OuterGlowWidth");
    private static readonly int GeometryExpansionId = Shader.PropertyToID("_GeometryExpansion");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    [Header("Timing")]
    [SerializeField, Min(0f)] private float growthDuration = 0.65f;

    [Header("Growth Glow")]
    [ColorUsage(false, true)] [SerializeField] private Color coreColor = new Color(0.12f, 1.25f, 0.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color growthColor = new Color(0.025f, 0.72f, 0.08f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color edgeColor = new Color(0.008f, 0.22f, 0.025f, 1f);
    [Range(2f, 40f)] [SerializeField] private float fragmentScale = 14f;
    [Range(0.01f, 0.2f)] [SerializeField] private float fragmentEdgeWidth = 0.035f;
    [Range(0.5f, 8f)] [SerializeField] private float rimWidth = 1.2f;
    [Range(0f, 1f)] [SerializeField] private float edgeNoiseAmount = 0.45f;
    [Range(0f, 5f)] [SerializeField] private float flowSpeed = 0.75f;
    [Range(0.5f, 10f)] [SerializeField] private float outerGlowWidth = 1.5f;
    [Range(1f, 1.4f)] [SerializeField] private float geometryExpansion = 1.14f;
    [Range(0f, 8f)] [SerializeField] private float emissionIntensity = 1.4f;
    [Range(0f, 1f)] [SerializeField] private float effectOpacity = 0.64f;
    [Range(1f, 1.15f)] [SerializeField] private float overlayScale = 1.02f;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer overlayRenderer;
    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;

    public float Duration => growthDuration;

    public void Configure(SpriteRenderer source)
    {
        sourceRenderer = source;
    }

    public bool BeginGrowth()
    {
        if (sourceRenderer == null)
        {
            return false;
        }

        EnsureOverlay();
        if (overlayRenderer == null || runtimeMaterial == null)
        {
            return false;
        }

        SyncOverlay();
        overlayRenderer.enabled = true;
        SetProgress(0f);
        return true;
    }

    public void SetProgress(float progress)
    {
        if (overlayRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        overlayRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(GrowthProgressId, Mathf.Clamp01(progress));
        propertyBlock.SetColor(CoreColorId, coreColor);
        propertyBlock.SetColor(GrowthColorId, growthColor);
        propertyBlock.SetColor(EdgeColorId, edgeColor);
        propertyBlock.SetFloat(FragmentScaleId, fragmentScale);
        propertyBlock.SetFloat(FragmentEdgeWidthId, fragmentEdgeWidth);
        propertyBlock.SetFloat(RimWidthId, rimWidth);
        propertyBlock.SetFloat(EdgeNoiseAmountId, edgeNoiseAmount);
        propertyBlock.SetFloat(FlowSpeedId, flowSpeed);
        propertyBlock.SetFloat(OuterGlowWidthId, outerGlowWidth);
        propertyBlock.SetFloat(GeometryExpansionId, geometryExpansion);
        propertyBlock.SetFloat(EmissionIntensityId, emissionIntensity);
        propertyBlock.SetFloat(OpacityId, effectOpacity);
        overlayRenderer.SetPropertyBlock(propertyBlock);
    }

    public void EndGrowth()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
        }
    }

    private void EnsureOverlay()
    {
        if (overlayRenderer != null && runtimeMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning("Plant growth edge shader not found: " + ShaderName, this);
            return;
        }

        Transform overlayTransform = sourceRenderer.transform.Find("GrowthEdgeOverlay");
        if (overlayTransform == null)
        {
            var overlayObject = new GameObject("GrowthEdgeOverlay");
            overlayTransform = overlayObject.transform;
            overlayTransform.SetParent(sourceRenderer.transform, false);
        }

        overlayTransform.localPosition = Vector3.zero;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one * overlayScale;

        overlayRenderer = overlayTransform.GetComponent<SpriteRenderer>();
        if (overlayRenderer == null)
        {
            overlayRenderer = overlayTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        int glowLayer = LayerMask.NameToLayer("Glow");
        if (glowLayer >= 0)
        {
            overlayRenderer.gameObject.layer = glowLayer;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime_PlantGrowthEdge"
        };
        overlayRenderer.material = runtimeMaterial;
        overlayRenderer.enabled = false;
        propertyBlock = new MaterialPropertyBlock();
    }

    private void SyncOverlay()
    {
        overlayRenderer.sprite = sourceRenderer.sprite;
        overlayRenderer.color = Color.white;
        overlayRenderer.flipX = sourceRenderer.flipX;
        overlayRenderer.flipY = sourceRenderer.flipY;
        overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + 3;
        overlayRenderer.maskInteraction = sourceRenderer.maskInteraction;
        overlayRenderer.transform.localScale = Vector3.one * overlayScale;
    }

    private void OnDisable()
    {
        EndGrowth();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }
    }
}
