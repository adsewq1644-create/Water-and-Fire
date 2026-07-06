using UnityEngine;

[DisallowMultipleComponent]
public class ShadowRockSilhouette : MonoBehaviour
{
    private const int MaxShaderRevealSources = 8;
    private static readonly int HiddenAlphaId = Shader.PropertyToID("_HiddenAlpha");
    private static readonly int RevealedAlphaId = Shader.PropertyToID("_RevealedAlpha");
    private static readonly int RevealSoftnessId = Shader.PropertyToID("_RevealSoftness");
    private static readonly int RevealSourceCountId = Shader.PropertyToID("_RevealSourceCount");
    private static readonly int RevealSourcesId = Shader.PropertyToID("_RevealSources");

    [SerializeField] private SpriteRenderer silhouetteRenderer;
    [SerializeField, Range(0f, 1f)] private float fireSilhouetteAlpha = 0.28f;
    [SerializeField] private bool useLightBasedVisibility = true;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.025f;
    [SerializeField, Range(0f, 1f)] private float waterSilhouetteAlpha = 0.14f;
    [SerializeField] private float waterVisibilityRadius = 2.7f;
    [SerializeField] private float fireVisibilityRadius = 7f;
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private bool debugGizmos = true;

    [Header("Partial Reveal")]
    [SerializeField] private bool usePartialReveal = true;
    [SerializeField] private Material partialRevealMaterial;
    [SerializeField] private float partialRevealSoftness = 0.85f;
    [SerializeField, Range(1, MaxShaderRevealSources)] private int maxRevealSources = 8;

    private readonly Vector4[] revealSourceData = new Vector4[MaxShaderRevealSources];
    private float currentAlpha;
    private float currentPartialRevealAmount;
    private Material runtimePartialRevealMaterial;
    private Material originalMaterial;
    private MaterialPropertyBlock propertyBlock;
    private Shader cachedPartialRevealShader;
    private bool partialRevealShaderSearched;

    private void Reset()
    {
        AutoAssignSilhouette();
    }

    private void Awake()
    {
        AutoAssignSilhouette();
        CacheOriginalMaterial();
        EnsurePartialRevealMaterial();
        currentAlpha = useLightBasedVisibility ? hiddenAlpha : fireSilhouetteAlpha;
        ApplyImmediateVisibility();
    }

    private void Update()
    {
        UpdateVisibility(Time.deltaTime);
    }

    private void OnValidate()
    {
        AutoAssignSilhouette();

        fireSilhouetteAlpha = Mathf.Clamp01(fireSilhouetteAlpha);
        hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
        waterSilhouetteAlpha = Mathf.Clamp01(waterSilhouetteAlpha);
        waterVisibilityRadius = Mathf.Max(0f, waterVisibilityRadius);
        fireVisibilityRadius = Mathf.Max(0f, fireVisibilityRadius);
        fadeSpeed = Mathf.Max(0f, fadeSpeed);
        partialRevealSoftness = Mathf.Max(0.01f, partialRevealSoftness);
        maxRevealSources = Mathf.Clamp(maxRevealSources, 1, MaxShaderRevealSources);
        cachedPartialRevealShader = null;
        partialRevealShaderSearched = false;

        if (!Application.isPlaying)
        {
            ApplyAlpha(useLightBasedVisibility ? hiddenAlpha : fireSilhouetteAlpha);
        }
    }

    private void UpdateVisibility(float deltaTime)
    {
        if (silhouetteRenderer == null)
        {
            return;
        }

        if (CanUsePartialReveal())
        {
            UpdatePartialVisibility(deltaTime);
            return;
        }

        UpdateWholeVisibility(deltaTime);
    }

    private void UpdateWholeVisibility(float deltaTime)
    {
        float targetAlpha = useLightBasedVisibility
            ? CalculateLightBasedAlpha()
            : fireSilhouetteAlpha;

        currentAlpha = fadeSpeed <= 0f
            ? targetAlpha
            : Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * deltaTime);

