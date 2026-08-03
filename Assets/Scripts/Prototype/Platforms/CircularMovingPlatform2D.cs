using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
[DefaultExecutionOrder(100)]
public sealed class CircularMovingPlatform2D : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField] private Transform orbitCenter;
    [SerializeField, Min(0.01f)] private float orbitRadius = 2.5f;
    [SerializeField, Range(-360f, 360f)] private float startAngleDegrees;
    [SerializeField, Min(0f)] private float angularSpeedDegrees = 45f;
    [SerializeField] private bool clockwise;
    [SerializeField] private bool snapToStartOnEnable = true;

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
    [SerializeField, Range(12, 160)] private int trajectorySegments = 72;
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
    private float currentAngleRadians;
    private MovingPlatformTrajectoryLine2D trajectoryPreview;

    public float OrbitRadius => orbitRadius;
    public float AngularSpeedDegrees => angularSpeedDegrees;
    public bool Clockwise => clockwise;

    private void Awake()
    {
        CacheReferences();
        ConfigureBody();
        InitializeOrbit(snapToStartOnEnable);
    }

    private void OnEnable()
    {
        CacheReferences();
        if (Application.isPlaying && body != null)
        {
            InitializeOrbit(snapToStartOnEnable);
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

        trajectoryPreview.DrawCircle(
            GetCenterPosition(),
            orbitRadius,
            trajectorySegments,
            trajectoryColor,
            trajectoryWidth,
            trajectorySortingOrder);
    }

    private void FixedUpdate()
    {
        CacheReferences();
        if (body == null || angularSpeedDegrees <= 0f)
        {
            return;
        }

        float directionSign = clockwise ? -1f : 1f;
        float angleStep = angularSpeedDegrees * Mathf.Deg2Rad * directionSign * Time.fixedDeltaTime;
        float nextAngle = currentAngleRadians + angleStep;
        Vector2 center = GetCenterPosition();
        Vector2 targetPosition = center + GetRadialOffset(nextAngle);
        Vector2 wantedDelta = targetPosition - body.position;
        float wantedDistance = wantedDelta.magnitude;
        if (wantedDistance <= MinMoveDistance)
        {
            currentAngleRadians = nextAngle;
            return;
        }

        Vector2 moveDirection = wantedDelta / wantedDistance;
        float allowedDistance = GetAllowedMoveDistance(moveDirection, wantedDistance);
        Vector2 allowedDelta = moveDirection * allowedDistance;
        if (allowedDelta.sqrMagnitude > MinMoveDistance * MinMoveDistance)
        {
            CollectRiders();
            body.MovePosition(body.position + allowedDelta);
            CarryCollectedRiders(allowedDelta);
        }

        if (allowedDistance >= wantedDistance - MinMoveDistance)
        {
            currentAngleRadians = nextAngle;
        }
    }

    [ContextMenu("Snap To Orbit Start")]
    public void SnapToOrbitStart()
    {
        CacheReferences();
        currentAngleRadians = startAngleDegrees * Mathf.Deg2Rad;
        Vector2 startPosition = GetCenterPosition() + GetRadialOffset(currentAngleRadians);
        if (body != null && Application.isPlaying)
        {
            body.position = startPosition;
        }
        else
        {
            transform.position = startPosition;
        }
    }

    private void InitializeOrbit(bool snapToStart)
    {
        currentAngleRadians = startAngleDegrees * Mathf.Deg2Rad;
        fallbackCenter = body != null
            ? body.position - GetRadialOffset(currentAngleRadians)
            : (Vector2)transform.position - GetRadialOffset(currentAngleRadians);

        if (!snapToStart)
        {
            Vector2 fromCenter = (body != null ? body.position : (Vector2)transform.position) - GetCenterPosition();
            if (fromCenter.sqrMagnitude > 0.0001f)
            {
                currentAngleRadians = Mathf.Atan2(fromCenter.y, fromCenter.x);
            }

            return;
        }

        Vector2 startPosition = GetCenterPosition() + GetRadialOffset(currentAngleRadians);
        if (body != null)
        {
            body.position = startPosition;
        }
        else
        {
            transform.position = startPosition;
        }
    }

    private Vector2 GetCenterPosition()
    {
        if (orbitCenter != null)
        {
            return orbitCenter.position;
        }

        return fallbackCenter;
    }

    private Vector2 GetRadialOffset(float angleRadians)
    {
        return new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * orbitRadius;
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
            if (riderBody != null)
            {
                PlayerCharacter player = riderBody.GetComponent<PlayerCharacter>();
                if (player != null)
                {
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
                else
                {
                    riderBody.position += delta;
                }
            }
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
        orbitRadius = Mathf.Max(0.01f, orbitRadius);
        angularSpeedDegrees = Mathf.Max(0f, angularSpeedDegrees);
        trajectoryWidth = Mathf.Max(0.005f, trajectoryWidth);
        trajectorySegments = Mathf.Clamp(trajectorySegments, 12, 160);
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

        Transform centerTransform = orbitCenter != null ? orbitCenter : transform.parent;
        Vector3 center = centerTransform != null
            ? centerTransform.position
            : transform.position - (Vector3)GetRadialOffset(startAngleDegrees * Mathf.Deg2Rad);

        UnityEditor.Handles.color = new Color(1f, 0.78f, 0.16f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, orbitRadius);

        float startRadians = startAngleDegrees * Mathf.Deg2Rad;
        Vector3 startPoint = center + (Vector3)GetRadialOffset(startRadians);
        Vector2 tangent = clockwise
            ? new Vector2(Mathf.Sin(startRadians), -Mathf.Cos(startRadians))
            : new Vector2(-Mathf.Sin(startRadians), Mathf.Cos(startRadians));

        Gizmos.color = new Color(1f, 0.92f, 0.35f, 0.95f);
        Gizmos.DrawWireSphere(startPoint, 0.08f);
        Gizmos.DrawLine(startPoint, startPoint + (Vector3)(tangent * 0.45f));
    }
#endif
}
