using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class WaterCharacterLight : MonoBehaviour
{
    private static readonly List<WaterCharacterLight> activeLights = new List<WaterCharacterLight>();

    [SerializeField] private Light2D pointLight2D;
    [SerializeField] private float lightRadius = 3f;
    [SerializeField] private float lightIntensity = 0.3f;
    [SerializeField] private Color lightColor = new Color(0.35f, 0.9f, 1f, 1f);
    [SerializeField] private bool onlyInDarkZone;
    [SerializeField] private bool debugGizmos = true;

    public static IReadOnlyList<WaterCharacterLight> ActiveLights => activeLights;
    public float LightRadius => lightRadius;
    public bool IsLightActive => isActiveAndEnabled && pointLight2D != null && pointLight2D.enabled;

    private void Reset()
    {
        pointLight2D = GetComponent<Light2D>();
    }

    private void Awake()
    {
        EnsureLight();
        ApplyLightSettings();
    }

    private void OnEnable()
    {
        if (!activeLights.Contains(this))
        {
            activeLights.Add(this);
        }

        EnsureLight();
        ApplyLightSettings();
    }

    private void OnDisable()
    {
        activeLights.Remove(this);
    }

    private void Update()
    {
        if (pointLight2D == null)
        {
            return;
        }

        pointLight2D.enabled = !onlyInDarkZone || DarkZoneManager.IsLocalViewDark;
        DarknessLightUtility.ConfigurePointLight(pointLight2D, lightRadius, lightIntensity, lightColor);
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
    }

    private void OnValidate()
    {
        lightRadius = Mathf.Max(0.01f, lightRadius);
        lightIntensity = Mathf.Max(0f, lightIntensity);

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

        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, lightRadius);
    }
}