        ApplyAlpha(currentAlpha);
    }

    private void UpdatePartialVisibility(float deltaTime)
    {
        if (!useLightBasedVisibility)
        {
            ApplyAlpha(fireSilhouetteAlpha);
            return;
        }

        ClearRevealSourceData();

        int sourceCount = CollectPartialRevealSources();
        float targetAmount = sourceCount > 0 ? 1f : 0f;
        currentPartialRevealAmount = fadeSpeed <= 0f
            ? targetAmount
            : Mathf.MoveTowards(currentPartialRevealAmount, targetAmount, fadeSpeed * deltaTime);

        ApplyAlpha(1f);
        ApplyPartialRevealProperties(sourceCount);
    }

    private int CollectPartialRevealSources()
    {
        int sourceCount = 0;

        foreach (WaterCharacterLight waterLight in WaterCharacterLight.ActiveLights)
        {
            if (waterLight == null || !waterLight.IsLightActive)
            {
                continue;
            }

            float radius = waterVisibilityRadius > 0f ? waterVisibilityRadius : waterLight.LightRadius;
            sourceCount = TryAddRevealSource(sourceCount, waterLight.transform.position, radius, waterSilhouetteAlpha);
        }

        foreach (FireLightSource fireLight in FireLightSource.ActiveSources)
        {
            if (fireLight == null || !fireLight.IsLightActive)
            {
                continue;
            }

            float radius = fireVisibilityRadius > 0f ? fireVisibilityRadius : fireLight.RevealRadius;
            sourceCount = TryAddRevealSource(sourceCount, fireLight.transform.position, radius, fireSilhouetteAlpha);
        }

        return sourceCount;
    }

    private int TryAddRevealSource(int sourceCount, Vector2 sourcePosition, float radius, float sourceAlpha)
    {
        if (sourceCount >= maxRevealSources || radius <= 0f)
        {
            return sourceCount;
        }

        float distance = GetDistanceToSilhouette(sourcePosition);
        if (distance > radius + partialRevealSoftness)
        {
            return sourceCount;
        }

        revealSourceData[sourceCount] = new Vector4(
            sourcePosition.x,
            sourcePosition.y,
            radius,
            Mathf.Clamp01(sourceAlpha) * currentPartialRevealAmount);

        return sourceCount + 1;
    }

    private float CalculateLightBasedAlpha()
    {
        float targetAlpha = hiddenAlpha;

        foreach (WaterCharacterLight waterLight in WaterCharacterLight.ActiveLights)
        {
            if (waterLight == null || !waterLight.IsLightActive)
            {
                continue;
            }

            float radius = waterVisibilityRadius > 0f ? waterVisibilityRadius : waterLight.LightRadius;
            targetAlpha = Mathf.Max(
                targetAlpha,
                CalculateAlphaContribution(waterLight.transform.position, radius, waterSilhouetteAlpha));
        }

        foreach (FireLightSource fireLight in FireLightSource.ActiveSources)
        {
            if (fireLight == null || !fireLight.IsLightActive)
            {
                continue;
            }

            float radius = fireVisibilityRadius > 0f ? fireVisibilityRadius : fireLight.RevealRadius;
            targetAlpha = Mathf.Max(
                targetAlpha,
                CalculateAlphaContribution(fireLight.transform.position, radius, fireSilhouetteAlpha));
        }

        return targetAlpha;
    }

    private float CalculateAlphaContribution(Vector2 lightPosition, float radius, float visibleAlpha)
    {
        if (radius <= 0f)
        {
            return hiddenAlpha;
        }

        float distance = GetDistanceToSilhouette(lightPosition);
        float normalized = 1f - Mathf.Clamp01(distance / radius);
        float strength = Mathf.SmoothStep(0f, 1f, normalized);
        return Mathf.Lerp(hiddenAlpha, visibleAlpha, strength);
    }

    private float GetDistanceToSilhouette(Vector2 lightPosition)
    {
        Vector2 closestPoint = GetClosestSilhouettePoint(lightPosition);
        return Vector2.Distance(closestPoint, lightPosition);
    }

    private Vector2 GetClosestSilhouettePoint(Vector2 lightPosition)
    {
        if (silhouetteRenderer == null)
        {
            return transform.position;
        }

        return silhouetteRenderer.bounds.ClosestPoint(lightPosition);
    }

    private void ApplyImmediateVisibility()
    {
        if (CanUsePartialReveal())
        {
            currentPartialRevealAmount = 0f;
            ClearRevealSourceData();
            ApplyAlpha(1f);
            ApplyPartialRevealProperties(0);
            return;
        }

        ApplyAlpha(currentAlpha);
    }

    private void ApplyPartialRevealProperties(int sourceCount)
    {
        if (silhouetteRenderer == null)
        {
            return;
        }

        EnsurePartialRevealMaterial();
        if (!CanUsePartialReveal())
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetFloat(HiddenAlphaId, hiddenAlpha);
        propertyBlock.SetFloat(RevealedAlphaId, 1f);
        propertyBlock.SetFloat(RevealSoftnessId, partialRevealSoftness);
        propertyBlock.SetInt(RevealSourceCountId, sourceCount);
        propertyBlock.SetVectorArray(RevealSourcesId, revealSourceData);
        silhouetteRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ClearRevealSourceData()
    {
        for (int i = 0; i < revealSourceData.Length; i++)
        {
            revealSourceData[i] = Vector4.zero;
        }
    }

    private void ApplyAlpha(float alpha)
    {
        DarknessLightUtility.SetSpriteAlpha(silhouetteRenderer, alpha);
    }

    private bool CanUsePartialReveal()
    {
        return Application.isPlaying
            && useLightBasedVisibility
            && usePartialReveal
            && silhouetteRenderer != null
            && GetPartialRevealShader() != null;
    }

    private void CacheOriginalMaterial()
    {
        if (silhouetteRenderer != null && originalMaterial == null)
        {
            originalMaterial = silhouetteRenderer.sharedMaterial;
        }
    }

    private void EnsurePartialRevealMaterial()
    {
        if (!Application.isPlaying || !usePartialReveal || silhouetteRenderer == null)
        {
            return;
        }

        Shader shader = GetPartialRevealShader();
        if (shader == null)
        {
            return;
        }

        if (runtimePartialRevealMaterial == null)
        {
            Material sourceMaterial = partialRevealMaterial != null
                ? partialRevealMaterial
                : originalMaterial;

            runtimePartialRevealMaterial = sourceMaterial != null
                ? new Material(sourceMaterial)
                : new Material(shader);

            runtimePartialRevealMaterial.shader = shader;
            runtimePartialRevealMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        if (silhouetteRenderer.sharedMaterial != runtimePartialRevealMaterial)
        {
            silhouetteRenderer.sharedMaterial = runtimePartialRevealMaterial;
        }
    }

    private Shader GetPartialRevealShader()
    {
        if (partialRevealMaterial != null)
        {
            return partialRevealMaterial.shader;
        }

        if (!partialRevealShaderSearched)
        {
            cachedPartialRevealShader = Shader.Find("WaterAndFire/SpriteFireRevealMask");
            partialRevealShaderSearched = true;
        }

        return cachedPartialRevealShader;
    }

    private void AutoAssignSilhouette()
    {
        if (silhouetteRenderer != null)
        {
            return;
        }

        Transform visual = transform.Find("BigSilhouetteVisual");
        if (visual == null)
        {
            visual = transform.Find("SilhouetteVisual");
        }

        silhouetteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
    }

    private void OnDestroy()
    {
        if (runtimePartialRevealMaterial != null)
        {
            Destroy(runtimePartialRevealMaterial);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        AutoAssignSilhouette();

        if (silhouetteRenderer != null)
        {
            Gizmos.color = new Color(0.18f, 0.12f, 0.28f, 0.35f);
            Gizmos.DrawWireCube(silhouetteRenderer.bounds.center, silhouetteRenderer.bounds.size);
        }

        if (useLightBasedVisibility)
        {
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, waterVisibilityRadius);
            Gizmos.color = new Color(1f, 0.45f, 0.08f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, fireVisibilityRadius);
        }
    }
}
