using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivePassThroughPlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    private const float ContinueFallSpeed = 18f;

    private Collider2D platformCollider;

    private void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        PlayerCharacter player = instigator != null
            ? instigator.GetComponent<PlayerCharacter>()
            : null;

        if (player == null || !player.IsDiving)
        {
            return;
        }

        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }

        player.ContinueDiveThroughPlatform(platformCollider, ContinueFallSpeed);
    }
}
