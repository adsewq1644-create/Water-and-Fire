using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CrumbleDustWarningVisual2D))]
public sealed class HiddenDiveThroughPlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float respawnDelay = 2f;

    [Header("Dive")]
    [SerializeField, Min(0f)] private float continueFallSpeed = 18f;

    [Header("Visual")]
    [SerializeField] private CrumbleDustWarningVisual2D warningVisual;

    private Collider2D platformCollider;
    private bool broken;
    private float brokenAt;

    private void Awake()
    {
        ResolveReferences();
        ResetPlatform(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetPlatform(false);
    }

    private void Update()
    {
        if (broken && Time.time >= brokenAt + respawnDelay)
        {
            ResetPlatform(true);
        }
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        if (broken)
        {
            return;
        }

        PlayerCharacter player = instigator != null
            ? instigator.GetComponentInParent<PlayerCharacter>()
            : null;
        if (player == null || !player.IsAliveLike || !player.IsDiving)
        {
            return;
        }

        ResolveReferences();
        player.ContinueDiveThroughPlatform(platformCollider, continueFallSpeed);

        broken = true;
        brokenAt = Time.time;
        warningVisual?.PlayImmediateBreak(impactPoint);
        if (platformCollider != null)
        {
            platformCollider.enabled = false;
        }
    }

    private void ResetPlatform(bool playRespawnBurst)
    {
        broken = false;
        brokenAt = 0f;
        warningVisual?.StopAndReset();
        if (platformCollider != null)
        {
            platformCollider.enabled = true;
            platformCollider.isTrigger = false;
        }

        if (playRespawnBurst)
        {
            warningVisual?.PlayRespawnBurst();
        }
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
        respawnDelay = Mathf.Max(0.1f, respawnDelay);
        continueFallSpeed = Mathf.Max(0f, continueFallSpeed);
        ResolveReferences();
    }
}
