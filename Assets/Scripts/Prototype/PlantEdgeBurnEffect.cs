using UnityEngine;

[DisallowMultipleComponent]
public class PlantEdgeBurnEffect : MonoBehaviour
{
    private const string ShaderName = "WaterAndFire/SpritePlantEdgeBurn";

    private static readonly int BurnProgressId = Shader.PropertyToID("_BurnProgress");
    private static readonly int HotColorId = Shader.PropertyToID("_HotColor");
    private static readonly int FlameColorId = Shader.PropertyToID("_FlameColor");
    private static readonly int CharColorId = Shader.PropertyToID("_CharColor");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int FrontWidthId = Shader.PropertyToID("_FrontWidth");
    private static readonly int OuterFlameWidthId = Shader.PropertyToID("_OuterFlameWidth");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
    private static readonly int MeltAmountId = Shader.PropertyToID("_MeltAmount");
    private static readonly int DripLengthId = Shader.PropertyToID("_DripLength");
    private static readonly int DripScaleId = Shader.PropertyToID("_DripScale");
    private static readonly int GeometryExpansionId = Shader.PropertyToID("_GeometryExpansion");
    private static readonly int CharOpacityId = Shader.PropertyToID("_CharOpacity");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    [Header("Timing")]
    [SerializeField, Min(0f)] private float burnDuration = 0.8f;

    [Header("Fire")]
    [ColorUsage(false, true)] [SerializeField] private Color hotColor = new Color(1f, 0.92f, 0.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color flameColor = new Color(1f, 0.25f, 0.015f, 1f);
    [SerializeField] private Color charColor = new Color(0.055f, 0.018f, 0.008f, 1f);
    [Range(0.5f, 8f)] [SerializeField] private float edgeWidth = 2f;
    [Range(0.02f, 0.35f)] [SerializeField] private float frontWidth = 0.12f;
    [Range(0.5f, 10f)] [SerializeField] private float outerFlameWidth = 3f;
    [Range(1f, 40f)] [SerializeField] private float noiseScale = 13f;
    [Range(0f, 10f)] [SerializeField] private float noiseSpeed = 3.5f;
    [Range(0f, 0.35f)] [SerializeField] private float noiseAmount = 0.14f;
    [Range(0f, 0.5f)] [SerializeField] private float meltAmount = 0.24f;
    [Range(0f, 0.35f)] [SerializeField] private float dripLength = 0.16f;
    [Range(3f, 40f)] [SerializeField] private float dripScale = 18f;
    [Range(1f, 1.4f)] [SerializeField] private float geometryExpansion = 1.18f;
    [Range(0f, 1f)] [SerializeField] private float charOpacity = 0.72f;
    [Range(0f, 8f)] [SerializeField] private float emissionIntensity = 3f;
    [Range(1f, 1.15f)] [SerializeField] private float overlayScale = 1.035f;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer overlayRenderer;
    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;

    public float Duration => burnDuration;

    public void Configure(SpriteRenderer source)
    {
        sourceRenderer = source;
    }

    public bool BeginBurn()
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
        propertyBlock.SetFloat(BurnProgressId, Mathf.Clamp01(progress));
        propertyBlock.SetColor(HotColorId, hotColor);
        propertyBlock.SetColor(FlameColorId, flameColor);
        propertyBlock.SetColor(CharColorId, charColor);
        propertyBlock.SetFloat(EdgeWidthId, edgeWidth);
        propertyBlock.SetFloat(FrontWidthId, frontWidth);
        propertyBlock.SetFloat(OuterFlameWidthId, outerFlameWidth);
        propertyBlock.SetFloat(NoiseScaleId, noiseScale);
        propertyBlock.SetFloat(NoiseSpeedId, noiseSpeed);
        propertyBlock.SetFloat(NoiseAmountId, noiseAmount);
        propertyBlock.SetFloat(MeltAmountId, meltAmount);
        propertyBlock.SetFloat(DripLengthId, dripLength);
        propertyBlock.SetFloat(DripScaleId, dripScale);
        propertyBlock.SetFloat(GeometryExpansionId, geometryExpansion);
        propertyBlock.SetFloat(CharOpacityId, charOpacity);
        propertyBlock.SetFloat(EmissionIntensityId, emissionIntensity);
        propertyBlock.SetFloat(OpacityId, 1f);
        overlayRenderer.SetPropertyBlock(propertyBlock);
    }

    public void EndBurn()
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
            Debug.LogWarning("Plant edge burn shader not found: " + ShaderName, this);
            return;
        }

        Transform overlayTransform = sourceRenderer.transform.Find("BurnEdgeOverlay");
        if (overlayTransform == null)
        {
            var overlayObject = new GameObject("BurnEdgeOverlay");
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
            name = "Runtime_PlantEdgeBurn"
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
        overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + 2;
        overlayRenderer.maskInteraction = sourceRenderer.maskInteraction;
        overlayRenderer.transform.localScale = Vector3.one * overlayScale;
    }

    private void OnDisable()
    {
        EndBurn();
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
