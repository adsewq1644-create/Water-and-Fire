using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class HiddenCrumblePlatform2D : MonoBehaviour
{
    private enum PlatformState
    {
        Ready,
        Warning,
        Broken
    }

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float collapseDelay = 2f;
    [SerializeField, Min(0.1f)] private float respawnDelay = 2f;

    [Header("Warning Dust")]
    [SerializeField, Min(0.01f)] private float slowDustInterval = 0.32f;
    [SerializeField, Min(0.01f)] private float fastDustInterval = 0.055f;

    [Header("Horizontal Warning Shake")]
    [FormerlySerializedAs("maximumDustShakeOffset")]
    [Tooltip("Maximum left/right travel of the crumble dust emission points. The platform collider does not move.")]
    [SerializeField, Range(0f, 0.5f)] private float horizontalShakeWidth = 0.16f;
    [SerializeField, Min(0.1f)] private float slowShakeFrequency = 1.8f;
    [SerializeField, Min(0.1f)] private float fastShakeFrequency = 14f;

    [Header("Dust Intensity")]
    [SerializeField, Range(0f, 2f)] private float startingDustIntensity = 0.35f;
    [SerializeField, Range(0f, 2f)] private float endingDustIntensity = 1.1f;

    [Header("Contact")]
    [SerializeField, Range(0f, 1f)] private float minimumTopContactNormalY = 0.55f;

    private Collider2D platformCollider;
    private ShockwaveHiddenPlatform2D revealFeedback;
    private PlatformState state;
    private float stateStartedAt;
    private float nextDustTime;
    private float shakePhase;

    private void Awake()
    {
        ResolveReferences();
        ResetPlatform();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetPlatform();
    }

    private void Update()
    {
        switch (state)
        {
            case PlatformState.Warning:
                UpdateWarning();
                break;
            case PlatformState.Broken:
                if (Time.time >= stateStartedAt + respawnDelay)
                {
                    ResetPlatform();
                }
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartWarning(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryStartWarning(collision);
    }

    private void TryStartWarning(Collision2D collision)
    {
        if (state != PlatformState.Ready || !IsValidTopContact(collision))
        {
            return;
        }

        state = PlatformState.Warning;
        stateStartedAt = Time.time;
        nextDustTime = Time.time;
        shakePhase = 0f;
    }

    private void UpdateWarning()
    {
        float duration = Mathf.Max(0.1f, collapseDelay);
        float elapsed = Time.time - stateStartedAt;
        float progress = Mathf.Clamp01(elapsed / duration);
        float acceleration = progress * progress;

        float frequency = Mathf.Lerp(slowShakeFrequency, fastShakeFrequency, acceleration);
        shakePhase += Time.deltaTime * frequency * Mathf.PI * 2f;
        float shakeAmplitude = Mathf.Lerp(0.35f, 1f, progress);
        float shakeOffset = Mathf.Sin(shakePhase) * horizontalShakeWidth * shakeAmplitude;

        if (Time.time >= nextDustTime)
        {
            float intensity = Mathf.Lerp(startingDustIntensity, endingDustIntensity, progress);
            revealFeedback?.EmitCrumbleDust(intensity, transform.right * shakeOffset);
            nextDustTime = Time.time + Mathf.Lerp(slowDustInterval, fastDustInterval, acceleration);
        }

        if (elapsed >= duration)
        {
            Collapse();
        }
    }

    private void Collapse()
    {
        state = PlatformState.Broken;
        stateStartedAt = Time.time;
        if (platformCollider != null)
        {
            platformCollider.enabled = false;
        }
    }

    private void ResetPlatform()
    {
        state = PlatformState.Ready;
        stateStartedAt = 0f;
        nextDustTime = 0f;
        shakePhase = 0f;
        if (platformCollider != null)
        {
            platformCollider.enabled = true;
            platformCollider.isTrigger = false;
        }
    }

    private bool IsValidTopContact(Collision2D collision)
    {
        if (collision == null || collision.contactCount == 0)
        {
            return false;
        }

        PlayerCharacter player = collision.rigidbody != null
            ? collision.rigidbody.GetComponentInParent<PlayerCharacter>()
            : collision.collider != null
                ? collision.collider.GetComponentInParent<PlayerCharacter>()
                : null;
        if (player == null || player.BodyCollider == null || !player.IsAliveLike)
        {
            return false;
        }

        Vector2 surfaceUp = transform.up;
        Vector2 playerCenter = player.BodyCollider.bounds.center;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 toPlayer = playerCenter - contact.point;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            float playerAboveSurface = Vector2.Dot(toPlayer.normalized, surfaceUp);
            float normalAlignment = Mathf.Abs(Vector2.Dot(contact.normal.normalized, surfaceUp));
            if (playerAboveSurface >= minimumTopContactNormalY &&
                normalAlignment >= minimumTopContactNormalY)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }
        if (revealFeedback == null)
        {
            revealFeedback = GetComponent<ShockwaveHiddenPlatform2D>();
        }
    }

    private void OnValidate()
    {
        collapseDelay = Mathf.Max(0.1f, collapseDelay);
        respawnDelay = Mathf.Max(0.1f, respawnDelay);
        slowDustInterval = Mathf.Max(0.01f, slowDustInterval);
        fastDustInterval = Mathf.Clamp(fastDustInterval, 0.01f, slowDustInterval);
        horizontalShakeWidth = Mathf.Max(0f, horizontalShakeWidth);
        slowShakeFrequency = Mathf.Max(0.1f, slowShakeFrequency);
        fastShakeFrequency = Mathf.Max(slowShakeFrequency, fastShakeFrequency);
        startingDustIntensity = Mathf.Max(0f, startingDustIntensity);
        endingDustIntensity = Mathf.Max(startingDustIntensity, endingDustIntensity);
        minimumTopContactNormalY = Mathf.Clamp01(minimumTopContactNormalY);
        ResolveReferences();
    }
}
