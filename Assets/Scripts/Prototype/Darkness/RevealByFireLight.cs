using UnityEngine;

[DisallowMultipleComponent]
public class RevealByFireLight : MonoBehaviour
{
    private const int MaxShaderRevealSources = 8;
    private static readonly int HiddenAlphaId = Shader.PropertyToID("_HiddenAlpha");
    private static readonly int RevealedAlphaId = Shader.PropertyToID("_RevealedAlpha");
    private static readonly int RevealSoftnessId = Shader.PropertyToID("_RevealSoftness");
    private static readonly int RevealSourceCountId = Shader.PropertyToID("_RevealSourceCount");
    private static readonly int RevealSourcesId = Shader.PropertyToID("_RevealSources");

    [SerializeField] private SpriteRenderer silhouetteRenderer;
    [SerializeField] private SpriteRenderer revealedRenderer;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha;
    [SerializeField, Range(0f, 1f)] private float revealedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float silhouetteAlpha = 0.22f;
    [SerializeField] private float revealSpeed = 6f;
    [SerializeField] private float hideSpeed = 3.5f;
    [SerializeField] private bool onlyRevealInDarkZone;
    [SerializeField] private bool debugGizmos = true;
    [SerializeField] private bool usePartialReveal = true;
    [SerializeField] private Material partialRevealMaterial;
    [SerializeField] private float partialRevealSoftness = 0.65f;
    [SerializeField, Range(1, MaxShaderRevealSources)] private int maxRevealSources = 8;

    private readonly Vector4[] revealSourceData = new Vector4[MaxShaderRevealSources];
    private float currentRevealAlpha;
    private bool revealed;
    private float nearestRevealDistance = float.PositiveInfinity;
    private Material runtimePartialRevealMaterial;
    private Material originalRevealedMaterial;
    private MaterialPropertyBlock propertyBlock;
    private Shader cachedPartialRevealShader;
    private bool partialRevealShaderSearched;

    public bool IsRevealed => revealed;
    public float NearestRevealDistance => nearestRevealDistance;

    private void Reset()
    {
        AutoAssignRenderers();
    }

    private void Awake()
    {
        AutoAssignRenderers();
        CacheOriginalMaterial();
        EnsurePartialRevealMaterial();
        ApplyImmediateAlpha(false);
    }

    private void Update()
    {
        DarknessLightUtility.SetSpriteAlpha(silhouetteRenderer, silhouetteAlpha);
        bool revealAllowed = !onlyRevealInDarkZone || DarkZoneManager.IsLocalViewDark;

        if (CanUsePartialReveal())
        {
            UpdatePartialReveal(revealAllowed);
        }
        else
        {
            UpdateWholeObjectReveal(revealAllowed);
        }
    }

    public void ApplyImmediateAlpha(bool forceRevealed)
    {
        currentRevealAlpha = forceRevealed ? revealedAlpha : hiddenAlpha;
        DarknessLightUtility.SetSpriteAlpha(silhouetteRenderer, silhouetteAlpha);
        if (CanUsePartialReveal())
        {
            DarknessLightUtility.SetSpriteAlpha(revealedRenderer, 1f);
            ApplyPartialRevealProperties(0);
        }
        else
        {
            DarknessLightUtility.SetSpriteAlpha(revealedRenderer, currentRevealAlpha);
        }
    }

    private void UpdateWholeObjectReveal(bool revealAllowed)
    {
        nearestRevealDistance = float.PositiveInfinity;
        revealed = false;

        if (revealAllowed)
        {
            foreach (FireLightSource source in FireLightSource.ActiveSources)
            {
                if (!IsValidRevealSource(source))
                {
                    continue;
                }

                float distance = GetDistanceToSource(source.transform.position);
                if (distance < nearestRevealDistance)
                {
                    nearestRevealDistance = distance;
                }

                if (distance <= source.RevealRadius)
                {
                    revealed = true;
                }
            }
        }

        float targetAlpha = revealed ? revealedAlpha : hiddenAlpha;
        float speed = revealed ? revealSpeed : hideSpeed;
        currentRevealAlpha = Mathf.MoveTowards(currentRevealAlpha, targetAlpha, speed * Time.deltaTime);
        DarknessLightUtility.SetSpriteAlpha(revealedRenderer, currentRevealAlpha);
    }

