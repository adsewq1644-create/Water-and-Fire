using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LeafBouncePlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    private static readonly Color FullWidthGizmoColor = new Color(0.2f, 1f, 0.35f, 0.7f);
    private static readonly Color SweetSpotFillGizmoColor = new Color(1f, 0.9f, 0.05f, 0.22f);
    private static readonly Color SweetSpotEdgeGizmoColor = new Color(1f, 0.9f, 0.05f, 0.95f);
    private static readonly Color LaunchDirectionGizmoColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    [Header("Leaf Bounce")]
    [SerializeField] private float bounceSpeed = 14f;
    [SerializeField] private float maxAngleError = 18f;
    [SerializeField, Range(0f, 1f)] private float edgeSpeedMultiplier = 0.75f;
    [SerializeField, Range(0f, 1f)] private float sweetSpotWidth = 0.25f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float compressDepth = 0.15f;
    [SerializeField] private float compressTime = 0.06f;
    [SerializeField] private float releaseDelay = 0.08f;
    [SerializeField] private float recoverTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool showSweetSpotGizmo = true;

    private Collider2D leafCollider;
    private Vector3 visualRestLocalPosition;
    private Coroutine bounceRoutine;

    private void Awake()
    {
        ResolveReferences();
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
        Vector2 bounceVelocity = CalculateBounceVelocity(offset);
        bounceRoutine = StartCoroutine(BounceAfterCompression(player, instigatorBody, bounceVelocity));
    }

    private IEnumerator BounceAfterCompression(PlayerCharacter player, Rigidbody2D instigatorBody, Vector2 bounceVelocity)
    {
        Vector3 compressedPosition = visualRestLocalPosition - transform.InverseTransformDirection(transform.up) * compressDepth;
        yield return MoveVisual(visualRestLocalPosition, compressedPosition, compressTime);

        if (releaseDelay > 0f)
        {
            yield return new WaitForSeconds(releaseDelay);
        }

        if (player != null)
        {
            player.ApplyDiveBounce(bounceVelocity);
        }
        else if (instigatorBody != null)
        {
            instigatorBody.linearVelocity = bounceVelocity;
        }

        yield return MoveVisual(compressedPosition, visualRestLocalPosition, recoverTime);

        bounceRoutine = null;
    }

    private Vector2 CalculateBounceVelocity(float offset)
    {
        float adjustedOffset = ApplySweetSpot(offset);
        float angleError = adjustedOffset * maxAngleError;
        Vector2 finalDirection = RotateVector(transform.up, angleError).normalized;
        float errorAmount = Mathf.Abs(adjustedOffset);
        float speedMultiplier = Mathf.Lerp(1f, edgeSpeedMultiplier, errorAmount);
        return finalDirection * bounceSpeed * speedMultiplier;
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

        if (leafCollider is BoxCollider2D box)
        {
            Vector2 localPoint = transform.InverseTransformPoint(impactPoint);
            float halfWidth = Mathf.Max(0.0001f, box.size.x * 0.5f);
            return Mathf.Clamp((localPoint.x - box.offset.x) / halfWidth, -1f, 1f);
        }

        float halfExtent = GetProjectedHalfExtentOnRight();
        float centerProjection = Vector2.Dot(leafCollider != null ? leafCollider.bounds.center : transform.position, transform.right);
        float impactProjection = Vector2.Dot(impactPoint, transform.right);
        return Mathf.Clamp((impactProjection - centerProjection) / Mathf.Max(0.0001f, halfExtent), -1f, 1f);
    }

    private float GetProjectedHalfExtentOnRight()
    {
        if (leafCollider == null)
        {
            return 0.5f;
        }

        Bounds bounds = leafCollider.bounds;
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
        if (leafCollider == null)
        {
            leafCollider = GetComponent<Collider2D>();
        }

        if (visualRoot == null)
        {
            Transform visual = transform.Find("Visual");
            visualRoot = visual != null ? visual : transform;
        }
    }

    private void OnValidate()
    {
        bounceSpeed = Mathf.Max(0f, bounceSpeed);
        maxAngleError = Mathf.Max(0f, maxAngleError);
        edgeSpeedMultiplier = Mathf.Clamp01(edgeSpeedMultiplier);
        sweetSpotWidth = Mathf.Clamp01(sweetSpotWidth);
        compressDepth = Mathf.Max(0f, compressDepth);
        compressTime = Mathf.Max(0f, compressTime);
        releaseDelay = Mathf.Max(0f, releaseDelay);
        recoverTime = Mathf.Max(0f, recoverTime);
        ResolveReferences();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSweetSpotGizmo)
        {
            return;
        }

        ResolveReferences();
        if (leafCollider == null)
        {
            return;
        }

        if (leafCollider is BoxCollider2D box)
        {
            DrawBoxColliderSweetSpotGizmo(box);
        }
    }

    private void DrawBoxColliderSweetSpotGizmo(BoxCollider2D box)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = box.offset;
        Vector3 fullSize = new Vector3(box.size.x, box.size.y, 0.02f);
        float sweetWidth = box.size.x * Mathf.Clamp01(sweetSpotWidth);
        Vector3 sweetSize = new Vector3(sweetWidth, Mathf.Max(box.size.y * 1.35f, 0.08f), 0.02f);

        Gizmos.color = FullWidthGizmoColor;
        Gizmos.DrawWireCube(center, fullSize);

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
}
