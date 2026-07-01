using System.Collections.Generic;
using UnityEngine;

public enum MovingPlatformLimitMode
{
    Unlimited,
    LocalDistance,
    WorldTarget
}

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MovingPlatform2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Vector2 moveDirection = Vector2.right;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private MovingPlatformLimitMode limitMode = MovingPlatformLimitMode.Unlimited;
    [SerializeField] private float localDistance = 6f;
    [SerializeField] private Vector2 worldTargetPosition;
    [SerializeField] private float arrivalTolerance = 0.03f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private bool ignorePlayerCharacters = true;
    [SerializeField] private float skinWidth = 0.01f;
    [SerializeField, Range(0f, 1f)] private float minBlockingNormalDot = 0.35f;

    [Header("Rider Carry")]
    [SerializeField] private bool carryRiders = true;
    [SerializeField] private LayerMask riderMask = ~0;
    [SerializeField] private Vector2 riderProbeSize = new Vector2(1.75f, 0.14f);
    [SerializeField] private Vector2 riderProbeOffset = new Vector2(0f, 0.47f);

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;

    private const float MinMoveDistance = 0.0001f;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    private readonly Collider2D[] riderHits = new Collider2D[16];
    private readonly List<Rigidbody2D> carriedBodies = new List<Rigidbody2D>(4);

    private Rigidbody2D body;
    private Collider2D platformCollider;
    private Vector2 startPosition;

    public Vector2 MoveDirection => GetNormalizedMoveDirection();
    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        CacheReferences();
        ConfigureBody();
        startPosition = body.position;
    }

    private void OnEnable()
    {
        CacheReferences();
        if (Application.isPlaying && body != null)
        {
            startPosition = body.position;
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        localDistance = Mathf.Max(0f, localDistance);
        arrivalTolerance = Mathf.Max(0f, arrivalTolerance);
        skinWidth = Mathf.Max(0f, skinWidth);
        riderProbeSize = new Vector2(Mathf.Max(0.01f, riderProbeSize.x), Mathf.Max(0.01f, riderProbeSize.y));
    }

    private void FixedUpdate()
    {
        CacheReferences();
        if (body == null || platformCollider == null || moveSpeed <= 0f)
        {
            return;
        }

        Vector2 delta = CalculateAllowedDelta(Time.fixedDeltaTime);
        if (delta.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return;
        }

        CollectRiders();
        body.MovePosition(body.position + delta);
        CarryCollectedRiders(delta);
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureBody()
    {
        if (body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private Vector2 CalculateAllowedDelta(float deltaTime)
    {
        Vector2 direction = GetDesiredDirection(out float remainingDistance);
        if (direction.sqrMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        float wantedDistance = moveSpeed * deltaTime;
        if (!float.IsPositiveInfinity(remainingDistance))
        {
            wantedDistance = Mathf.Min(wantedDistance, remainingDistance);
        }

        float allowedDistance = GetAllowedMoveDistance(direction, wantedDistance);
        return direction * allowedDistance;
    }

    private Vector2 GetDesiredDirection(out float remainingDistance)
    {
        remainingDistance = float.PositiveInfinity;

        if (limitMode == MovingPlatformLimitMode.WorldTarget)
        {
            Vector2 toTarget = worldTargetPosition - body.position;
            float distance = toTarget.magnitude;
            if (distance <= arrivalTolerance)
            {
                remainingDistance = 0f;
                return Vector2.zero;
            }

            remainingDistance = distance;
            return toTarget / distance;
        }

        Vector2 direction = GetNormalizedMoveDirection();
        if (limitMode == MovingPlatformLimitMode.LocalDistance)
        {
            float travelled = Vector2.Dot(body.position - startPosition, direction);
            remainingDistance = Mathf.Max(0f, localDistance - travelled);
            if (remainingDistance <= arrivalTolerance)
            {
                return Vector2.zero;
            }
        }

        return direction;
    }

    private Vector2 GetNormalizedMoveDirection()
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.right;
        }

        return moveDirection.normalized;
    }

    private float GetAllowedMoveDistance(Vector2 direction, float wantedDistance)
    {
        if (wantedDistance <= 0f)
        {
            return 0f;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(blockingMask);
        filter.useTriggers = false;

        float allowedDistance = wantedDistance;
        int hitCount = platformCollider.Cast(direction, filter, castHits, wantedDistance + skinWidth);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider.isTrigger || IsOwnCollider(hit.collider))
            {
                continue;
            }

            if (ignorePlayerCharacters && hit.collider.GetComponentInParent<PlayerCharacter>() != null)
            {
                continue;
            }

            float blockingDot = Vector2.Dot(hit.normal, -direction);
            if (blockingDot < minBlockingNormalDot)
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - skinWidth));
        }

        return allowedDistance;
    }

    private bool IsOwnCollider(Collider2D other)
    {
        return other.transform == transform || other.transform.IsChildOf(transform);
    }

    private void CollectRiders()
    {
        carriedBodies.Clear();
        if (!carryRiders)
        {
            return;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(riderMask);
        filter.useTriggers = false;

        Vector2 center = (Vector2)transform.TransformPoint(riderProbeOffset);
        float angle = transform.eulerAngles.z;
        int hitCount = Physics2D.OverlapBox(center, riderProbeSize, angle, filter, riderHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = riderHits[i];
            if (hit == null || hit.isTrigger || IsOwnCollider(hit))
            {
                continue;
            }

            Rigidbody2D riderBody = hit.attachedRigidbody;
            if (riderBody == null || riderBody == body || riderBody.bodyType == RigidbodyType2D.Static)
            {
                continue;
            }

            if (!carriedBodies.Contains(riderBody))
            {
                carriedBodies.Add(riderBody);
            }
        }
    }

    private void CarryCollectedRiders(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0f)
        {
            return;
        }

        for (int i = 0; i < carriedBodies.Count; i++)
        {
            Rigidbody2D riderBody = carriedBodies[i];
            if (riderBody == null)
            {
                continue;
            }

            riderBody.position = riderBody.position + delta;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector2 direction = Application.isPlaying && body != null ? GetNormalizedMoveDirection() : (moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector2.right);
        Vector3 origin = transform.position;
        Gizmos.color = new Color(1f, 0.84f, 0.15f, 0.9f);
        Gizmos.DrawLine(origin, origin + (Vector3)(direction * 1.25f));
        Gizmos.DrawWireSphere(origin + (Vector3)(direction * 1.25f), 0.06f);

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.75f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(riderProbeOffset), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(riderProbeSize.x, riderProbeSize.y, 0f));
        Gizmos.matrix = oldMatrix;
    }
}
