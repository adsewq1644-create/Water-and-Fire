using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FireLightSource : MonoBehaviour
{
    private static readonly List<FireLightSource> activeSources = new List<FireLightSource>();

    [SerializeField] private Light2D pointLight2D;
    [FormerlySerializedAs("revealRadius")]
    [SerializeField] private float lightRadius = 7f;
    [SerializeField] private float lightIntensity = 1f;
    [SerializeField] private Color lightColor = new Color(1f, 0.55f, 0.16f, 1f);
    [SerializeField] private float flickerAmount = 0.08f;
    [SerializeField] private float flickerSpeed = 8f;
    [SerializeField] private bool debugGizmos = true;

    private bool lightActive = true;
    private float flickerSeed;

    public static IReadOnlyList<FireLightSource> ActiveSources => activeSources;
    public float LightRadius => lightRadius;
    public bool IsLightActive => isActiveAndEnabled && lightActive && pointLight2D != null && pointLight2D.enabled;

    private void Reset()
    {
        pointLight2D = GetComponent<Light2D>();
    }

    private void Awake()
    {
        EnsureLight();
        flickerSeed = Random.Range(0f, 100f);
        ApplyLightSettings();
    }

    private void OnEnable()
    {
        if (!activeSources.Contains(this))
        {
            activeSources.Add(this);
        }

        EnsureLight();
        ApplyLightSettings();
    }

    private void OnDisable()
    {
        activeSources.Remove(this);
    }

    private void Update()
    {
        if (pointLight2D == null)
        {
            return;
        }

        float flicker = flickerAmount <= 0f
            ? 0f
            : (Mathf.PerlinNoise(flickerSeed, Time.time * flickerSpeed) - 0.5f) * flickerAmount;

        pointLight2D.enabled = lightActive;
        pointLight2D.intensity = Mathf.Max(0f, lightIntensity + flicker);
        pointLight2D.color = lightColor;
        pointLight2D.pointLightOuterRadius = Mathf.Max(0.01f, lightRadius);
        pointLight2D.pointLightInnerRadius = Mathf.Max(0f, lightRadius * 0.18f);
    }

    public void SetLightEnabled(bool enabled)
    {
        lightActive = enabled;
        if (pointLight2D != null)
        {
            pointLight2D.enabled = enabled;
        }
    }

    public void SetRadiusAndIntensity(float radius, float intensity)
    {
        lightRadius = Mathf.Max(0.01f, radius);
        lightIntensity = Mathf.Max(0f, intensity);
        ApplyLightSettings();
    }

    private void EnsureLight()
    {
        if (pointLight2D == null)
        {
            pointLight2D = GetComponent<Light2D>();
        }

        if (pointLight2D == null && Application.isPlaying)
        {
            pointLight2D = gameObject.AddComponent<Light2D>();
        }
    }

    private void ApplyLightSettings()
    {
        DarknessLightUtility.ConfigurePointLight(pointLight2D, lightRadius, lightIntensity, lightColor);
        if (pointLight2D != null)
        {
            pointLight2D.enabled = lightActive;
        }
    }

    private void OnValidate()
    {
        lightRadius = Mathf.Max(0.01f, lightRadius);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        flickerAmount = Mathf.Max(0f, flickerAmount);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);

        if (pointLight2D == null)
        {
            pointLight2D = GetComponent<Light2D>();
        }

        ApplyLightSettings();
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.45f, 0.08f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, lightRadius);
    }
}
