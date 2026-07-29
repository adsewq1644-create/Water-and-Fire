using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CrumbleDustWarningVisual2D))]
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

    [Header("Visual")]
    [SerializeField] private CrumbleDustWarningVisual2D warningVisual;

    [Header("Contact")]
    [SerializeField, Range(0f, 1f)] private float minimumTopContactNormalY = 0.55f;

    private Collider2D platformCollider;
    private PlatformState state;
    private float stateStartedAt;

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
        if (state != PlatformState.Ready ||
            !TryGetValidTopContact(collision, out Vector2 impactPoint))
        {
            return;
        }

        state = PlatformState.Warning;
        stateStartedAt = Time.time;
        warningVisual?.BeginWarning(collapseDelay);
        warningVisual?.PlayLandingBurst(impactPoint);
    }

    private void UpdateWarning()
    {
        float duration = Mathf.Max(0.1f, collapseDelay);
        float elapsed = Time.time - stateStartedAt;
        float progress = Mathf.Clamp01(elapsed / duration);
        warningVisual?.SetWarningProgress(progress);

        if (elapsed >= duration)
        {
            Collapse();
        }
    }

    private void Collapse()
    {
        state = PlatformState.Broken;
        stateStartedAt = Time.time;
        warningVisual?.PlayPreBreakWarning();
        warningVisual?.PlayBreakRelease();
        if (platformCollider != null)
        {
            platformCollider.enabled = false;
        }
    }

    private void ResetPlatform()
    {
        state = PlatformState.Ready;
        stateStartedAt = 0f;
        warningVisual?.StopAndReset();
        if (platformCollider != null)
        {
            platformCollider.enabled = true;
            platformCollider.isTrigger = false;
        }
    }

    private bool TryGetValidTopContact(Collision2D collision, out Vector2 impactPoint)
    {
        impactPoint = transform.position;
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
                impactPoint = contact.point;
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
        if (warningVisual == null)
        {
            warningVisual = GetComponent<CrumbleDustWarningVisual2D>();
        }
    }

    private void OnValidate()
    {
        collapseDelay = Mathf.Max(0.1f, collapseDelay);
        respawnDelay = Mathf.Max(0.1f, respawnDelay);
        minimumTopContactNormalY = Mathf.Clamp01(minimumTopContactNormalY);
        ResolveReferences();
    }
}
