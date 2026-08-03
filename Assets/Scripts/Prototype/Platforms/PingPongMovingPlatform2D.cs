using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
[DefaultExecutionOrder(100)]
public sealed class PingPongMovingPlatform2D : MonoBehaviour
{
    [Header("Ping Pong Path")]
    [SerializeField] private Transform pathCenter;
    [SerializeField] private Vector2 pathOffset = new Vector2(0f, 5f);
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField] private bool startAtPositiveEnd;
    [SerializeField] private bool snapToPathStartOnEnable = true;
    [SerializeField, Min(0f)] private float endpointPause;
    [SerializeField, Min(0f)] private float arrivalTolerance = 0.01f;

    [Header("Blocking")]
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private bool ignorePlayerCharacters = true;
    [SerializeField, Min(0f)] private float skinWidth = 0.01f;
    [SerializeField, Range(0f, 1f)] private float minBlockingNormalDot = 0.35f;

    [Header("Rider Carry")]
    [SerializeField] private bool carryRiders = true;
    [SerializeField] private LayerMask riderMask = ~0;
    [SerializeField] private Vector2 riderProbeSize = new Vector2(1.9f, 0.18f);
    [SerializeField] private Vector2 riderProbeOffset = new Vector2(0f, 0.5f);

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;

    [Header("Game Trajectory Preview")]
    [SerializeField] private bool showTrajectoryInGame = true;
    [SerializeField] private Color trajectoryColor = new Color(1f, 0.78f, 0.16f, 0.72f);
    [SerializeField, Min(0.005f)] private float trajectoryWidth = 0.055f;
    [SerializeField] private int trajectorySortingOrder = 90;

    private const float MinMoveDistance = 0.0001f;
    private const float RiderProbePenetration = 0.04f;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    private readonly Collider2D[] riderHits = new Collider2D[16];
    private readonly List<Rigidbody2D> carriedBodies = new List<Rigidbody2D>(4);
    private readonly Dictionary<Rigidbody2D, Vector2> riderRelativePositions =
        new Dictionary<Rigidbody2D, Vector2>(4);
    private readonly List<Rigidbody2D> staleRiders = new List<Rigidbody2D>(4);

    private Rigidbody2D body;
    private Collider2D platformCollider;
    private Vector2 fallbackCenter;
    private bool movingTowardPositiveEnd;
    private float endpointPauseTimer;
    private MovingPlatformTrajectoryLine2D trajectoryPreview;

    public Vector2 PathOffset => pathOffset;
    public float MoveSpeed => moveSpeed;
    public Vector2 NegativeEndpoint => GetCenterPosition() - GetWorldPathOffset() * 0.5f;
    public Vector2 PositiveEndpoint => GetCenterPosition() + GetWorldPathOffset() * 0.5f;

    private void Awake()
    {
        CacheReferences();
        ConfigureBody();
        InitializePath(snapToPathStartOnEnable);
    }

    private void OnEnable()
    {
        CacheReferences();
        if (Application.isPlaying && body != null)
        {
            InitializePath(snapToPathStartOnEnable);
        }
    }

    private void OnDisable()
    {
        trajectoryPreview?.Hide();
        carriedBodies.Clear();
        riderRelativePositions.Clear();
        staleRiders.Clear();
    }

    private void OnDestroy()
    {
        trajectoryPreview?.Dispose();
    }

    private void LateUpdate()
    {
        if (!showTrajectoryInGame)
        {
            trajectoryPreview?.Hide();
            return;
        }

        if (trajectoryPreview == null)
        {
            trajectoryPreview = new MovingPlatformTrajectoryLine2D(transform);
        }

        trajectoryPreview.DrawSegment(
            NegativeEndpoint,
            PositiveEndpoint,
            trajectoryColor,
            trajectoryWidth,
            trajectorySortingOrder);
    }

    private void FixedUpdate()
    {
        CacheReferences();
        if (body == null || moveSpeed <= 0f || pathOffset.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (endpointPauseTimer > 0f)
        {
            endpointPauseTimer = Mathf.Max(0f, endpointPauseTimer - Time.fixedDeltaTime);
            return;
        }

        Vector2 targetPosition = movingTowardPositiveEnd ? PositiveEndpoint : NegativeEndpoint;
        Vector2 toTarget = targetPosition - body.position;
        float remainingDistance = toTarget.magnitude;
        if (remainingDistance <= arrivalTolerance)
        {
            ReverseAtEndpoint();
            return;
        }

        Vector2 moveDirection = toTarget / remainingDistance;
        float wantedDistance = Mathf.Min(moveSpeed * Time.fixedDeltaTime, remainingDistance);
        float allowedDistance = GetAllowedMoveDistance(moveDirection, wantedDistance);
        Vector2 allowedDelta = moveDirection * allowedDistance;
        if (allowedDelta.sqrMagnitude > MinMoveDistance * MinMoveDistance)
        {
            CollectRiders();
            body.MovePosition(body.position + allowedDelta);
            CarryCollectedRiders(allowedDelta);
        }

        if (allowedDistance >= remainingDistance - arrivalTolerance)
        {
            ReverseAtEndpoint();
        }
    }

    [ContextMenu("Snap To Path Start")]
    public void SnapToPathStart()
    {
        CacheReferences();
        InitializePath(true);
    }

    private void InitializePath(bool snapToStart)
    {
        Vector2 selectedStartOffset = GetSelectedStartOffset();
        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
        if (pathCenter == null)
        {
            fallbackCenter = currentPosition - selectedStartOffset;
        }

        movingTowardPositiveEnd = !startAtPositiveEnd;
        endpointPauseTimer = 0f;
        riderRelativePositions.Clear();

        if (!snapToStart)
        {
            Vector2 center = GetCenterPosition();
            float positionAlongPath = Vector2.Dot(
                currentPosition - center,
                GetWorldPathOffset().normalized);
            movingTowardPositiveEnd = positionAlongPath <= 0f;
            return;
        }

        Vector2 startPosition = GetCenterPosition() + selectedStartOffset;
        if (body != null && Application.isPlaying)
        {
            body.position = startPosition;
        }
        else
        {
            transform.position = startPosition;
        }
    }

    private Vector2 GetSelectedStartOffset()
    {
        Vector2 halfOffset = GetWorldPathOffset() * 0.5f;
        return startAtPositiveEnd ? halfOffset : -halfOffset;
    }

    private Vector2 GetCenterPosition()
    {
        return pathCenter != null ? (Vector2)pathCenter.position : fallbackCenter;
    }

    private Vector2 GetWorldPathOffset()
    {
        if (pathCenter == null)
        {
            return pathOffset;
        }

        return pathCenter.rotation * pathOffset;
    }

    private void ReverseAtEndpoint()
    {
        movingTowardPositiveEnd = !movingTowardPositiveEnd;
        endpointPauseTimer = endpointPause;
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

    private float GetAllowedMoveDistance(Vector2 direction, float wantedDistance)
    {
        if (wantedDistance <= 0f || platformCollider == null || !platformCollider.enabled)
        {
            return wantedDistance;
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
        return other.transform == transform ||
            other.transform.IsChildOf(transform) ||
            transform.IsChildOf(other.transform);
    }

    private void CollectRiders()
    {
        carriedBodies.Clear();
        if (!carryRiders || platformCollider == null || !platformCollider.enabled)
        {
            return;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(riderMask);
        filter.useTriggers = false;

        GetRiderProbeGeometry(out Vector2 center, out Vector2 size, out float angle);
        int hitCount = Physics2D.OverlapBox(center, size, angle, filter, riderHits);
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

            PlayerCharacter player = hit.GetComponentInParent<PlayerCharacter>();
            bool isKnownRider = riderRelativePositions.ContainsKey(riderBody);
            bool canRide = player == null ||
                (isKnownRider
                    ? player.CanRideMovingPlatform(platformCollider, 0.28f)
                    : player.IsStandingOnCollider(platformCollider));
            if (!canRide)
            {
                continue;
            }

            if (!carriedBodies.Contains(riderBody))
            {
                carriedBodies.Add(riderBody);
            }

            if (player != null && !riderRelativePositions.ContainsKey(riderBody))
            {
                riderRelativePositions.Add(riderBody, riderBody.position - body.position);
            }
        }

        CollectPersistedRiders();
    }

    private void CarryCollectedRiders(Vector2 delta)
    {
        for (int i = 0; i < carriedBodies.Count; i++)
        {
            Rigidbody2D riderBody = carriedBodies[i];
            if (riderBody == null)
            {
                continue;
            }

            PlayerCharacter player = riderBody.GetComponent<PlayerCharacter>();
            if (player == null)
            {
                riderBody.position += delta;
                continue;
            }

            if (!riderRelativePositions.TryGetValue(riderBody, out Vector2 relativePosition))
            {
                relativePosition = riderBody.position - body.position;
                riderRelativePositions[riderBody] = relativePosition;
            }

            if (!Mathf.Approximately(player.CurrentMoveInput, 0f))
            {
                relativePosition.x += riderBody.linearVelocity.x * Time.fixedDeltaTime;
                riderRelativePositions[riderBody] = relativePosition;
            }

            Vector2 targetPosition = body.position + delta + relativePosition;
            player.ApplyMovingPlatformTargetPosition(targetPosition, platformCollider);
        }
    }

    private void CollectPersistedRiders()
    {
        staleRiders.Clear();
        foreach (KeyValuePair<Rigidbody2D, Vector2> pair in riderRelativePositions)
        {
            Rigidbody2D riderBody = pair.Key;
            if (riderBody == null)
            {
                staleRiders.Add(riderBody);
                continue;
            }

            if (carriedBodies.Contains(riderBody))
            {
                continue;
            }

            PlayerCharacter player = riderBody.GetComponent<PlayerCharacter>();
            if (player != null && player.CanRideMovingPlatform(platformCollider, 0.28f))
            {
                carriedBodies.Add(riderBody);
                continue;
            }

            staleRiders.Add(riderBody);
        }

        for (int i = 0; i < staleRiders.Count; i++)
        {
            riderRelativePositions.Remove(staleRiders[i]);
        }
    }

    private void GetRiderProbeGeometry(out Vector2 center, out Vector2 size, out float angle)
    {
        float probeHeight = Mathf.Max(0.01f, riderProbeSize.y);
        if (platformCollider is BoxCollider2D box)
        {
            Transform colliderTransform = box.transform;
            float worldWidth = box.size.x *
                ((Vector2)colliderTransform.TransformVector(Vector2.right)).magnitude;
            Vector2 topCenter = colliderTransform.TransformPoint(
                box.offset + Vector2.up * (box.size.y * 0.5f));
            Vector2 up = colliderTransform.up;

            center = topCenter +
                up * ((probeHeight - RiderProbePenetration) * 0.5f) +
                (Vector2)colliderTransform.right * riderProbeOffset.x;
            size = new Vector2(
                Mathf.Max(worldWidth, riderProbeSize.x),
                probeHeight + RiderProbePenetration);
            angle = colliderTransform.eulerAngles.z;
            return;
        }

        Bounds bounds = platformCollider.bounds;
        center = new Vector2(
            bounds.center.x + riderProbeOffset.x,
            bounds.max.y + (probeHeight - RiderProbePenetration) * 0.5f);
        size = new Vector2(
            Mathf.Max(bounds.size.x, riderProbeSize.x),
            probeHeight + RiderProbePenetration);
        angle = 0f;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        endpointPause = Mathf.Max(0f, endpointPause);
        arrivalTolerance = Mathf.Max(0f, arrivalTolerance);
        trajectoryWidth = Mathf.Max(0.005f, trajectoryWidth);
        skinWidth = Mathf.Max(0f, skinWidth);
        riderProbeSize = new Vector2(
            Mathf.Max(0.01f, riderProbeSize.x),
            Mathf.Max(0.01f, riderProbeSize.y));
        CacheReferences();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Transform centerTransform = pathCenter != null ? pathCenter : transform.parent;
        Vector3 center = centerTransform != null ? centerTransform.position : transform.position;
        Quaternion pathRotation = centerTransform != null ? centerTransform.rotation : Quaternion.identity;
        Vector3 worldOffset = pathRotation * pathOffset;
        Vector3 negativeEndpoint = center - worldOffset * 0.5f;
        Vector3 positiveEndpoint = center + worldOffset * 0.5f;

        Gizmos.color = new Color(1f, 0.78f, 0.16f, 0.95f);
        Gizmos.DrawLine(negativeEndpoint, positiveEndpoint);
        Gizmos.DrawWireSphere(negativeEndpoint, 0.08f);
        Gizmos.DrawWireSphere(positiveEndpoint, 0.08f);

        Vector3 direction = worldOffset.sqrMagnitude > 0.0001f
            ? worldOffset.normalized
            : Vector3.right;
        Vector3 midpoint = Vector3.Lerp(negativeEndpoint, positiveEndpoint, 0.5f);
        Gizmos.DrawLine(midpoint - direction * 0.16f, midpoint + direction * 0.16f);
    }
#endif
}
