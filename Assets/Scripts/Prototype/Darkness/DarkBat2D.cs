using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class DarkBat2D : MonoBehaviour, IShockwaveContextReceiver
{
    public enum BatState
    {
        Dormant,
        Chasing,
        Recovering,
        Returning,
        Dead
    }

    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private Collider2D hitCollider;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private AudioSource wingAudio;
    [SerializeField] private ParticleSystem deathParticles;

    [Header("Movement")]
    [SerializeField] private float chaseSpeed = 7f;
    [SerializeField] private float returnSpeed = 5f;
    [SerializeField] private float attackDistance = 0.3f;
    [SerializeField] private float chaseLeashRadius = 6f;
    [SerializeField] private float maxChaseDuration = 2.5f;
    [SerializeField] private float returnCompleteDistance = 0.1f;
    [SerializeField] private float postAttackDelay = 0.3f;

    [Header("Curled Flight")]
    [SerializeField] private bool useCurledFlight = true;
    [SerializeField] private float curlRadius = 0.45f;
    [SerializeField] private float curlAngularSpeed = 7f;
    [SerializeField] private float guideAdvanceSpeed = 4f;
    [SerializeField] private float maxFlightSpeed = 8f;
    [SerializeField] private float curlStartDistance = 0.8f;
    [SerializeField] private float fullCurlDistance = 2.5f;
    [SerializeField] private float targetApproachRadiusMultiplier = 0.15f;
    [SerializeField] private float obstacleCurlMultiplier = 0.25f;
    [SerializeField] private float returnCurlMultiplier = 0.6f;
    [SerializeField] private float directionSmoothTime = 0.12f;
    [SerializeField] private bool clockwiseCurl = true;
    [SerializeField] private bool alternateInitialCurlDirection = true;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float obstacleProbeRadius = 0.25f;
    [SerializeField] private float obstacleLookAheadDistance = 1.5f;
    [SerializeField] private float obstacleSkin = 0.05f;
    [SerializeField] private float avoidanceTurnSpeed = 8f;
    [SerializeField] private float avoidanceDirectionLockDuration = 0.2f;
    [SerializeField] private float wallSlideMultiplier = 0.8f;
    [SerializeField] private float stuckCheckDuration = 0.6f;
    [SerializeField] private float stuckMovementThreshold = 0.08f;
    [SerializeField] private bool avoidTriggers = true;
    [SerializeField] private bool showAvoidanceGizmos = true;

    [Header("Knockback")]
    [SerializeField] private float horizontalKnockback = 8f;
    [SerializeField] private float verticalKnockback = 4f;
    [SerializeField] private float inputLockDuration = 0.15f;
    [SerializeField] private float contactHitCooldown = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private static readonly float[] AvoidanceAngles =
    {
        0f, 20f, -20f, 40f, -40f, 60f, -60f, 85f, -85f, 120f, -120f
    };

    private static readonly float[] StuckAvoidanceAngles =
    {
        60f, -60f, 85f, -85f, 120f, -120f, 150f, -150f, 180f
    };

    private readonly Dictionary<PlayerCharacter, float> nextAllowedHitTime = new Dictionary<PlayerCharacter, float>();
    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[24];
    private readonly Collider2D[] obstacleOverlaps = new Collider2D[16];
    private readonly Vector2[] debugCandidateDirections = new Vector2[16];
    private readonly float[] debugCandidateClearances = new float[16];
    private Vector2 homePosition;
    private Vector2 activationOrigin;
    private Vector2 movementDirection;
    private Vector2 guidePosition;
    private Vector2 guideForward = Vector2.right;
    private Vector2 orbitOffset;
    private Vector2 lockedAvoidanceDirection;
    private Vector2 selectedAvoidanceDirection;
    private Vector2 lastObstacleNormal;
    private Vector2 lastObservedPosition;
    private PlayerCharacter attackTarget;
    private float chaseElapsed;
    private float recoveryElapsed;
    private float curlPhase;
    private float currentCurlRadius;
    private float avoidanceLockRemaining;
    private float stuckCheckElapsed;
    private float stuckDistanceTravelled;
    private float stuckEscapeRemaining;
    private int curlDirectionSign = 1;
    private int debugCandidateCount;
    private bool flightInitialized;
    private bool avoidingObstacle;
    private bool stuckDetected;
    private bool homeInsideObstacle;
    private ContactFilter2D obstacleFilter;
    private BatState state = BatState.Dormant;

    public BatState State => state;
    public PlayerCharacter AttackTarget => attackTarget;
    public Vector2 ActivationOrigin => activationOrigin;

    private void Reset()
    {
        ResolveReferences();
        ConfigurePhysics();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigurePhysics();
        homePosition = body != null ? body.position : (Vector2)transform.position;
        RebuildObstacleFilter();
        InitializeFlight(homePosition, Vector2.right, true);
        CheckHomePosition();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (!flightInitialized && body != null)
        {
            InitializeFlight(body.position, Vector2.right, true);
        }
    }

    private void OnDisable()
    {
        nextAllowedHitTime.Clear();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (wingAudio != null)
        {
            wingAudio.Stop();
        }
    }

    private void Update()
    {
        if (state != BatState.Dead)
        {
            SetVisible(IsInsideActiveFireLight());
        }
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            case BatState.Chasing:
                UpdateChasing();
                break;
            case BatState.Recovering:
                UpdateRecovering();
                break;
            case BatState.Returning:
                UpdateReturning();
                break;
        }
    }

    public void OnShockwaveReceived(ShockwaveContext context)
    {
        if (state == BatState.Dead || context.Instigator == null ||
            context.Instigator.LifeState != PlayerLifeState.Alive)
        {
            return;
        }

        attackTarget = context.Instigator;
        activationOrigin = context.Origin;
        chaseElapsed = 0f;
        recoveryElapsed = 0f;
        PrepareFlightForDestination(attackTarget.transform.position);
        state = BatState.Chasing;
    }

    public void HitByProjectile(ElementType elementType)
    {
        if (state == BatState.Dead || (elementType != ElementType.Fire && elementType != ElementType.Water))
        {
            return;
        }

        state = BatState.Dead;
        attackTarget = null;
        movementDirection = Vector2.zero;
        orbitOffset = Vector2.zero;
        currentCurlRadius = 0f;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }

        SetVisible(false);
        if (wingAudio != null)
        {
            wingAudio.Stop();
        }

        if (deathParticles != null)
        {
            deathParticles.Play();
            Destroy(gameObject, Mathf.Max(0.1f, deathParticles.main.duration));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdateChasing()
    {
        chaseElapsed += Time.fixedDeltaTime;
        if (!IsValidTarget(attackTarget) || chaseElapsed >= maxChaseDuration ||
            Vector2.Distance(attackTarget.transform.position, activationOrigin) > chaseLeashRadius)
        {
            BeginReturn();
            return;
        }

        UpdateFlight(
            attackTarget.transform.position,
            useCurledFlight ? guideAdvanceSpeed : chaseSpeed,
            chaseSpeed,
            1f);
        if (Vector2.Distance(body.position, attackTarget.transform.position) <= attackDistance)
        {
            TryHitPlayer(attackTarget);
        }
    }

    private void UpdateRecovering()
    {
        recoveryElapsed += Time.fixedDeltaTime;
        if (recoveryElapsed >= postAttackDelay)
        {
            BeginReturn();
        }
    }

    private void UpdateReturning()
    {
        if (Vector2.Distance(body.position, homePosition) <= returnCompleteDistance)
        {
            Vector2 safeHomePosition = ResolveSafePosition(body.position, homePosition, false);
            if (Vector2.Distance(safeHomePosition, homePosition) <= 0.001f)
            {
                body.MovePosition(homePosition);
                movementDirection = Vector2.zero;
                attackTarget = null;
                InitializeFlight(homePosition, guideForward, true);
                state = BatState.Dormant;
                return;
            }
        }

        UpdateFlight(homePosition, returnSpeed, returnSpeed, returnCurlMultiplier);
    }

    private void UpdateFlight(Vector2 destination, float guideSpeed, float movementSpeed, float curlMultiplier)
    {
        Vector2 current = body.position;
        PrepareFlightForDestination(destination);
        UpdateStuckState(current, destination);

        Vector2 desiredDirection = destination - guidePosition;
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            desiredDirection = destination - current;
        }

        desiredDirection = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : guideForward;

        bool obstacleNearby;
        Vector2 steeringDirection = ResolveSteeringDirection(current, desiredDirection, out obstacleNearby);
        float steeringSharpness = avoidingObstacle || stuckDetected
            ? avoidanceTurnSpeed
            : 1f / Mathf.Max(0.01f, directionSmoothTime);
        guideForward = SmoothDirection(guideForward, steeringDirection, steeringSharpness, Time.fixedDeltaTime);

        float safeGuideSpeed = Mathf.Max(0f, guideSpeed);
        Vector2 proposedGuide = guidePosition + guideForward * safeGuideSpeed * Time.fixedDeltaTime;
        guidePosition = ResolveSafePosition(guidePosition, proposedGuide, false);

        float distanceToDestination = Vector2.Distance(current, destination);
        float distanceFactor = Mathf.InverseLerp(curlStartDistance, fullCurlDistance, distanceToDestination);
        distanceFactor = Mathf.Lerp(targetApproachRadiusMultiplier, 1f, distanceFactor);
        float obstacleFactor = obstacleNearby ? obstacleCurlMultiplier : 1f;
        float stuckFactor = stuckDetected ? 0.05f : 1f;
        float targetRadius = useCurledFlight
            ? curlRadius * Mathf.Max(0f, curlMultiplier) * distanceFactor * obstacleFactor * stuckFactor
            : 0f;
        float radiusBlend = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.01f, directionSmoothTime));
        currentCurlRadius = Mathf.Lerp(currentCurlRadius, targetRadius, radiusBlend);

        curlPhase += curlDirectionSign * curlAngularSpeed * Time.fixedDeltaTime;
        Vector2 guideRight = new Vector2(-guideForward.y, guideForward.x);
        orbitOffset = guideRight * (Mathf.Sin(curlPhase) * currentCurlRadius)
            - guideForward * (Mathf.Cos(curlPhase) * currentCurlRadius);

        Vector2 desiredPosition = guidePosition + orbitOffset;
        float safeMovementSpeed = Mathf.Min(Mathf.Max(0f, movementSpeed), Mathf.Max(0f, maxFlightSpeed));
        Vector2 proposedPosition = Vector2.MoveTowards(
            current,
            desiredPosition,
            safeMovementSpeed * Time.fixedDeltaTime);
        Vector2 safePosition = ResolveSafePosition(current, proposedPosition, true);
        Vector2 actualMove = safePosition - current;
        movementDirection = actualMove.sqrMagnitude > 0.000001f ? actualMove.normalized : guideForward;
        body.MovePosition(safePosition);

        float maxGuideLead = Mathf.Max(obstacleLookAheadDistance, currentCurlRadius + obstacleProbeRadius);
        Vector2 guideLead = guidePosition - safePosition;
        if (guideLead.sqrMagnitude > maxGuideLead * maxGuideLead)
        {
            guidePosition = safePosition + guideLead.normalized * maxGuideLead;
        }
    }

    private void PrepareFlightForDestination(Vector2 destination)
    {
        Vector2 current = body != null ? body.position : (Vector2)transform.position;
        if (!flightInitialized)
        {
            InitializeFlight(current, destination - current, false);
            return;
        }

        float maxGuideSeparation = Mathf.Max(1f, obstacleLookAheadDistance + curlRadius * 2f);
        if ((guidePosition - current).sqrMagnitude > maxGuideSeparation * maxGuideSeparation)
        {
            guidePosition = current;
        }
    }

    private void InitializeFlight(Vector2 position, Vector2 initialDirection, bool resetPhase)
    {
        guidePosition = position;
        guideForward = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right;
        movementDirection = Vector2.zero;
        orbitOffset = Vector2.zero;
        currentCurlRadius = 0f;
        lockedAvoidanceDirection = Vector2.zero;
        selectedAvoidanceDirection = Vector2.zero;
        lastObstacleNormal = Vector2.zero;
        avoidanceLockRemaining = 0f;
        stuckCheckElapsed = 0f;
        stuckDistanceTravelled = 0f;
        stuckEscapeRemaining = 0f;
        stuckDetected = false;
        avoidingObstacle = false;
        lastObservedPosition = position;

        if (resetPhase)
        {
            int instanceSeed = Mathf.Abs(GetInstanceID());
            curlPhase = alternateInitialCurlDirection ? (instanceSeed % 360) * Mathf.Deg2Rad : 0f;
            int baseDirection = clockwiseCurl ? -1 : 1;
            curlDirectionSign = alternateInitialCurlDirection && (instanceSeed & 1) == 1
                ? -baseDirection
                : baseDirection;
        }

        flightInitialized = true;
    }

    private Vector2 ResolveSteeringDirection(Vector2 origin, Vector2 desiredDirection, out bool obstacleNearby)
    {
        avoidanceLockRemaining = Mathf.Max(0f, avoidanceLockRemaining - Time.fixedDeltaTime);
        stuckEscapeRemaining = Mathf.Max(0f, stuckEscapeRemaining - Time.fixedDeltaTime);
        float straightClearance = GetObstacleClearance(origin, desiredDirection, obstacleLookAheadDistance, out _);
        obstacleNearby = straightClearance < obstacleLookAheadDistance - obstacleSkin;

        if (!obstacleNearby && stuckEscapeRemaining <= 0f)
        {
            avoidingObstacle = false;
            selectedAvoidanceDirection = desiredDirection;
            debugCandidateCount = 0;
            return desiredDirection;
        }

        if (avoidanceLockRemaining > 0f && lockedAvoidanceDirection.sqrMagnitude > 0.0001f)
        {
            float lockedClearance = GetObstacleClearance(
                origin,
                lockedAvoidanceDirection,
                obstacleLookAheadDistance,
                out RaycastHit2D lockedHit);
            if (lockedClearance > obstacleSkin)
            {
                avoidingObstacle = true;
                selectedAvoidanceDirection = lockedAvoidanceDirection;
                if (lockedHit.collider != null)
                {
                    lastObstacleNormal = lockedHit.normal;
                }

                return lockedAvoidanceDirection;
            }
        }

        float[] candidateAngles = stuckEscapeRemaining > 0f ? StuckAvoidanceAngles : AvoidanceAngles;
        Vector2 bestDirection = desiredDirection;
        float bestScore = float.NegativeInfinity;
        RaycastHit2D bestHit = default;
        debugCandidateCount = Mathf.Min(candidateAngles.Length, debugCandidateDirections.Length);

        for (int i = 0; i < candidateAngles.Length; i++)
        {
            Vector2 candidate = Rotate(desiredDirection, candidateAngles[i]);
            float clearance = GetObstacleClearance(origin, candidate, obstacleLookAheadDistance, out RaycastHit2D hit);
            float clearanceScore = clearance / Mathf.Max(0.01f, obstacleLookAheadDistance);
            float targetAlignment = Vector2.Dot(candidate, desiredDirection);
            float motionContinuity = Vector2.Dot(candidate, guideForward);
            float avoidanceContinuity = lockedAvoidanceDirection.sqrMagnitude > 0.0001f
                ? Vector2.Dot(candidate, lockedAvoidanceDirection)
                : 0f;
            float wallSeparation = hit.collider != null ? Mathf.Max(0f, Vector2.Dot(candidate, hit.normal)) : 1f;
            float score = clearanceScore * 4f
                + targetAlignment * 2f
                + motionContinuity * 1.25f
                + avoidanceContinuity * 0.75f
                + wallSeparation * 0.5f;

            if (i < debugCandidateCount)
            {
                debugCandidateDirections[i] = candidate;
                debugCandidateClearances[i] = clearance;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
                bestHit = hit;
            }
        }

        avoidingObstacle = true;
        selectedAvoidanceDirection = bestDirection;
        lockedAvoidanceDirection = bestDirection;
        avoidanceLockRemaining = avoidanceDirectionLockDuration;
        if (bestHit.collider != null)
        {
            lastObstacleNormal = bestHit.normal;
        }

        return bestDirection;
    }

    private Vector2 ResolveSafePosition(Vector2 origin, Vector2 proposedPosition, bool allowWallSlide)
    {
        Vector2 displacement = proposedPosition - origin;
        float distance = displacement.magnitude;
        if (distance <= 0.00001f)
        {
            return origin;
        }

        Vector2 direction = displacement / distance;
        if (!TryGetClosestObstacleHit(origin, direction, distance + obstacleSkin, out RaycastHit2D hit))
        {
            return proposedPosition;
        }

        lastObstacleNormal = hit.normal;
        float allowedDistance = Mathf.Clamp(hit.distance - obstacleSkin, 0f, distance);
        Vector2 blockedPosition = origin + direction * allowedDistance;
        if (!allowWallSlide || wallSlideMultiplier <= 0f)
        {
            return blockedPosition;
        }

        Vector2 remaining = displacement - direction * allowedDistance;
        Vector2 tangent = remaining - Vector2.Dot(remaining, hit.normal) * hit.normal;
        tangent *= Mathf.Clamp01(wallSlideMultiplier);
        float tangentDistance = tangent.magnitude;
        if (tangentDistance <= 0.00001f)
        {
            return blockedPosition;
        }

        Vector2 tangentDirection = tangent / tangentDistance;
        if (TryGetClosestObstacleHit(
                blockedPosition,
                tangentDirection,
                tangentDistance + obstacleSkin,
                out RaycastHit2D slideHit))
        {
            tangentDistance = Mathf.Clamp(slideHit.distance - obstacleSkin, 0f, tangentDistance);
            lastObstacleNormal = slideHit.normal;
        }

        return blockedPosition + tangentDirection * tangentDistance;
    }

    private float GetObstacleClearance(
        Vector2 origin,
        Vector2 direction,
        float distance,
        out RaycastHit2D closestHit)
    {
        if (TryGetClosestObstacleHit(origin, direction, distance, out closestHit))
        {
            return Mathf.Clamp(closestHit.distance - obstacleSkin, 0f, distance);
        }

        return distance;
    }

    private bool TryGetClosestObstacleHit(
        Vector2 origin,
        Vector2 direction,
        float distance,
        out RaycastHit2D closestHit)
    {
        closestHit = default;
        if (direction.sqrMagnitude <= 0.0001f || distance <= 0f || obstacleMask.value == 0)
        {
            return false;
        }

        float radius = GetEffectiveProbeRadius();
        int hitCount = Physics2D.CircleCast(
            origin,
            radius,
            direction.normalized,
            obstacleFilter,
            obstacleHits,
            distance);
        float closestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = obstacleHits[i];
            if (!IsValidObstacle(hit.collider) || hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            closestHit = hit;
            found = true;
        }

        return found;
    }

    private bool IsValidObstacle(Collider2D candidate)
    {
        if (candidate == null || candidate == hitCollider || candidate.attachedRigidbody == body)
        {
            return false;
        }

        if (avoidTriggers && candidate.isTrigger)
        {
            return false;
        }

        if (candidate.GetComponentInParent<PlayerCharacter>() != null ||
            candidate.GetComponentInParent<DarkBat2D>() != null ||
            candidate.GetComponentInParent<ElementProjectile>() != null)
        {
            return false;
        }

        return true;
    }

    private float GetEffectiveProbeRadius()
    {
        float radius = Mathf.Max(0.01f, obstacleProbeRadius);
        if (hitCollider is CircleCollider2D circle)
        {
            Vector3 scale = circle.transform.lossyScale;
            float colliderRadius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            radius = Mathf.Max(radius, colliderRadius);
        }

        return radius;
    }

    private void UpdateStuckState(Vector2 currentPosition, Vector2 destination)
    {
        stuckDistanceTravelled += Vector2.Distance(currentPosition, lastObservedPosition);
        lastObservedPosition = currentPosition;
        stuckCheckElapsed += Time.fixedDeltaTime;
        if (stuckCheckElapsed < stuckCheckDuration)
        {
            return;
        }

        float destinationThreshold = Mathf.Max(attackDistance * 2f, returnCompleteDistance * 2f);
        bool farFromDestination = Vector2.Distance(currentPosition, destination) > destinationThreshold;
        stuckDetected = farFromDestination && stuckDistanceTravelled < stuckMovementThreshold;
        if (stuckDetected)
        {
            stuckEscapeRemaining = Mathf.Max(0.35f, avoidanceDirectionLockDuration * 2f);
            avoidanceLockRemaining = 0f;
            lockedAvoidanceDirection = Vector2.zero;
        }

        stuckCheckElapsed = 0f;
        stuckDistanceTravelled = 0f;
    }

    private static Vector2 SmoothDirection(
        Vector2 current,
        Vector2 target,
        float sharpness,
        float deltaTime)
    {
        if (target.sqrMagnitude <= 0.0001f)
        {
            return current.sqrMagnitude > 0.0001f ? current.normalized : Vector2.right;
        }

        if (current.sqrMagnitude <= 0.0001f)
        {
            return target.normalized;
        }

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * deltaTime);
        Vector2 result = Vector2.Lerp(current.normalized, target.normalized, blend);
        return result.sqrMagnitude > 0.0001f ? result.normalized : target.normalized;
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    private void BeginReturn()
    {
        attackTarget = null;
        PrepareFlightForDestination(homePosition);
        state = BatState.Returning;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private void HandlePlayerContact(Collider2D other)
    {
        if (state != BatState.Chasing || other == null)
        {
            return;
        }

        TryHitPlayer(other.GetComponentInParent<PlayerCharacter>());
    }

    private void TryHitPlayer(PlayerCharacter player)
    {
        if (state != BatState.Chasing || !IsValidTarget(player))
        {
            return;
        }

        if (nextAllowedHitTime.TryGetValue(player, out float nextTime) && Time.time < nextTime)
        {
            return;
        }

        nextAllowedHitTime[player] = Time.time + contactHitCooldown;
        float horizontalDirection = Mathf.Sign(player.transform.position.x - transform.position.x);
        if (Mathf.Approximately(horizontalDirection, 0f))
        {
            horizontalDirection = Mathf.Approximately(movementDirection.x, 0f) ? 1f : Mathf.Sign(movementDirection.x);
        }

        player.ApplyExternalKnockback(
            new Vector2(horizontalDirection * horizontalKnockback, verticalKnockback),
            inputLockDuration);

        if (player == attackTarget)
        {
            recoveryElapsed = 0f;
            state = BatState.Recovering;
        }
    }

    private bool IsInsideActiveFireLight()
    {
        IReadOnlyList<FireLightSource> sources = FireLightSource.ActiveSources;
        Vector2 position = visualRoot != null ? visualRoot.position : transform.position;
        for (int i = 0; i < sources.Count; i++)
        {
            FireLightSource source = sources[i];
            if (source == null || !source.CanRevealObjects || !source.IsLightActive)
            {
                continue;
            }

            float radius = source.RevealRadius;
            if (((Vector2)source.transform.position - position).sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidTarget(PlayerCharacter player)
    {
        return player != null && player.gameObject.activeInHierarchy && player.LifeState == PlayerLifeState.Alive;
    }

    private void SetVisible(bool visible)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    private void ResolveReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (hitCollider == null)
        {
            hitCollider = GetComponent<Collider2D>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void RebuildObstacleFilter()
    {
        obstacleFilter = new ContactFilter2D();
        obstacleFilter.SetLayerMask(obstacleMask);
        obstacleFilter.useTriggers = !avoidTriggers;
    }

    private void CheckHomePosition()
    {
        homeInsideObstacle = false;
        if (obstacleMask.value == 0)
        {
            return;
        }

        int overlapCount = Physics2D.OverlapCircle(
            homePosition,
            GetEffectiveProbeRadius(),
            obstacleFilter,
            obstacleOverlaps);
        for (int i = 0; i < overlapCount; i++)
        {
            if (!IsValidObstacle(obstacleOverlaps[i]))
            {
                continue;
            }

            homeInsideObstacle = true;
            Debug.LogWarning(
                $"{name}: DarkBat homePosition이 장애물 '{obstacleOverlaps[i].name}' 안에 있습니다. " +
                "복귀 경로가 막힐 수 있으니 Scene Gizmo를 확인하세요.",
                this);
            break;
        }
    }

    private void ConfigurePhysics()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
        attackDistance = Mathf.Max(0f, attackDistance);
        chaseLeashRadius = Mathf.Max(0f, chaseLeashRadius);
        maxChaseDuration = Mathf.Max(0f, maxChaseDuration);
        returnCompleteDistance = Mathf.Max(0.01f, returnCompleteDistance);
        postAttackDelay = Mathf.Max(0f, postAttackDelay);
        horizontalKnockback = Mathf.Max(0f, horizontalKnockback);
        verticalKnockback = Mathf.Max(0f, verticalKnockback);
        inputLockDuration = Mathf.Max(0f, inputLockDuration);
        contactHitCooldown = Mathf.Max(0f, contactHitCooldown);
        curlRadius = Mathf.Max(0f, curlRadius);
        curlAngularSpeed = Mathf.Max(0f, curlAngularSpeed);
        guideAdvanceSpeed = Mathf.Max(0f, guideAdvanceSpeed);
        maxFlightSpeed = Mathf.Max(0.01f, maxFlightSpeed);
        curlStartDistance = Mathf.Max(0f, curlStartDistance);
        fullCurlDistance = Mathf.Max(curlStartDistance + 0.01f, fullCurlDistance);
        targetApproachRadiusMultiplier = Mathf.Clamp01(targetApproachRadiusMultiplier);
        obstacleCurlMultiplier = Mathf.Clamp01(obstacleCurlMultiplier);
        returnCurlMultiplier = Mathf.Clamp01(returnCurlMultiplier);
        directionSmoothTime = Mathf.Max(0.01f, directionSmoothTime);
        obstacleProbeRadius = Mathf.Max(0.01f, obstacleProbeRadius);
        obstacleLookAheadDistance = Mathf.Max(0.05f, obstacleLookAheadDistance);
        obstacleSkin = Mathf.Clamp(obstacleSkin, 0f, obstacleLookAheadDistance * 0.5f);
        avoidanceTurnSpeed = Mathf.Max(0.01f, avoidanceTurnSpeed);
        avoidanceDirectionLockDuration = Mathf.Max(0f, avoidanceDirectionLockDuration);
        wallSlideMultiplier = Mathf.Clamp01(wallSlideMultiplier);
        stuckCheckDuration = Mathf.Max(0.05f, stuckCheckDuration);
        stuckMovementThreshold = Mathf.Max(0f, stuckMovementThreshold);
        ResolveReferences();
        ConfigurePhysics();
        RebuildObstacleFilter();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos && !showAvoidanceGizmos)
        {
            return;
        }

        Vector3 home = Application.isPlaying ? (Vector3)homePosition : transform.position;
        if (showDebugGizmos)
        {
            Gizmos.color = homeInsideObstacle ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(home, 0.18f);
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(
                Application.isPlaying ? (Vector3)activationOrigin : transform.position,
                chaseLeashRadius);

            if (Application.isPlaying && attackTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, attackTarget.transform.position);
            }

            if (Application.isPlaying && movementDirection.sqrMagnitude > 0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + (Vector3)movementDirection);
            }
        }

        if (!showAvoidanceGizmos || !Application.isPlaying)
        {
            return;
        }

        float probeRadius = GetEffectiveProbeRadius();
        Vector3 current = body != null ? (Vector3)body.position : transform.position;
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(current, probeRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(current, guidePosition);
        Gizmos.DrawWireSphere(guidePosition, 0.1f);
        Gizmos.DrawLine(guidePosition, guidePosition + guideForward);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(guidePosition, currentCurlRadius);
        Gizmos.DrawLine(guidePosition, guidePosition + orbitOffset);

        for (int i = 0; i < debugCandidateCount; i++)
        {
            bool selected = Vector2.Dot(debugCandidateDirections[i], selectedAvoidanceDirection) > 0.999f;
            Gizmos.color = selected
                ? Color.green
                : new Color(1f, 1f, 0f, 0.35f);
            Vector3 end = current + (Vector3)(debugCandidateDirections[i] * debugCandidateClearances[i]);
            Gizmos.DrawLine(current, end);
            Gizmos.DrawWireSphere(end, probeRadius * 0.35f);
        }

        if (lastObstacleNormal.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(current, current + (Vector3)lastObstacleNormal);
        }

        UnityEditor.Handles.color = stuckDetected ? Color.red : Color.white;
        UnityEditor.Handles.Label(
            current + Vector3.up * 0.65f,
            $"{state} | Curl {currentCurlRadius:0.00} | Avoid {avoidingObstacle} | Stuck {stuckDetected}");
    }
#endif
}