    private void UpdatePartialReveal(bool revealAllowed)
    {
        nearestRevealDistance = float.PositiveInfinity;
        revealed = false;
        int sourceCount = 0;

        ClearRevealSourceData();

        if (revealAllowed)
        {
            foreach (FireLightSource source in FireLightSource.ActiveSources)
            {
                if (!IsValidRevealSource(source))
                {
                    continue;
                }

                float distance = GetDistanceToSource(source.transform.position);
                if (distance < nearestRevealDistance)
                {
                    nearestRevealDistance = distance;
                }

                if (distance <= source.RevealRadius)
                {
                    revealed = true;
                }

                if (sourceCount < maxRevealSources && distance <= source.RevealRadius + partialRevealSoftness)
                {
                    Vector3 position = source.transform.position;
                    revealSourceData[sourceCount] = new Vector4(position.x, position.y, source.RevealRadius, 1f);
                    sourceCount++;
                }
            }
        }

        float targetAlpha = sourceCount > 0 ? revealedAlpha : hiddenAlpha;
        float speed = sourceCount > 0 ? revealSpeed : hideSpeed;
        currentRevealAlpha = Mathf.MoveTowards(currentRevealAlpha, targetAlpha, speed * Time.deltaTime);

        DarknessLightUtility.SetSpriteAlpha(revealedRenderer, 1f);
        ApplyPartialRevealProperties(sourceCount);
    }

    private bool IsValidRevealSource(FireLightSource source)
    {
        return source != null && source.CanRevealObjects && source.IsLightActive;
    }

    private void ApplyPartialRevealProperties(int sourceCount)
    {
        if (revealedRenderer == null)
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
        propertyBlock.SetFloat(RevealedAlphaId, currentRevealAlpha);
        propertyBlock.SetFloat(RevealSoftnessId, partialRevealSoftness);
        propertyBlock.SetInt(RevealSourceCountId, sourceCount);
        propertyBlock.SetVectorArray(RevealSourcesId, revealSourceData);
        revealedRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ClearRevealSourceData()
    {
        for (int i = 0; i < revealSourceData.Length; i++)
        {
            revealSourceData[i] = Vector4.zero;
        }
    }

    private bool CanUsePartialReveal()
    {
        return Application.isPlaying
            && usePartialReveal
            && revealedRenderer != null
            && GetPartialRevealShader() != null;
    }

    private void CacheOriginalMaterial()
    {
        if (revealedRenderer != null && originalRevealedMaterial == null)
        {
            originalRevealedMaterial = revealedRenderer.sharedMaterial;
        }
    }

    private void EnsurePartialRevealMaterial()
    {
        if (!Application.isPlaying || !usePartialReveal || revealedRenderer == null)
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
                : originalRevealedMaterial;

            runtimePartialRevealMaterial = sourceMaterial != null
                ? new Material(sourceMaterial)
                : new Material(shader);

            runtimePartialRevealMaterial.shader = shader;
            runtimePartialRevealMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        if (revealedRenderer.sharedMaterial != runtimePartialRevealMaterial)
        {
            revealedRenderer.sharedMaterial = runtimePartialRevealMaterial;
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

    private void OnDestroy()
    {
        if (runtimePartialRevealMaterial != null)
        {
            Destroy(runtimePartialRevealMaterial);
        }
    }

    private float GetDistanceToSource(Vector2 sourcePosition)
    {
        if (revealedRenderer != null)
        {
            return Vector2.Distance(revealedRenderer.bounds.ClosestPoint(sourcePosition), sourcePosition);
        }

        if (silhouetteRenderer != null)
        {
            return Vector2.Distance(silhouetteRenderer.bounds.ClosestPoint(sourcePosition), sourcePosition);
        }

        return Vector2.Distance(transform.position, sourcePosition);
    }

    private void AutoAssignRenderers()
    {
        if (silhouetteRenderer == null)
        {
            Transform silhouette = transform.Find("SilhouetteVisual");
            if (silhouette == null)
            {
                silhouette = transform.Find("Silhouette");
            }

            silhouetteRenderer = silhouette != null ? silhouette.GetComponent<SpriteRenderer>() : null;
        }

        if (revealedRenderer == null)
        {
            Transform revealedVisual = transform.Find("RevealedVisual");
            if (revealedVisual == null)
            {
                revealedVisual = transform.Find("Revealed");
            }

            revealedRenderer = revealedVisual != null ? revealedVisual.GetComponent<SpriteRenderer>() : null;
        }
    }

    private void OnValidate()
    {
        revealSpeed = Mathf.Max(0f, revealSpeed);
        hideSpeed = Mathf.Max(0f, hideSpeed);
        partialRevealSoftness = Mathf.Max(0.01f, partialRevealSoftness);
        maxRevealSources = Mathf.Clamp(maxRevealSources, 1, MaxShaderRevealSources);
        cachedPartialRevealShader = null;
        partialRevealShaderSearched = false;
        AutoAssignRenderers();
        if (!Application.isPlaying)
        {
            ApplyImmediateAlpha(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = revealed
            ? new Color(1f, 0.65f, 0.1f, 0.9f)
            : new Color(0.6f, 0.6f, 0.65f, 0.45f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.35f);
    }
}
