using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterVisionVisible2D : MonoBehaviour
{
    private const int MaxShaderSources = 8;
    private static readonly int HiddenAlphaId = Shader.PropertyToID("_HiddenAlpha");
    private static readonly int VisibleAlphaId = Shader.PropertyToID("_VisibleAlpha");
    private static readonly int VisionSoftnessId = Shader.PropertyToID("_VisionSoftness");
    private static readonly int VisionSourceCountId = Shader.PropertyToID("_VisionSourceCount");
    private static readonly int VisionSourcesId = Shader.PropertyToID("_VisionSources");

    [Header("References")]
    [SerializeField] private SpriteRenderer[] renderers;

    [Header("Visibility")]
    [SerializeField] private bool restrictOnlyInDarkZone = true;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField] private float visionSoftness = 0.8f;
    [SerializeField, Range(1, MaxShaderSources)] private int maxVisionSources = 8;

    private readonly Vector4[] sourceData = new Vector4[MaxShaderSources];
    private Material runtimeMaterial;
    private Material[] originalMaterials;
    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        ResolveRenderers();
    }

    private void Awake()
    {
        ResolveRenderers();
        CacheOriginalMaterials();
        EnsureRuntimeMaterial();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EnsureRuntimeMaterial();
        }
    }

    private void Update()
    {
        if (!EnsureRuntimeMaterial())
        {
            return;
        }

        bool restrictVisibility = !restrictOnlyInDarkZone || DarkZoneManager.IsLocalViewDark;
        int sourceCount = restrictVisibility ? CollectVisionSources() : 0;
        ApplyVisionProperties(sourceCount, restrictVisibility);
    }

    private int CollectVisionSources()
    {
        int count = 0;
        ClearSourceData();

        IReadOnlyList<WaterCharacterLight> waterLights = WaterCharacterLight.ActiveLights;
        for (int i = 0; i < waterLights.Count && count < maxVisionSources; i++)
        {
            WaterCharacterLight light = waterLights[i];
            if (light == null || !light.IsLightActive)
            {
                continue;
            }

            Vector3 position = light.transform.position;
            sourceData[count++] = new Vector4(position.x, position.y, light.LightRadius, 1f);
        }

        IReadOnlyList<FireLightSource> fireLights = FireLightSource.ActiveSources;
        for (int i = 0; i < fireLights.Count && count < maxVisionSources; i++)
        {
            FireLightSource light = fireLights[i];
            if (light == null || !light.IsLightActive)
            {
                continue;
            }

            Vector3 position = light.transform.position;
            sourceData[count++] = new Vector4(position.x, position.y, light.LightRadius, 1f);
        }

        return count;
    }

    private void ApplyVisionProperties(int sourceCount, bool restrictVisibility)
    {
        propertyBlock ??= new MaterialPropertyBlock();
        float baseAlpha = restrictVisibility ? hiddenAlpha : visibleAlpha;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetFloat(HiddenAlphaId, baseAlpha);
            propertyBlock.SetFloat(VisibleAlphaId, visibleAlpha);
            propertyBlock.SetFloat(VisionSoftnessId, visionSoftness);
            propertyBlock.SetInt(VisionSourceCountId, sourceCount);
            propertyBlock.SetVectorArray(VisionSourcesId, sourceData);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private bool EnsureRuntimeMaterial()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        ResolveRenderers();
        CacheOriginalMaterials();

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("WaterAndFire/SpriteCharacterVisionMask");
            if (shader == null)
            {
                return false;
            }

            runtimeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial != runtimeMaterial)
            {
                renderers[i].sharedMaterial = runtimeMaterial;
            }
        }

        return true;
    }

    private void CacheOriginalMaterials()
    {
        if (originalMaterials != null && originalMaterials.Length == renderers.Length)
        {
            return;
        }

        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterial : null;
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (originalMaterials == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length && i < originalMaterials.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].sharedMaterial = originalMaterials[i];
            renderers[i].SetPropertyBlock(null);
        }
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterials();
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void ResolveRenderers()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void ClearSourceData()
    {
        for (int i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = Vector4.zero;
        }
    }

    private void OnValidate()
    {
        hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
        visibleAlpha = Mathf.Clamp01(visibleAlpha);
        visionSoftness = Mathf.Max(0.01f, visionSoftness);
        maxVisionSources = Mathf.Clamp(maxVisionSources, 1, MaxShaderSources);
        ResolveRenderers();
    }
}
