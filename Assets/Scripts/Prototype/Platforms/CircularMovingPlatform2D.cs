using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
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

    private const float MinMoveDistance = 0.0001f;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    private readonly Collider2D[] riderHits = new Collider2D[16];
    private readonly List<Rigidbody2D> carriedBodies = new List<Rigidbody2D>(4);

    private Rigidbody2D body;
    private Collider2D platformCollider;
    private Vector2 fallbackCenter;
    private float currentAngleRadians;

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

        Vector2 center = transform.TransformPoint(riderProbeOffset);
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
        for (int i = 0; i < carriedBodies.Count; i++)
        {
            Rigidbody2D riderBody = carriedBodies[i];
            if (riderBody != null)
            {
                riderBody.position += delta;
            }
        }
    }

    private void OnValidate()
    {
        orbitRadius = Mathf.Max(0.01f, orbitRadius);
        angularSpeedDegrees = Mathf.Max(0f, angularSpeedDegrees);
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
