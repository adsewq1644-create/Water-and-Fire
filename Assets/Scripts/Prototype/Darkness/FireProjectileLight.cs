using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class FireProjectileLight : MonoBehaviour
{
    [SerializeField] private FireLightSource fireLightSource;
    [SerializeField] private Light2D pointLight2D;
    [SerializeField] private float revealRadius = 4f;
    [SerializeField] private float lightIntensity = 0.65f;
    [SerializeField] private Color lightColor = new Color(1f, 0.42f, 0.08f, 1f);
    [SerializeField] private float lightLifetimeAfterHit = 1.2f;
    [SerializeField] private bool fadeAfterHit = true;
    [SerializeField] private bool debugGizmos = true;

    private bool fading;
    private float fadeTimer;

    private void Awake()
    {
        EnsureComponents();
        ApplySettings();
    }

    private void Update()
    {
        if (!fading)
        {
            return;
        }

        fadeTimer += Time.deltaTime;
        float t = lightLifetimeAfterHit <= 0f ? 1f : Mathf.Clamp01(fadeTimer / lightLifetimeAfterHit);
        float intensity = Mathf.Lerp(lightIntensity, 0f, t);

        if (fireLightSource != null)
        {
            fireLightSource.SetRadiusAndIntensity(revealRadius, intensity);
            fireLightSource.SetLightEnabled(intensity > 0.01f);
        }

        if (pointLight2D != null)
        {
            pointLight2D.intensity = intensity;
            pointLight2D.enabled = intensity > 0.01f;
        }

        if (t >= 1f)
        {
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BeginFadeAfterHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        BeginFadeAfterHit(collision.collider);
    }

    public void BeginFadeAfterHit(Collider2D hit)
    {
        if (!fadeAfterHit || fading || hit == null || hit.GetComponentInParent<PlayerCharacter>() != null)
        {
            return;
        }

        fading = true;
        fadeTimer = 0f;
    }

    private void EnsureComponents()
    {
        if (fireLightSource == null)
        {
            fireLightSource = GetComponent<FireLightSource>();
        }

        if (pointLight2D == null)
        {
            pointLight2D = GetComponent<Light2D>();
        }

        if (fireLightSource == null && Application.isPlaying)
        {
            fireLightSource = gameObject.AddComponent<FireLightSource>();
        }

        if (pointLight2D == null && Application.isPlaying)
        {
            pointLight2D = gameObject.AddComponent<Light2D>();
        }
    }

    private void ApplySettings()
    {
        if (fireLightSource != null)
        {
            fireLightSource.SetRadiusAndIntensity(revealRadius, lightIntensity);
            fireLightSource.SetRevealEnabled(true);
            fireLightSource.SetLightEnabled(true);
        }

        if (pointLight2D != null)
        {
            DarknessLightUtility.ConfigurePointLight(pointLight2D, revealRadius, lightIntensity, lightColor);
            pointLight2D.enabled = true;
        }
    }

    private void OnValidate()
    {
        revealRadius = Mathf.Max(0.01f, revealRadius);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        lightLifetimeAfterHit = Mathf.Max(0f, lightLifetimeAfterHit);

        if (fireLightSource == null)
        {
            fireLightSource = GetComponent<FireLightSource>();
        }

        if (pointLight2D == null)
        {
            pointLight2D = GetComponent<Light2D>();
        }

        ApplySettings();
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.35f, 0.08f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, revealRadius);
    }
}
