using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LeafBouncePlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    private static readonly Color FullWidthGizmoColor = new Color(0.2f, 1f, 0.35f, 0.7f);
    private static readonly Color SweetSpotFillGizmoColor = new Color(1f, 0.9f, 0.05f, 0.22f);
    private static readonly Color SweetSpotEdgeGizmoColor = new Color(1f, 0.9f, 0.05f, 0.95f);
    private static readonly Color LaunchDirectionGizmoColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    [Header("Leaf Accuracy")]
    [SerializeField] private float maxAngleError = 18f;
    [SerializeField, Range(0f, 1f)] private float edgeSpeedMultiplier = 0.75f;
    [SerializeField, Range(0f, 1f)] private float sweetSpotWidth = 0.25f;
    [SerializeField, Range(-1f, 1f)] private float sweetSpotOffset;

    [Header("Dive Height Power")]
    [SerializeField] private float minEffectiveDiveHeight = 1f;
    [SerializeField] private float maxEffectiveDiveHeight = 6f;
    [SerializeField] private float minBounceSpeed = 9f;
    [SerializeField] private float maxBounceSpeed = 18f;

    [Header("Bounce Direction Strength")]
    [SerializeField] private float verticalBounceMultiplier = 1.25f;
    [SerializeField] private float horizontalBounceMultiplier = 1f;

    [Header("Curved Launch")]
    [SerializeField] private float horizontalBuildDuration = 0.45f;

    [Header("Collider")]
    [SerializeField] private Collider2D topSurfaceCollider;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float compressDepth = 0.15f;
    [SerializeField] private float compressTime = 0.06f;
    [SerializeField] private float releaseDelay = 0.08f;
    [SerializeField] private float recoverTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool showSweetSpotGizmo = true;

    private Vector3 visualRestLocalPosition;
    private Coroutine bounceRoutine;

    private void Awake()
    {
        ResolveReferences();
        ConfigureCollider();
        visualRestLocalPosition = visualRoot.localPosition;
    }

    private void OnEnable()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRestLocalPosition;
        }
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        if (bounceRoutine != null)
        {
            return;
        }

        PlayerCharacter player = instigator != null ? instigator.GetComponent<PlayerCharacter>() : null;
        Rigidbody2D instigatorBody = instigator != null ? instigator.GetComponent<Rigidbody2D>() : null;

        if (player != null)
        {
            player.SuppressDiveLandingStunThisImpact();
        }

        float offset = GetImpactOffsetFromCenter(impactPoint);
        Vector2 bounceVelocity = CalculateBounceVelocity(offset, player);

        if (player != null)
        {
            Vector2 initialVelocity = new Vector2(0f, bounceVelocity.y);
            player.ApplyLeafDiveBounce(
                initialVelocity,
                bounceVelocity.x,
                horizontalBuildDuration);
        }
        else if (instigatorBody != null)
        {
            instigatorBody.linearVelocity = bounceVelocity;
        }

        bounceRoutine = StartCoroutine(AnimateBounce());
    }

    private IEnumerator AnimateBounce()
    {
        Vector3 compressedPosition = visualRestLocalPosition - transform.InverseTransformDirection(transform.up) * compressDepth;
        yield return MoveVisual(visualRestLocalPosition, compressedPosition, compressTime);

        if (releaseDelay > 0f)
        {
            yield return new WaitForSeconds(releaseDelay);
        }

        yield return MoveVisual(compressedPosition, visualRestLocalPosition, recoverTime);

        bounceRoutine = null;
    }

    private Vector2 CalculateBounceVelocity(float offset, PlayerCharacter player)
    {
        float adjustedOffset = ApplySweetSpot(offset);
        float angleError = adjustedOffset * maxAngleError;
        Vector2 finalDirection = RotateVector(transform.up, angleError).normalized;
        float accuracyLoss = Mathf.Abs(adjustedOffset);
        float accuracySpeedMultiplier = Mathf.Lerp(1f, edgeSpeedMultiplier, accuracyLoss);
        float heightPower = player != null
            ? Mathf.InverseLerp(minEffectiveDiveHeight, maxEffectiveDiveHeight, player.LastDiveFallDistance)
            : 0f;
        float heightBasedSpeed = Mathf.Lerp(minBounceSpeed, maxBounceSpeed, heightPower);
        float finalSpeed = heightBasedSpeed * accuracySpeedMultiplier;
        return new Vector2(
            finalDirection.x * finalSpeed * horizontalBounceMultiplier,
            finalDirection.y * finalSpeed * verticalBounceMultiplier);
    }

    private float ApplySweetSpot(float offset)
    {
        float sweetRadius = sweetSpotWidth;
        float magnitude = Mathf.Abs(offset);
        if (magnitude <= sweetRadius)
        {
            return 0f;
        }

        float remappedMagnitude = Mathf.InverseLerp(sweetRadius, 1f, magnitude);
        return Mathf.Sign(offset) * remappedMagnitude;
    }

    private float GetImpactOffsetFromCenter(Vector2 impactPoint)
    {
        ResolveReferences();

        if (TryGetSurfaceLocalRange(out Transform surfaceTransform, out float centerX, out float halfWidth, out _))
        {
            Vector2 localPoint = surfaceTransform.InverseTransformPoint(impactPoint);
            float normalizedPoint = (localPoint.x - centerX) / Mathf.Max(0.0001f, halfWidth);
            return Mathf.Clamp(normalizedPoint - sweetSpotOffset, -1f, 1f);
        }

        float halfExtent = GetProjectedHalfExtentOnRight();
        float centerProjection = Vector2.Dot(topSurfaceCollider != null ? topSurfaceCollider.bounds.center : transform.position, transform.right);
        float impactProjection = Vector2.Dot(impactPoint, transform.right);
        float normalizedProjection = (impactProjection - centerProjection) / Mathf.Max(0.0001f, halfExtent);
        return Mathf.Clamp(normalizedProjection - sweetSpotOffset, -1f, 1f);
    }

    private float GetProjectedHalfExtentOnRight()
    {
        if (topSurfaceCollider == null)
        {
            return 0.5f;
        }

        Bounds bounds = topSurfaceCollider.bounds;
        Vector2 right = transform.right;
        Vector2 center = bounds.center;
        Vector2 extents = bounds.extents;
        float maxDistance = 0f;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                Vector2 corner = center + new Vector2(extents.x * x, extents.y * y);
                float distance = Mathf.Abs(Vector2.Dot(corner - center, right));
                maxDistance = Mathf.Max(maxDistance, distance);
            }
        }

        return Mathf.Max(0.0001f, maxDistance);
    }

    private static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos);
    }

    private IEnumerator MoveVisual(Vector3 from, Vector3 to, float duration)
    {
        if (visualRoot == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            visualRoot.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            visualRoot.localPosition = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        visualRoot.localPosition = to;
    }

    private void ResolveReferences()
    {
        if (topSurfaceCollider == null)
        {
            Transform topSurface = transform.Find("TopSurface");
            topSurfaceCollider = topSurface != null ? topSurface.GetComponent<Collider2D>() : GetComponent<Collider2D>();
        }

        if (visualRoot == null)
        {
            Transform visual = transform.Find("Visual");
            visualRoot = visual != null ? visual : transform;
        }
    }

    private void ConfigureCollider()
    {
        if (topSurfaceCollider == null)
        {
            return;
        }

        topSurfaceCollider.isTrigger = false;
        topSurfaceCollider.usedByEffector = false;

        PlatformEffector2D effector = topSurfaceCollider.GetComponent<PlatformEffector2D>();
        if (effector != null)
        {
            effector.enabled = false;
        }
    }

    private bool TryGetSurfaceLocalRange(out Transform surfaceTransform, out float centerX, out float halfWidth, out float centerY)
    {
        ResolveReferences();
        surfaceTransform = topSurfaceCollider != null ? topSurfaceCollider.transform : transform;
        centerX = 0f;
        halfWidth = 0.5f;
        centerY = 0f;

        if (topSurfaceCollider is EdgeCollider2D edge && edge.pointCount >= 2)
        {
            Vector2[] points = edge.points;
            float minX = points[0].x;
            float maxX = points[0].x;
            float ySum = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                minX = Mathf.Min(minX, points[i].x);
                maxX = Mathf.Max(maxX, points[i].x);
                ySum += points[i].y;
            }

            centerX = (minX + maxX) * 0.5f;
            halfWidth = Mathf.Max(0.0001f, (maxX - minX) * 0.5f);
            centerY = ySum / points.Length;
            return true;
        }

        if (topSurfaceCollider is BoxCollider2D box)
        {
            centerX = box.offset.x;
            halfWidth = Mathf.Max(0.0001f, box.size.x * 0.5f);
            centerY = box.offset.y;
            return true;
        }

        return topSurfaceCollider != null;
    }

    private void OnValidate()
    {
        maxAngleError = Mathf.Max(0f, maxAngleError);
        edgeSpeedMultiplier = Mathf.Clamp01(edgeSpeedMultiplier);
        sweetSpotWidth = Mathf.Clamp01(sweetSpotWidth);
        sweetSpotOffset = Mathf.Clamp(sweetSpotOffset, -1f, 1f);
        minEffectiveDiveHeight = Mathf.Max(0f, minEffectiveDiveHeight);
        maxEffectiveDiveHeight = Mathf.Max(minEffectiveDiveHeight + 0.01f, maxEffectiveDiveHeight);
        minBounceSpeed = Mathf.Max(0f, minBounceSpeed);
        maxBounceSpeed = Mathf.Max(minBounceSpeed, maxBounceSpeed);
        verticalBounceMultiplier = Mathf.Max(0f, verticalBounceMultiplier);
        horizontalBounceMultiplier = Mathf.Max(0f, horizontalBounceMultiplier);
        horizontalBuildDuration = Mathf.Max(0f, horizontalBuildDuration);
        compressDepth = Mathf.Max(0f, compressDepth);
        compressTime = Mathf.Max(0f, compressTime);
        releaseDelay = Mathf.Max(0f, releaseDelay);
        recoverTime = Mathf.Max(0f, recoverTime);
        ResolveReferences();
        ConfigureCollider();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSweetSpotGizmo)
        {
            return;
        }

        ResolveReferences();
        if (topSurfaceCollider == null)
        {
            return;
        }

        if (topSurfaceCollider is BoxCollider2D box)
        {
            DrawBoxColliderSweetSpotGizmo(box);
            return;
        }

        if (topSurfaceCollider is EdgeCollider2D edge)
        {
            DrawEdgeColliderSweetSpotGizmo(edge);
        }
    }

    private void DrawBoxColliderSweetSpotGizmo(BoxCollider2D box)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = box.transform.localToWorldMatrix;

        Vector3 surfaceCenter = box.offset;
        Vector3 center = surfaceCenter + Vector3.right * (box.size.x * 0.5f * sweetSpotOffset);
        Vector3 fullSize = new Vector3(box.size.x, box.size.y, 0.02f);
        float sweetWidth = box.size.x * Mathf.Clamp01(sweetSpotWidth);
        Vector3 sweetSize = new Vector3(sweetWidth, Mathf.Max(box.size.y * 1.35f, 0.08f), 0.02f);

        Gizmos.color = FullWidthGizmoColor;
        Gizmos.DrawWireCube(surfaceCenter, fullSize);

        Gizmos.color = SweetSpotFillGizmoColor;
        Gizmos.DrawCube(center, sweetSize);

        Gizmos.color = SweetSpotEdgeGizmoColor;
        Gizmos.DrawWireCube(center, sweetSize);

        float launchLineLength = Mathf.Max(box.size.y * 3f, 0.5f);
        Gizmos.color = LaunchDirectionGizmoColor;
        Gizmos.DrawLine(center, center + Vector3.up * launchLineLength);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void DrawEdgeColliderSweetSpotGizmo(EdgeCollider2D edge)
    {
        if (!TryGetSurfaceLocalRange(out Transform surfaceTransform, out float centerX, out float halfWidth, out float centerY))
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = surfaceTransform.localToWorldMatrix;

        float minX = centerX - halfWidth;
        float maxX = centerX + halfWidth;
        Vector3 left = new Vector3(minX, centerY, 0f);
        Vector3 right = new Vector3(maxX, centerY, 0f);
        float sweetHalfWidth = halfWidth * Mathf.Clamp01(sweetSpotWidth);
        float sweetCenterX = centerX + halfWidth * sweetSpotOffset;
        Vector3 sweetLeft = new Vector3(sweetCenterX - sweetHalfWidth, centerY, 0f);
        Vector3 sweetRight = new Vector3(sweetCenterX + sweetHalfWidth, centerY, 0f);
        float gizmoHeight = 0.18f;
        Vector3 center = new Vector3(sweetCenterX, centerY, 0f);

        Gizmos.color = FullWidthGizmoColor;
        Gizmos.DrawLine(left, right);

        Gizmos.color = SweetSpotFillGizmoColor;
        Gizmos.DrawCube(center, new Vector3(sweetHalfWidth * 2f, gizmoHeight, 0.02f));

        Gizmos.color = SweetSpotEdgeGizmoColor;
        Gizmos.DrawLine(sweetLeft, sweetRight);
        Gizmos.DrawWireCube(center, new Vector3(sweetHalfWidth * 2f, gizmoHeight, 0.02f));

        Gizmos.color = LaunchDirectionGizmoColor;
        Gizmos.DrawLine(center, center + Vector3.up * 0.7f);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
