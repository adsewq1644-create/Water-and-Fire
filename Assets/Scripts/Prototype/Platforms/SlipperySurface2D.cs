using UnityEngine;

[DisallowMultipleComponent]
public sealed class SlipperySurface2D : MonoBehaviour
{
    [Header("Safety")]
    [SerializeField] private bool preventSafeGround = true;

    [Header("Ground Slide")]
    [SerializeField] private bool enableGroundSlide = true;
    [SerializeField] private float groundSlideAcceleration = 24f;
    [SerializeField] private float maxGroundSlideSpeed = 6f;
    [SerializeField, Range(0f, 1f)] private float groundInputControl = 0.1f;

    [Header("Exit Carry")]
    [SerializeField] private float exitCarryTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    public bool PreventSafeGround => preventSafeGround;
    public float GroundSlideAcceleration => groundSlideAcceleration;
    public float MaxGroundSlideSpeed => maxGroundSlideSpeed;
    public float GroundInputControl => groundInputControl;
    public float ExitCarryTime => exitCarryTime;

    public bool TryGetGroundSlideDirection(Vector2 surfaceNormal, out Vector2 slideDirection)
    {
        slideDirection = Vector2.zero;
        if (!enableGroundSlide)
        {
            return false;
        }

        if (surfaceNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        return TryGetProjectedGravitySlideDirection(surfaceNormal, out slideDirection);
    }

    private static bool TryGetProjectedGravitySlideDirection(Vector2 surfaceNormal, out Vector2 slideDirection)
    {
        slideDirection = Vector2.zero;
        if (surfaceNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 normal = surfaceNormal.normalized;
        Vector2 gravityDirection = Physics2D.gravity.sqrMagnitude > 0.0001f
            ? Physics2D.gravity.normalized
            : Vector2.down;

        Vector2 projectedGravity = gravityDirection - Vector2.Dot(gravityDirection, normal) * normal;
        if (projectedGravity.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        slideDirection = projectedGravity.normalized;
        return true;
    }

    private void OnValidate()
    {
        groundSlideAcceleration = Mathf.Max(0f, groundSlideAcceleration);
        maxGroundSlideSpeed = Mathf.Max(0f, maxGroundSlideSpeed);
        groundInputControl = Mathf.Clamp01(groundInputControl);
        exitCarryTime = Mathf.Max(0f, exitCarryTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.65f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Vector3 start = transform.position;
        Gizmos.DrawLine(start, start + Vector3.down * Mathf.Max(0.25f, maxGroundSlideSpeed * 0.15f));
    }
}
