using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class WaterCharacterLight : MonoBehaviour
{
    [SerializeField] private Light2D pointLight2D;
    [SerializeField] private float lightRadius = 3f;
    [SerializeField] private float lightIntensity = 0.3f;
    [SerializeField] private Color lightColor = new Color(0.35f, 0.9f, 1f, 1f);
    [SerializeField] private bool onlyInDarkZone;
    [SerializeField] private bool canRevealObjects;
    [SerializeField] private bool debugGizmos = true;

    public bool CanRevealObjects => canRevealObjects;

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
        EnsureLight();
        ApplyLightSettings();
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
        canRevealObjects = false;
        DarknessLightUtility.ConfigurePointLight(pointLight2D, lightRadius, lightIntensity, lightColor);
    }

    private void OnValidate()
    {
        lightRadius = Mathf.Max(0.01f, lightRadius);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        canRevealObjects = false;

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
