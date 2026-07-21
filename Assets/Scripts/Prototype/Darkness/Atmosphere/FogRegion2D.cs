using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FogRegion2D : MonoBehaviour
{
    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int FogAlphaId = Shader.PropertyToID("_FogAlpha");
    private static readonly int VerticalBiasId = Shader.PropertyToID("_VerticalBias");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");

    [Header("References")]
    [SerializeField] private Renderer[] staticFogRenderers;
    [SerializeField] private Renderer[] driftingFogRenderers;

    [Header("Region")]
    [SerializeField] private bool regionEnabled = true;
    [SerializeField] private Vector2 regionSize = new Vector2(58f, 28f);
    [SerializeField] private Vector2 regionOffset = new Vector2(0f, -1f);

    [Header("Fog")]
    [SerializeField] private Color staticFogColor = new Color(0.05f, 0.28f, 0.50f, 1f);
    [SerializeField, Range(0f, 1f)] private float staticFogAlpha = 0.18f;
    [SerializeField] private Color driftingFogColor = new Color(0.07f, 0.46f, 0.62f, 1f);
    [SerializeField, Range(0f, 1f)] private float driftingFogAlpha = 0.20f;
    [SerializeField, Range(0f, 1f)] private float fogVerticalBias = 0.90f;

    [Header("Drifting Fog")]
    [SerializeField] private bool enableDriftingFog = true;
    [SerializeField] private Vector2 driftDirection = new Vector2(1f, 0.25f);
    [SerializeField, Min(0f)] private float driftSpeed = 0.012f;

    [Header("Visual")]
    [SerializeField, Min(0f)] private float overallBrightness = 1f;
    [SerializeField, Range(0.25f, 2f)] private float overallContrast = 1.08f;
    [SerializeField] private int staticSortingOrder = -78;
    [SerializeField] private int driftingSortingOrder = -52;

    [Header("Scene Gizmo")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool gizmosOnlyWhenSelected = true;

    private MaterialPropertyBlock propertyBlock;

    public Bounds LocalBounds => new Bounds(regionOffset, regionSize);

    private void OnEnable()
    {
        ApplyRegion();
    }

    private void OnValidate()
    {
        regionSize.x = Mathf.Max(0.1f, regionSize.x);
        regionSize.y = Mathf.Max(0.1f, regionSize.y);
        ApplyRegion();
    }

    [ContextMenu("Apply Fog Region")]
    public void ApplyRegion()
    {
        EnsurePropertyBlock();
        ApplyLayer(staticFogRenderers, regionEnabled, staticFogColor, staticFogAlpha, Vector2.zero, staticSortingOrder);

        Vector2 direction = driftDirection.sqrMagnitude > 0.0001f ? driftDirection.normalized : Vector2.zero;
        Vector2 scroll = direction * driftSpeed;
        ApplyLayer(
            driftingFogRenderers,
            regionEnabled && enableDriftingFog,
            driftingFogColor,
            driftingFogAlpha,
            scroll,
            driftingSortingOrder);
    }

    public void SetRegionEnabled(bool value)
    {
        regionEnabled = value;
        ApplyRegion();
    }

    private void ApplyLayer(Renderer[] renderers, bool visible, Color color, float alpha, Vector2 scroll, int sortingOrder)
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

            renderer.enabled = visible;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.localPosition = new Vector3(regionOffset.x, regionOffset.y, renderer.transform.localPosition.z);
            renderer.transform.localScale = new Vector3(regionSize.x, regionSize.y, 1f);

            propertyBlock.Clear();
            propertyBlock.SetColor(FogColorId, color);
            propertyBlock.SetFloat(FogAlphaId, alpha);
            propertyBlock.SetFloat(VerticalBiasId, fogVerticalBias);
            propertyBlock.SetVector(ScrollSpeedId, new Vector4(scroll.x, scroll.y, 0f, 0f));
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

    private void OnDrawGizmos()
    {
        if (!showGizmos || gizmosOnlyWhenSelected)
        {
            return;
        }

        DrawRegionGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        DrawRegionGizmo();
    }

    private void DrawRegionGizmo()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = regionEnabled ? new Color(0.20f, 0.75f, 0.95f, 0.8f) : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        Gizmos.DrawWireCube(regionOffset, regionSize);

        if (enableDriftingFog && driftDirection.sqrMagnitude > 0.0001f)
        {
            Vector2 direction = driftDirection.normalized;
            Vector3 start = regionOffset;
            Vector3 end = start + (Vector3)(direction * Mathf.Min(regionSize.x, regionSize.y) * 0.18f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.12f);
        }

        Gizmos.matrix = previous;
    }
}
