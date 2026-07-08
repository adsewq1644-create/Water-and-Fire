using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
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

    [Header("Colliders")]
    [SerializeField] private Collider2D topSurfaceCollider;
    [SerializeField] private Collider2D slideZoneCollider;
    [SerializeField] private bool useOneWayTopSurface = true;
    [SerializeField, Range(1f, 180f)] private float oneWaySurfaceArc = 130f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float compressDepth = 0.15f;
    [SerializeField] private float compressTime = 0.06f;
    [SerializeField] private float releaseDelay = 0.08f;
    [SerializeField] private float recoverTime = 0.12f;

    [Header("Forced Slide")]
    [SerializeField] private bool enableForcedSlide = true;
    [SerializeField] private float forcedSlideAcceleration = 12f;
    [SerializeField] private float maxForcedSlideSpeed = 7f;
    [SerializeField] private float minSlideAngle = 3f;
    [SerializeField] private bool allowResistSlide;
    [SerializeField] private float slideSurfaceContactHeight = 0.22f;
    [SerializeField] private float slideSurfaceBottomTolerance = 0.08f;

    [Header("Edge Exit Assist")]
    [SerializeField] private bool enableEdgeExitAssist;
    [SerializeField] private float edgeExitAssistDistance = 0.08f;
    [SerializeField] private float edgeExitNudgeDistance = 0.01f;
    [SerializeField] private float edgeExitSpeedBoost = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showSweetSpotGizmo = true;

    private Vector3 visualRestLocalPosition;
    private Coroutine bounceRoutine;
    private PlayerCharacter pendingBouncePlayer;
    private static PhysicsMaterial2D noFrictionMaterial;
    private readonly Dictionary<PlayerCharacter, SlideState> slidingPlayers = new Dictionary<PlayerCharacter, SlideState>();
    private readonly List<PlayerCharacter> slideCleanup = new List<PlayerCharacter>();

    private sealed class SlideState
    {
        public Rigidbody2D Body;
        public float Speed;
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureColliders(assignRuntimeMaterial: true);
        visualRestLocalPosition = visualRoot.localPosition;
    }

    private void OnEnable()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRestLocalPosition;
        }
    }

    private void OnDisable()
    {
        slidingPlayers.Clear();
        pendingBouncePlayer = null;
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
            pendingBouncePlayer = player;
            slidingPlayers.Remove(player);
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

        if (pendingBouncePlayer == player)
        {
            pendingBouncePlayer = null;
        }

        bounceRoutine = null;
    }

    private void FixedUpdate()
    {
        if (!enableForcedSlide || slidingPlayers.Count == 0)
        {
            return;
        }

        if (!TryGetSlideDirection(out Vector2 slideDirection))
        {
            return;
        }

        slideCleanup.Clear();
        foreach (var entry in slidingPlayers)
        {
            PlayerCharacter player = entry.Key;
            SlideState state = entry.Value;
            if (player == null || state == null || state.Body == null || ShouldSkipForcedSlide(player) || !IsPlayerTouchingSlideSurface(player))
            {
                slideCleanup.Add(player);
                continue;
            }

            ApplyForcedSlide(player, state, slideDirection);
        }

        for (int i = 0; i < slideCleanup.Count; i++)
        {
            slidingPlayers.Remove(slideCleanup[i]);
        }
    }

    public void NotifySlideZoneStay(Collider2D other)
    {
        if (!enableForcedSlide)
        {
            return;
        }

        PlayerCharacter player = other != null ? other.GetComponentInParent<PlayerCharacter>() : null;
        if (player == null || ShouldSkipForcedSlide(player))
        {
            if (player != null)
            {
                slidingPlayers.Remove(player);
            }
            return;
        }

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null)
        {
            body = player.GetComponent<Rigidbody2D>();
        }

        if (body == null)
        {
            return;
        }

        if (!IsPlayerTouchingSlideSurface(player))
        {
            slidingPlayers.Remove(player);
            return;
        }

        if (!slidingPlayers.TryGetValue(player, out SlideState state))
        {
            state = new SlideState();
            slidingPlayers.Add(player, state);
        }

        state.Body = body;
        if (TryGetSlideDirection(out Vector2 slideDirection))
        {
            state.Speed = Mathf.Max(state.Speed, Vector2.Dot(body.linearVelocity, slideDirection));
        }
    }

    public void NotifySlideZoneExit(Collider2D other)
    {
        PlayerCharacter player = other != null ? other.GetComponentInParent<PlayerCharacter>() : null;
        if (player != null)
        {
            slidingPlayers.Remove(player);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!enableForcedSlide || slideZoneCollider != null)
        {
            return;
        }

        PlayerCharacter player = collision.collider.GetComponentInParent<PlayerCharacter>();
        if (player == null || ShouldSkipForcedSlide(player))
        {
            if (player != null)
            {
                slidingPlayers.Remove(player);
            }
            return;
        }

        if (!IsStandingOnLeaf(collision, player))
        {
            return;
        }

        Rigidbody2D body = collision.rigidbody != null ? collision.rigidbody : collision.collider.attachedRigidbody;
        if (body == null)
        {
            body = player.GetComponent<Rigidbody2D>();
        }

        if (body == null)
        {
            return;
        }

        if (!slidingPlayers.TryGetValue(player, out SlideState state))
        {
            state = new SlideState();
            slidingPlayers.Add(player, state);
        }

        state.Body = body;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (slideZoneCollider != null)
        {
            return;
        }

        PlayerCharacter player = collision.collider.GetComponentInParent<PlayerCharacter>();
        if (player != null)
        {
            slidingPlayers.Remove(player);
        }
    }

    private bool ShouldSkipForcedSlide(PlayerCharacter player)
    {
        return player.IsDiving || player.IsDiveBounceGroundIgnored || player == pendingBouncePlayer;
    }

    private bool IsPlayerTouchingSlideSurface(PlayerCharacter player)
    {
        if (player == null || player.BodyCollider == null)
        {
            return false;
        }

        if (!TryGetSurfaceLocalRange(out Transform surfaceTransform, out float centerX, out float halfWidth, out float centerY))
        {
            return true;
        }

        Bounds bounds = player.BodyCollider.bounds;
        Vector3[] footSamples =
        {
            new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.center.z)
        };

        float minLocalX = float.PositiveInfinity;
        float maxLocalX = float.NegativeInfinity;
        bool hasFootNearSurface = false;
        for (int i = 0; i < footSamples.Length; i++)
        {
            Vector3 localFoot = surfaceTransform.InverseTransformPoint(footSamples[i]);
            minLocalX = Mathf.Min(minLocalX, localFoot.x);
            maxLocalX = Mathf.Max(maxLocalX, localFoot.x);

            float footDistanceFromSurface = localFoot.y - centerY;
            if (footDistanceFromSurface >= -slideSurfaceBottomTolerance && footDistanceFromSurface <= slideSurfaceContactHeight)
            {
                hasFootNearSurface = true;
            }
        }

        float overlapPadding = Mathf.Max(0.04f, slideSurfaceBottomTolerance);
        bool overlapsSurfaceWidth = maxLocalX >= centerX - halfWidth - overlapPadding && minLocalX <= centerX + halfWidth + overlapPadding;
        if (!overlapsSurfaceWidth)
        {
            return false;
        }

        return hasFootNearSurface;
    }

    private void ApplyForcedSlide(PlayerCharacter player, SlideState state, Vector2 slideDirection)
    {
        Vector2 velocity = state.Body.linearVelocity;
        float targetSpeed = Mathf.Max(0f, maxForcedSlideSpeed);
        float accelerationStep = Mathf.Max(0f, forcedSlideAcceleration) * Time.fixedDeltaTime;
        float currentAlongSlide = Vector2.Dot(velocity, slideDirection);
        Vector2 perpendicularVelocity = velocity - slideDirection * currentAlongSlide;

        if (allowResistSlide)
        {
            float nextAlongSlide = Mathf.MoveTowards(currentAlongSlide, targetSpeed, accelerationStep);
            velocity = perpendicularVelocity + slideDirection * nextAlongSlide;
        }
        else
        {
            state.Speed = Mathf.MoveTowards(Mathf.Max(state.Speed, currentAlongSlide), targetSpeed, accelerationStep);
            velocity = perpendicularVelocity + slideDirection * state.Speed;
        }

        state.Body.linearVelocity = velocity;
        ApplyEdgeExitAssist(player, state, slideDirection, targetSpeed);
    }

    private void ApplyEdgeExitAssist(PlayerCharacter player, SlideState state, Vector2 slideDirection, float targetSpeed)
    {
        if (!enableEdgeExitAssist || player.BodyCollider == null || topSurfaceCollider is not BoxCollider2D box)
        {
            return;
        }

        Vector2 leafRight = transform.right;
        float slideSign = Mathf.Sign(Vector2.Dot(slideDirection, leafRight));
        if (Mathf.Approximately(slideSign, 0f))
        {
            return;
        }

        Vector2 leafCenter = transform.TransformPoint(box.offset);
        Bounds playerBounds = player.BodyCollider.bounds;
        float leafHalfWidth = Mathf.Abs(box.size.x * transform.lossyScale.x) * 0.5f;
        float playerHalfWidth = GetProjectedHalfExtent(playerBounds, leafRight);
        float playerCenterOffset = Vector2.Dot((Vector2)playerBounds.center - leafCenter, leafRight);
        float playerFront = slideSign * playerCenterOffset + playerHalfWidth;
        float distanceToExitEdge = leafHalfWidth - playerFront;

        if (distanceToExitEdge > edgeExitAssistDistance)
        {
            return;
        }

        float nudge = Mathf.Clamp(edgeExitAssistDistance - distanceToExitEdge, 0f, edgeExitNudgeDistance);
        if (nudge > 0f)
        {
            state.Body.position += slideDirection.normalized * nudge;
        }

        Vector2 velocity = state.Body.linearVelocity;
        float currentAlongSlide = Vector2.Dot(velocity, slideDirection);
        float boostedSpeed = Mathf.Max(targetSpeed + edgeExitSpeedBoost, currentAlongSlide);
        Vector2 perpendicularVelocity = velocity - slideDirection * currentAlongSlide;
        state.Body.linearVelocity = perpendicularVelocity + slideDirection * boostedSpeed;
        state.Speed = Mathf.Max(state.Speed, boostedSpeed);
    }

    private static float GetProjectedHalfExtent(Bounds bounds, Vector2 axis)
    {
        axis.Normalize();
        return Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.y) * bounds.extents.y;
    }

    private bool IsStandingOnLeaf(Collision2D collision, PlayerCharacter player)
    {
        if (player.BodyCollider == null)
        {
            return false;
        }

        ResolveReferences();
        if (topSurfaceCollider is BoxCollider2D box)
        {
            float topContactThreshold = box.offset.y - box.size.y * 0.35f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 localPoint = transform.InverseTransformPoint(collision.GetContact(i).point);
                if (localPoint.y >= topContactThreshold)
                {
                    return true;
                }
            }

            Vector3 playerCenterLocal = transform.InverseTransformPoint(player.BodyCollider.bounds.center);
            return playerCenterLocal.y >= box.offset.y;
        }

        return Vector2.Dot(player.BodyCollider.bounds.center - transform.position, transform.up) >= 0f;
    }

    private bool TryGetSlideDirection(out Vector2 slideDirection)
    {
        Vector2 leafRight = transform.right;
        float angleFromHorizontal = Mathf.Abs(Mathf.Asin(Mathf.Clamp(leafRight.y, -1f, 1f)) * Mathf.Rad2Deg);
        if (angleFromHorizontal < minSlideAngle || Mathf.Approximately(leafRight.y, 0f))
        {
            slideDirection = Vector2.zero;
            return false;
        }

        slideDirection = leafRight * -Mathf.Sign(leafRight.y);
        slideDirection.Normalize();
        return true;
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

        if (TryGetSurfaceLocalRange(out Transform surfaceTransform, out float centerX, out float halfWidth, out _))
        {
            Vector2 localPoint = surfaceTransform.InverseTransformPoint(impactPoint);
            return Mathf.Clamp((localPoint.x - centerX) / Mathf.Max(0.0001f, halfWidth), -1f, 1f);
        }

        float halfExtent = GetProjectedHalfExtentOnRight();
        float centerProjection = Vector2.Dot(topSurfaceCollider != null ? topSurfaceCollider.bounds.center : transform.position, transform.right);
        float impactProjection = Vector2.Dot(impactPoint, transform.right);
        return Mathf.Clamp((impactProjection - centerProjection) / Mathf.Max(0.0001f, halfExtent), -1f, 1f);
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

        if (slideZoneCollider == null)
        {
            Transform slideZone = transform.Find("SlideZone");
            slideZoneCollider = slideZone != null ? slideZone.GetComponent<Collider2D>() : null;
        }

        if (visualRoot == null)
        {
            Transform visual = transform.Find("Visual");
            visualRoot = visual != null ? visual : transform;
        }
    }

    private void ConfigureColliders(bool assignRuntimeMaterial)
    {
        if (slideZoneCollider != null)
        {
            slideZoneCollider.isTrigger = true;
        }

        if (topSurfaceCollider != null)
        {
            topSurfaceCollider.isTrigger = false;
            if (assignRuntimeMaterial)
            {
                topSurfaceCollider.sharedMaterial = GetNoFrictionMaterial();
                ConfigureTopSurfaceEffector(createIfMissing: true);
            }
            else
            {
                ConfigureTopSurfaceEffector(createIfMissing: false);
            }
        }
    }

    private void ConfigureTopSurfaceEffector(bool createIfMissing)
    {
        if (topSurfaceCollider == null)
        {
            return;
        }

        PlatformEffector2D effector = topSurfaceCollider.GetComponent<PlatformEffector2D>();
        if (effector == null && createIfMissing && useOneWayTopSurface)
        {
            effector = topSurfaceCollider.gameObject.AddComponent<PlatformEffector2D>();
        }

        if (effector == null)
        {
            topSurfaceCollider.usedByEffector = false;
            return;
        }

        topSurfaceCollider.usedByEffector = useOneWayTopSurface;
        effector.useOneWay = useOneWayTopSurface;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = oneWaySurfaceArc;
        effector.rotationalOffset = 0f;
        effector.useSideFriction = false;
        effector.useSideBounce = false;
    }

    private static PhysicsMaterial2D GetNoFrictionMaterial()
    {
        if (noFrictionMaterial != null)
        {
            return noFrictionMaterial;
        }

        noFrictionMaterial = new PhysicsMaterial2D("LeafBounce_NoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };
        noFrictionMaterial.hideFlags = HideFlags.HideAndDontSave;
        return noFrictionMaterial;
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
        bounceSpeed = Mathf.Max(0f, bounceSpeed);
        maxAngleError = Mathf.Max(0f, maxAngleError);
        edgeSpeedMultiplier = Mathf.Clamp01(edgeSpeedMultiplier);
        sweetSpotWidth = Mathf.Clamp01(sweetSpotWidth);
        oneWaySurfaceArc = Mathf.Clamp(oneWaySurfaceArc, 1f, 180f);
        compressDepth = Mathf.Max(0f, compressDepth);
        compressTime = Mathf.Max(0f, compressTime);
        releaseDelay = Mathf.Max(0f, releaseDelay);
        recoverTime = Mathf.Max(0f, recoverTime);
        forcedSlideAcceleration = Mathf.Max(0f, forcedSlideAcceleration);
        maxForcedSlideSpeed = Mathf.Max(0f, maxForcedSlideSpeed);
        minSlideAngle = Mathf.Max(0f, minSlideAngle);
        slideSurfaceContactHeight = Mathf.Max(0f, slideSurfaceContactHeight);
        slideSurfaceBottomTolerance = Mathf.Max(0f, slideSurfaceBottomTolerance);
        edgeExitAssistDistance = Mathf.Max(0f, edgeExitAssistDistance);
        edgeExitNudgeDistance = Mathf.Max(0f, edgeExitNudgeDistance);
        edgeExitSpeedBoost = Mathf.Max(0f, edgeExitSpeedBoost);
        ResolveReferences();
        ConfigureColliders(assignRuntimeMaterial: false);
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
        Vector3 sweetLeft = new Vector3(centerX - sweetHalfWidth, centerY, 0f);
        Vector3 sweetRight = new Vector3(centerX + sweetHalfWidth, centerY, 0f);
        float gizmoHeight = 0.18f;
        Vector3 center = new Vector3(centerX, centerY, 0f);

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
