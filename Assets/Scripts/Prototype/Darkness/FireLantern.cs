using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class FireLantern : MonoBehaviour
{
    [SerializeField] private FireLightSource fireLightSource;
    [SerializeField] private Light2D pointLight2D;
    [SerializeField] private bool stayOnPermanently = true;
    [SerializeField] private float lightRadius = 6f;
    [SerializeField] private float lightIntensity = 0.9f;
    [SerializeField] private bool activatedByFireProjectileOnly = true;
    [SerializeField] private bool startsOn;
    [SerializeField] private bool debugGizmos = true;

    private bool isOn;

    public bool IsOn => isOn;

    private void Reset()
    {
        fireLightSource = GetComponent<FireLightSource>();
        pointLight2D = GetComponent<Light2D>();
    }

    private void Awake()
    {
        EnsureComponents();
        SetLanternOn(startsOn, true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryActivateFromCollider(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryActivateFromCollider(collision.collider);
    }

    public void SetLanternOn(bool on)
    {
        SetLanternOn(on, false);
    }

    public void ToggleLantern()
    {
        SetLanternOn(!isOn);
    }

    private void SetLanternOn(bool on, bool force)
    {
        if (!force && stayOnPermanently && isOn && !on)
        {
            return;
        }

        isOn = on;
        EnsureComponents();

        if (fireLightSource != null)
        {
            fireLightSource.SetRadiusAndIntensity(lightRadius, lightIntensity);
            fireLightSource.SetLightEnabled(isOn);
        }

        if (pointLight2D != null)
        {
            DarknessLightUtility.ConfigurePointLight(pointLight2D, lightRadius, lightIntensity, pointLight2D.color);
            pointLight2D.enabled = isOn;
        }
    }

    private void TryActivateFromCollider(Collider2D other)
    {
        if (other == null || isOn)
        {
            return;
        }

        if (activatedByFireProjectileOnly && !IsFireProjectile(other))
        {
            return;
        }

        SetLanternOn(true);
    }

    private bool IsFireProjectile(Collider2D other)
    {
        ElementProjectile projectile = other.GetComponentInParent<ElementProjectile>();
        return projectile != null && projectile.Element == ElementType.Fire;
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

    private void OnValidate()
    {
        lightRadius = Mathf.Max(0.01f, lightRadius);
        lightIntensity = Mathf.Max(0f, lightIntensity);

        if (fireLightSource == null)
        {
            fireLightSource = GetComponent<FireLightSource>();
        }

        if (pointLight2D == null)
        {
            pointLight2D = GetComponent<Light2D>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = isOn
            ? new Color(1f, 0.5f, 0.1f, 0.9f)
            : new Color(0.35f, 0.25f, 0.12f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, lightRadius);
    }
}
