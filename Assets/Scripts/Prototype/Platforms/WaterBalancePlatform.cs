using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class WaterBalancePlatform : MonoBehaviour
{
    [Header("Platforms")]
    [SerializeField] private Transform leftPlatform;
    [SerializeField] private Transform rightPlatform;
    [SerializeField] private Collider2D leftWaterSensor;
    [SerializeField] private Collider2D rightWaterSensor;

    [Header("Motion")]
    [SerializeField] private float heightOffset = 1.25f;
    [SerializeField] private float downMoveSpeed = 2.2f;
    [SerializeField] private float upMoveSpeed = 2.2f;
    [FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float neutralMoveSpeed = 1.8f;

    [Header("Water Detection")]
    [SerializeField] private LayerMask waterLayerMask = 0;
    [SerializeField] private float wetHoldTime = 0.35f;
    [SerializeField] private bool sensorsFollowPlatforms = true;

    [Header("Rider Carry")]
    [SerializeField] private bool carryRiders = true;
    [SerializeField] private LayerMask riderMask = ~0;
    [SerializeField] private float riderProbeHeight = 0.18f;
    [SerializeField] private float riderProbeExtraWidth = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool debugGizmos = true;

    private const float MinMoveDistance = 0.0001f;
    private readonly Collider2D[] waterHits = new Collider2D[32];
    private readonly Collider2D[] riderHits = new Collider2D[16];
    private readonly List<Rigidbody2D> carriedBodies = new List<Rigidbody2D>(4);

    private Rigidbody2D leftBody;
    private Rigidbody2D rightBody;
    private Collider2D leftPlatformCollider;
    private Collider2D rightPlatformCollider;
    private Vector2 leftNeutralPosition;
    private Vector2 rightNeutralPosition;
    private Vector2 previousLeftPosition;
    private Vector2 previousRightPosition;
    private Vector2 leftSensorOffsetFromPlatform;
    private Vector2 rightSensorOffsetFromPlatform;
    private float leftWetUntilTime;
    private float rightWetUntilTime;

    public bool LeftWet { get; private set; }
    public bool RightWet { get; private set; }

    private void Reset()
    {
        Transform left = transform.Find("LeftPlatform");
        Transform right = transform.Find("RightPlatform");
        Transform leftSensor = transform.Find("LeftWaterSensor");
        Transform rightSensor = transform.Find("RightWaterSensor");

        leftPlatform = left;
        rightPlatform = right;
        leftWaterSensor = leftSensor != null ? leftSensor.GetComponent<Collider2D>() : null;
        rightWaterSensor = rightSensor != null ? rightSensor.GetComponent<Collider2D>() : null;
    }

    private void Awake()
    {
        CacheReferences();
        ConfigurePlatformBody(leftBody);
        ConfigurePlatformBody(rightBody);
        CaptureNeutralPositions();
        CaptureSensorOffsets();
    }

    private void OnEnable()
    {
        CacheReferences();
        ConfigurePlatformBody(leftBody);
        ConfigurePlatformBody(rightBody);

        if (Application.isPlaying)
        {
            CaptureNeutralPositions();
            CaptureSensorOffsets();
        }
    }

    private void OnValidate()
    {
        heightOffset = Mathf.Max(0f, heightOffset);
        downMoveSpeed = Mathf.Max(0f, downMoveSpeed);
        upMoveSpeed = Mathf.Max(0f, upMoveSpeed);
        neutralMoveSpeed = Mathf.Max(0f, neutralMoveSpeed);
        wetHoldTime = Mathf.Max(0f, wetHoldTime);
        riderProbeHeight = Mathf.Max(0.01f, riderProbeHeight);
        riderProbeExtraWidth = Mathf.Max(0f, riderProbeExtraWidth);
    }

    private void FixedUpdate()
    {
        CacheReferences();
        if (leftPlatform == null || rightPlatform == null)
        {
            return;
        }

        SyncSensorsToPlatforms();

        LeftWet = GetStableWetState(leftWaterSensor, ref leftWetUntilTime);
        RightWet = GetStableWetState(rightWaterSensor, ref rightWetUntilTime);

        Vector2 leftTarget = leftNeutralPosition;
        Vector2 rightTarget = rightNeutralPosition;
        float leftSpeed = neutralMoveSpeed;
        float rightSpeed = neutralMoveSpeed;

        if (LeftWet && !RightWet)
        {
            leftTarget += Vector2.down * heightOffset;
            rightTarget += Vector2.up * heightOffset;
            leftSpeed = downMoveSpeed;
            rightSpeed = upMoveSpeed;
        }
        else if (RightWet && !LeftWet)
        {
            leftTarget += Vector2.up * heightOffset;
            rightTarget += Vector2.down * heightOffset;
            leftSpeed = upMoveSpeed;
            rightSpeed = downMoveSpeed;
        }

        MovePlatform(leftPlatform, leftBody, leftPlatformCollider, leftTarget, leftSpeed, ref previousLeftPosition);
        MovePlatform(rightPlatform, rightBody, rightPlatformCollider, rightTarget, rightSpeed, ref previousRightPosition);
        SyncSensorsToPlatforms();
    }

    private void CacheReferences()
    {
        if (leftPlatform != null)
        {
            if (leftBody == null)
            {
                leftBody = leftPlatform.GetComponent<Rigidbody2D>();
            }

            if (leftPlatformCollider == null)
            {
                leftPlatformCollider = leftPlatform.GetComponent<Collider2D>();
            }
        }

        if (rightPlatform != null)
        {
            if (rightBody == null)
            {
                rightBody = rightPlatform.GetComponent<Rigidbody2D>();
            }

            if (rightPlatformCollider == null)
            {
                rightPlatformCollider = rightPlatform.GetComponent<Collider2D>();
            }
        }
    }

    private void CaptureNeutralPositions()
    {
        if (leftPlatform != null)
        {
            leftNeutralPosition = leftBody != null ? leftBody.position : (Vector2)leftPlatform.position;
            previousLeftPosition = leftNeutralPosition;
        }

        if (rightPlatform != null)
        {
            rightNeutralPosition = rightBody != null ? rightBody.position : (Vector2)rightPlatform.position;
            previousRightPosition = rightNeutralPosition;
        }
    }

    private void CaptureSensorOffsets()
    {
        leftSensorOffsetFromPlatform = GetSensorOffset(leftPlatform, leftWaterSensor);
        rightSensorOffsetFromPlatform = GetSensorOffset(rightPlatform, rightWaterSensor);
    }

    private static Vector2 GetSensorOffset(Transform platform, Collider2D sensor)
    {
        if (platform == null || sensor == null)
        {
            return Vector2.zero;
        }

        return (Vector2)sensor.transform.position - (Vector2)platform.position;
    }

    private void SyncSensorsToPlatforms()
    {
        if (!sensorsFollowPlatforms)
        {
            return;
        }

        SyncSensorToPlatform(leftPlatform, leftWaterSensor, leftSensorOffsetFromPlatform);
        SyncSensorToPlatform(rightPlatform, rightWaterSensor, rightSensorOffsetFromPlatform);
    }

    private static void SyncSensorToPlatform(Transform platform, Collider2D sensor, Vector2 offset)
    {
        if (platform == null || sensor == null || sensor.transform.IsChildOf(platform))
        {
            return;
        }

        sensor.transform.position = (Vector2)platform.position + offset;
    }

    private static void ConfigurePlatformBody(Rigidbody2D platformBody)
    {
        if (platformBody == null)
        {
            return;
        }

        platformBody.bodyType = RigidbodyType2D.Kinematic;
        platformBody.gravityScale = 0f;
        platformBody.freezeRotation = true;
        platformBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        platformBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private bool IsSensorWet(Collider2D sensor)
    {
        if (sensor == null || !sensor.enabled)
        {
            return false;
        }

        sensor.isTrigger = true;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(~0);
        filter.useTriggers = true;

        int hitCount = sensor.Overlap(filter, waterHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = waterHits[i];
            if (hit == null || hit == sensor)
            {
                continue;
            }

            if (IsWaterCollider(hit))
            {
                return true;
            }
        }

        return false;
    }

    private bool GetStableWetState(Collider2D sensor, ref float wetUntilTime)
    {
        if (IsSensorWet(sensor))
        {
            wetUntilTime = Time.time + wetHoldTime;
            return true;
        }

        return Time.time <= wetUntilTime;
    }

    private bool IsWaterCollider(Collider2D hit)
    {
        bool hasWaterComponent = hit.GetComponentInParent<WaterFlowBlob>() != null ||
            hit.GetComponentInParent<WaterHazard>() != null;
        if (hasWaterComponent)
        {
            return true;
        }

        return waterLayerMask.value != 0 && ((1 << hit.gameObject.layer) & waterLayerMask.value) != 0;
    }

    private void MovePlatform(
        Transform platform,
        Rigidbody2D platformBody,
        Collider2D platformCollider,
        Vector2 target,
        float speed,
        ref Vector2 previousPosition)
    {
        Vector2 current = platformBody != null ? platformBody.position : (Vector2)platform.position;
        Vector2 next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);
        Vector2 delta = next - current;

        if (delta.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            previousPosition = current;
            return;
        }

        CollectRiders(platform, platformCollider);

        if (platformBody != null)
        {
            platformBody.MovePosition(next);
        }
        else
        {
            platform.position = next;
        }

        CarryCollectedRiders(delta);
        previousPosition = next;
    }

    private void CollectRiders(Transform platform, Collider2D platformCollider)
    {
        carriedBodies.Clear();
        if (!carryRiders || platform == null || platformCollider == null)
        {
            return;
        }

        Bounds bounds = platformCollider.bounds;
        Vector2 center = new Vector2(bounds.center.x, bounds.max.y + riderProbeHeight * 0.5f);
        Vector2 size = new Vector2(bounds.size.x + riderProbeExtraWidth, riderProbeHeight);

        var filter = new ContactFilter2D();
        filter.SetLayerMask(riderMask);
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapBox(center, size, platform.eulerAngles.z, filter, riderHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = riderHits[i];
            if (hit == null || hit.isTrigger || hit.transform == platform || hit.transform.IsChildOf(platform))
            {
                continue;
            }

            Rigidbody2D riderBody = hit.attachedRigidbody;
            if (riderBody == null || riderBody.bodyType == RigidbodyType2D.Static)
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

            riderBody.position += delta;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        DrawPlatformTargetGizmos(leftPlatform, leftNeutralPosition, true);
        DrawPlatformTargetGizmos(rightPlatform, rightNeutralPosition, false);
        DrawSensorGizmo(leftWaterSensor, LeftWet);
        DrawSensorGizmo(rightWaterSensor, RightWet);
    }

    private void DrawPlatformTargetGizmos(Transform platform, Vector2 storedNeutral, bool isLeft)
    {
        if (platform == null)
        {
            return;
        }

        Vector2 neutral = Application.isPlaying ? storedNeutral : (Vector2)platform.position;
        Vector2 down = neutral + Vector2.down * heightOffset;
        Vector2 up = neutral + Vector2.up * heightOffset;

        Gizmos.color = isLeft ? new Color(0.35f, 0.75f, 1f, 0.9f) : new Color(1f, 0.75f, 0.3f, 0.9f);
        Gizmos.DrawLine(down, up);
        Gizmos.DrawWireSphere(down, 0.07f);
        Gizmos.DrawWireSphere(neutral, 0.07f);
        Gizmos.DrawWireSphere(up, 0.07f);
    }

    private static void DrawSensorGizmo(Collider2D sensor, bool wet)
    {
        if (sensor == null)
        {
            return;
        }

        Gizmos.color = wet ? new Color(0.15f, 0.8f, 1f, 0.9f) : new Color(0.15f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(sensor.bounds.center, sensor.bounds.size);
    }
}
