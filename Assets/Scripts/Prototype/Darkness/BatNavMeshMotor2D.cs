using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(NavMeshAgent))]
public sealed class BatNavMeshMotor2D : MonoBehaviour
{
    public enum NavigationMode
    {
        Stopped,
        Chasing,
        Returning
    }

    public enum NavigationCondition
    {
        Idle,
        NavigatingNormally,
        WaitingForPath,
        WaitingForMovingObstacle,
        AvoidingMovingObstacle,
        Failed
    }

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Rigidbody2D body;

    [Header("NavMesh")]
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;
    [SerializeField] private float navigationRadius = 0.24f;
    [SerializeField] private float targetSampleRadius = 1.5f;
    [SerializeField] private float homeSampleRadius = 1.5f;
    [SerializeField] private float destinationTolerance = 0.1f;

    [Header("Path Refresh")]
    [SerializeField] private float repathIntervalMin = 0.18f;
    [SerializeField] private float repathIntervalMax = 0.32f;
    [SerializeField] private float targetMoveRepathThreshold = 0.35f;
    [SerializeField] private float invalidPathRetryInterval = 0.4f;

    [Header("Moving Obstacles")]
    [SerializeField] private LayerMask staticObstacleMask;
    [SerializeField] private LayerMask movingObstacleMask;
    [SerializeField] private float movingObstacleProbeDistance = 1.2f;
    [SerializeField] private float movingObstacleLookAheadTime = 0.3f;
    [SerializeField] private float movingObstacleStopDistance = 0.34f;
    [SerializeField] private float movingObstacleAvoidDistance = 0.75f;
    [SerializeField] private float movingObstacleAvoidWeight = 0.8f;
    [SerializeField] private float dynamicBlockRepathDelay = 0.35f;
    [SerializeField] private float avoidanceDirectionHoldTime = 0.22f;
    [SerializeField] private float finalMoveSkin = 0.02f;

    [Header("Navigation Failure")]
    [SerializeField] private float pathPendingTimeout = 1f;
    [SerializeField] private float noValidPathTimeout = 1.5f;
    [SerializeField] private float noProgressTimeout = 2f;
    [SerializeField] private float noProgressDistanceThreshold = 0.08f;
    [SerializeField] private float dynamicBlockedTimeout = 2.5f;
    [SerializeField] private float totalNavigationFailureTimeout = 3.5f;
    [SerializeField] private int maxConsecutivePathFailures = 3;
    [SerializeField] private int maxConsecutiveRepathWithoutProgress = 4;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private NavMeshPath validationPath;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[24];
    private readonly Collider2D[] overlapHits = new Collider2D[24];

    private Transform target;
    private Vector2 homeNavPosition;
    private Vector2 requestedDestination;
    private Vector2 sampledDestination;
    private Vector2 lastRequestedTargetPosition;
    private Vector2 lastProgressPosition;
    private Vector2 lastPathRequestPosition;
    private Vector2 currentPathDirection = Vector2.right;
    private Vector2 finalMoveDirection;
    private Vector2 heldAvoidanceDirection;
    private MovingNavObstacle2D blockingObstacle;
    private float movementSpeed;
    private float nextRepathTime;
    private float pathPendingElapsed;
    private float noValidPathElapsed;
    private float noProgressElapsed;
    private float dynamicBlockedElapsed;
    private float totalFailureElapsed;
    private float avoidanceHoldRemaining;
    private float lastPathRequestTime = float.NegativeInfinity;
    private float navigationPlaneZ;
    private int consecutivePathFailures;
    private int consecutiveRepathsWithoutProgress;
    private int navMeshCalculatePathCount;
    private int setDestinationCount;
    private int physicsQueryCount;
    private bool destinationRequested;
    private bool destinationReached;
    private bool hasNavigationPlaneZ;
    private bool chaseNavigationFailed;
    private bool returnNavigationFailed;
    private NavigationMode mode;
    private NavigationCondition condition;

    public NavigationMode Mode => mode;
    public NavigationCondition Condition => condition;
    public bool HasValidPath => agent != null && agent.enabled && agent.isOnNavMesh &&
        !agent.pathPending && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete;
    public bool IsDestinationReached => destinationReached;
    public Vector2 CurrentPathDirection => currentPathDirection;
    public Vector2 FinalMoveDirection => finalMoveDirection;
    public bool IsBlockedByMovingObstacle => blockingObstacle != null;
    public MovingNavObstacle2D BlockingObstacle => blockingObstacle;
    public bool HasChaseNavigationFailed => chaseNavigationFailed;
    public bool HasReturnNavigationFailed => returnNavigationFailed;
    public Vector2 HomeNavPosition => homeNavPosition;
    public Vector2 CurrentDestination => sampledDestination;
    public float NoValidPathElapsed => noValidPathElapsed;
    public float NoProgressElapsed => noProgressElapsed;
    public float DynamicBlockedElapsed => dynamicBlockedElapsed;
    public float TotalFailureElapsed => totalFailureElapsed;
    public int ConsecutivePathFailures => consecutivePathFailures;
    public int ConsecutiveRepathsWithoutProgress => consecutiveRepathsWithoutProgress;
    public float LastPathRequestTime => lastPathRequestTime;
    public Vector2 LastProgressPosition => lastProgressPosition;
    public int NavMeshCalculatePathCount => navMeshCalculatePathCount;
    public int SetDestinationCount => setDestinationCount;
    public int PhysicsQueryCount => physicsQueryCount;
    public Vector2 BlockingRelativeVelocity => blockingObstacle != null && body != null
        ? body.linearVelocity - blockingObstacle.Velocity
        : Vector2.zero;
    public bool AgentIsOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh;
    public bool IsPathPending => agent != null && agent.pathPending;
    public NavMeshPathStatus CurrentPathStatus => agent != null ? agent.pathStatus : NavMeshPathStatus.PathInvalid;

    private void Reset()
    {
        ResolveReferences();
        ConfigureComponents();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureComponents();
        validationPath = new NavMeshPath();
        lastProgressPosition = body != null ? body.position : (Vector2)transform.position;
        RandomizeNextRepath(0f);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureComponents();
        SyncAgentPosition();
    }

    private void OnDisable()
    {
        StopMovement();
    }

    private void FixedUpdate()
    {
        if (mode == NavigationMode.Stopped || body == null)
        {
            finalMoveDirection = Vector2.zero;
            return;
        }

        TickNavigation(Time.fixedDeltaTime);
    }

    public bool InitializeHome(Vector2 requestedHome)
    {
        if (!TrySamplePosition(requestedHome, homeSampleRadius, out homeNavPosition))
        {
            homeNavPosition = requestedHome;
            Debug.LogWarning($"{name}: no BatFlyingAgent NavMesh point was found near home {requestedHome}.", this);
            return false;
        }

        if (body != null && Vector2.Distance(body.position, homeNavPosition) <= homeSampleRadius)
        {
            EnsureAgentOnNavMesh();
        }

        return true;
    }

    public void BeginChase(Transform chaseTarget, float speed)
    {
        target = chaseTarget;
        movementSpeed = Mathf.Max(0f, speed);
        mode = NavigationMode.Chasing;
        ResetNavigationAttempt();
        if (target != null)
        {
            requestedDestination = target.position;
        }
        RandomizeNextRepath(0f);
    }

    public void BeginReturn(Vector2 requestedHome, float speed)
    {
        target = null;
        movementSpeed = Mathf.Max(0f, speed);
        mode = NavigationMode.Returning;
        if (!TrySamplePosition(requestedHome, homeSampleRadius, out homeNavPosition))
        {
            homeNavPosition = requestedHome;
        }

        requestedDestination = homeNavPosition;
        sampledDestination = homeNavPosition;
        ResetNavigationAttempt();
        RandomizeNextRepath(0f);
    }

    public void StopMovement()
    {
        mode = NavigationMode.Stopped;
        condition = NavigationCondition.Idle;
        target = null;
        blockingObstacle = null;
        finalMoveDirection = Vector2.zero;
        destinationRequested = false;
        destinationReached = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    public void CompleteAtDestination()
    {
        if (body != null)
        {
            body.MovePosition(sampledDestination);
            body.linearVelocity = Vector2.zero;
        }

        SyncAgentPosition();
        StopMovement();
    }

    public Vector2 ClampVisualOffset(Vector2 desiredWorldOffset, float visualRadius)
    {
        if (body == null || desiredWorldOffset.sqrMagnitude <= 0.000001f)
        {
            return Vector2.zero;
        }

        float distance = desiredWorldOffset.magnitude;
        Vector2 direction = desiredWorldOffset / distance;
        int mask = staticObstacleMask.value | movingObstacleMask.value;
        physicsQueryCount++;
        int count = Physics2D.CircleCast(
            body.position,
            Mathf.Max(0.01f, visualRadius),
            direction,
            CreateContactFilter(mask),
            castHits,
            distance);
        float allowedDistance = distance;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = castHits[i].collider;
            if (!IsBlockingCollider(hit))
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, castHits[i].distance - finalMoveSkin));
        }

        Vector2 result = direction * allowedDistance;
        Vector2 candidate = body.position + result;
        physicsQueryCount++;
        count = Physics2D.OverlapCircle(
            candidate,
            Mathf.Max(0.01f, visualRadius),
            CreateContactFilter(mask),
            overlapHits);
        for (int i = 0; i < count; i++)
        {
            if (IsBlockingCollider(overlapHits[i]))
            {
                return result * 0.25f;
            }
        }

        return result;
    }

    private void TickNavigation(float deltaTime)
    {
        if (!EnsureAgentOnNavMesh())
        {
            noValidPathElapsed += deltaTime;
            totalFailureElapsed += deltaTime;
            condition = NavigationCondition.WaitingForPath;
            EvaluateFailure();
            return;
        }

        SyncAgentPosition();
        requestedDestination = mode == NavigationMode.Chasing && target != null
            ? (Vector2)target.position
            : homeNavPosition;

        if (mode == NavigationMode.Returning &&
            (homeNavPosition - body.position).sqrMagnitude <= destinationTolerance * destinationTolerance)
        {
            sampledDestination = homeNavPosition;
            destinationReached = true;
            finalMoveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            condition = NavigationCondition.Idle;
            ResetProgressTimers();
            return;
        }

        UpdatePathRequest(deltaTime);
        UpdatePathHealth(deltaTime);
        if (mode == NavigationMode.Stopped)
        {
            return;
        }

        if (destinationRequested &&
            (sampledDestination - body.position).sqrMagnitude <= destinationTolerance * destinationTolerance)
        {
            destinationReached = true;
            finalMoveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            condition = NavigationCondition.Idle;
            ResetProgressTimers();
            return;
        }

        if (!HasValidPath)
        {
            finalMoveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            condition = NavigationCondition.WaitingForPath;
            EvaluateFailure();
            return;
        }

        Vector2 toDestination = sampledDestination - body.position;
        destinationReached = toDestination.sqrMagnitude <= destinationTolerance * destinationTolerance;
        if (destinationReached)
        {
            finalMoveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            condition = NavigationCondition.Idle;
            ResetProgressTimers();
            return;
        }

        Vector2 steeringTarget = agent.steeringTarget;
        Vector2 desiredDirection = steeringTarget - body.position;
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            desiredDirection = toDestination;
        }

        currentPathDirection = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : currentPathDirection;

        Vector2 selectedDirection = ResolveMovingObstacleDirection(currentPathDirection, deltaTime);
        if (selectedDirection.sqrMagnitude <= 0.0001f)
        {
            finalMoveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            UpdateProgress(deltaTime);
            EvaluateFailure();
            return;
        }

        float wantedDistance = movementSpeed * deltaTime;
        int safetyMask = condition == NavigationCondition.AvoidingMovingObstacle
            ? staticObstacleMask.value | movingObstacleMask.value
            : movingObstacleMask.value;
        float allowedDistance = GetAllowedMoveDistance(
            body.position,
            selectedDirection,
            wantedDistance,
            safetyMask);

        if (allowedDistance <= 0.0001f)
        {
            finalMoveDirection = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            totalFailureElapsed += deltaTime;
            UpdateProgress(deltaTime);
            EvaluateFailure();
            return;
        }

        finalMoveDirection = selectedDirection;
        body.MovePosition(body.position + selectedDirection * allowedDistance);
        body.linearVelocity = selectedDirection * movementSpeed;
        agent.nextPosition = new Vector3(body.position.x, body.position.y, transform.position.z);
        UpdateProgress(deltaTime);
        EvaluateFailure();
    }

    private void UpdatePathRequest(float deltaTime)
    {
        if (agent.pathPending)
        {
            pathPendingElapsed += deltaTime;
            condition = NavigationCondition.WaitingForPath;
            return;
        }

        pathPendingElapsed = 0f;
        bool targetMoved = (requestedDestination - lastRequestedTargetPosition).sqrMagnitude >=
            targetMoveRepathThreshold * targetMoveRepathThreshold;
        bool hasValidPath = HasValidPath;
        bool intervalReached = Time.time >= nextRepathTime;
        bool needsInitialPath = !destinationRequested && consecutivePathFailures == 0;
        bool needsMissingPathRetry = !destinationRequested && consecutivePathFailures > 0 && intervalReached;
        bool needsLostPathRetry = destinationRequested && !hasValidPath && intervalReached;
        bool needsMovedTargetRepath = hasValidPath && intervalReached && targetMoved;
        bool needsDynamicRepath = blockingObstacle != null &&
            dynamicBlockedElapsed >= dynamicBlockRepathDelay && intervalReached;

        if (!needsInitialPath && !needsMissingPathRetry && !needsLostPathRetry &&
            !needsMovedTargetRepath && !needsDynamicRepath)
        {
            return;
        }

        bool wasDestinationRequested = destinationRequested;
        bool hadValidPath = hasValidPath;
        lastPathRequestTime = Time.time;
        if (TryRequestPath(requestedDestination))
        {
            destinationRequested = true;
            lastRequestedTargetPosition = requestedDestination;
            if (wasDestinationRequested &&
                (body.position - lastPathRequestPosition).sqrMagnitude <=
                noProgressDistanceThreshold * noProgressDistanceThreshold)
            {
                consecutiveRepathsWithoutProgress++;
            }
            else
            {
                consecutiveRepathsWithoutProgress = 0;
            }

            lastPathRequestPosition = body.position;
            RandomizeNextRepath(repathIntervalMin);
        }
        else
        {
            destinationRequested = hadValidPath;
            consecutivePathFailures++;
            nextRepathTime = Time.time + invalidPathRetryInterval + Random.Range(0f, 0.08f);
        }
    }

    private bool TryRequestPath(Vector2 destination)
    {
        if (validationPath == null)
        {
            validationPath = new NavMeshPath();
        }

        float sampleRadius = mode == NavigationMode.Returning ? homeSampleRadius : targetSampleRadius;
        if (!TrySampleNavMeshPoint(destination, sampleRadius, out Vector3 targetNavPosition))
        {
            return false;
        }

        float originSampleRadius = Mathf.Max(navigationRadius * 2f, 0.5f);
        if (!TrySampleNavMeshPoint(body.position, originSampleRadius, out Vector3 originNavPosition))
        {
            return false;
        }

        var filter = GetQueryFilter();
        navMeshCalculatePathCount++;
        if (!NavMesh.CalculatePath(originNavPosition, targetNavPosition, filter, validationPath) ||
            validationPath.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        setDestinationCount++;
        if (!agent.SetDestination(targetNavPosition))
        {
            return false;
        }

        sampledDestination = new Vector2(targetNavPosition.x, targetNavPosition.y);
        consecutivePathFailures = 0;
        noValidPathElapsed = 0f;
        return true;
    }

    private void UpdatePathHealth(float deltaTime)
    {
        if (agent.pathPending)
        {
            return;
        }

        if (HasValidPath)
        {
            noValidPathElapsed = 0f;
            return;
        }

        noValidPathElapsed += deltaTime;
        totalFailureElapsed += deltaTime;
        if (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            consecutivePathFailures++;
            agent.ResetPath();
            destinationRequested = false;
        }
    }

    private Vector2 ResolveMovingObstacleDirection(Vector2 desiredDirection, float deltaTime)
    {
        blockingObstacle = FindBlockingMovingObstacle(body.position, desiredDirection, movingObstacleProbeDistance);
        if (blockingObstacle == null)
        {
            dynamicBlockedElapsed = 0f;
            avoidanceHoldRemaining = Mathf.Max(0f, avoidanceHoldRemaining - deltaTime);
            condition = NavigationCondition.NavigatingNormally;
            return desiredDirection;
        }

        dynamicBlockedElapsed += deltaTime;
        totalFailureElapsed += deltaTime;
        if (avoidanceHoldRemaining > 0f &&
            IsDirectionClear(body.position, heldAvoidanceDirection, movingObstacleAvoidDistance))
        {
            avoidanceHoldRemaining -= deltaTime;
            condition = NavigationCondition.AvoidingMovingObstacle;
            return heldAvoidanceDirection;
        }

        Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
        Vector2 first = (desiredDirection + perpendicular * movingObstacleAvoidWeight).normalized;
        Vector2 second = (desiredDirection - perpendicular * movingObstacleAvoidWeight).normalized;
        if ((blockingObstacle.Velocity.x * perpendicular.x + blockingObstacle.Velocity.y * perpendicular.y) > 0f)
        {
            Vector2 swap = first;
            first = second;
            second = swap;
        }

        if (TrySelectAvoidanceDirection(first, out Vector2 selected) ||
            TrySelectAvoidanceDirection(second, out selected))
        {
            heldAvoidanceDirection = selected;
            avoidanceHoldRemaining = avoidanceDirectionHoldTime;
            condition = NavigationCondition.AvoidingMovingObstacle;
            return selected;
        }

        condition = NavigationCondition.WaitingForMovingObstacle;
        return Vector2.zero;
    }

    private bool TrySelectAvoidanceDirection(Vector2 direction, out Vector2 selected)
    {
        selected = direction;
        if (!IsDirectionClear(body.position, direction, movingObstacleAvoidDistance))
        {
            return false;
        }

        Vector2 candidate = body.position + direction * movingObstacleAvoidDistance;
        if (!TrySamplePosition(candidate, navigationRadius * 2f, out Vector2 sampled))
        {
            return false;
        }

        return (sampled - candidate).sqrMagnitude <= navigationRadius * navigationRadius * 4f;
    }

    private MovingNavObstacle2D FindBlockingMovingObstacle(Vector2 origin, Vector2 direction, float distance)
    {
        IReadOnlyList<MovingNavObstacle2D> obstacles = MovingNavObstacle2D.ActiveObstacles;
        MovingNavObstacle2D closest = null;
        float closestForward = float.PositiveInfinity;
        Vector2 side = new Vector2(-direction.y, direction.x);

        for (int i = 0; i < obstacles.Count; i++)
        {
            MovingNavObstacle2D obstacle = obstacles[i];
            if (obstacle == null || !obstacle.isActiveAndEnabled ||
                !obstacle.TryGetClosestPoint(origin, out Vector2 closestPoint, out Collider2D source))
            {
                continue;
            }

            if (((1 << source.gameObject.layer) & movingObstacleMask.value) == 0)
            {
                continue;
            }

            Vector2 toObstacle = closestPoint - origin;
            Vector2 batVelocity = body != null ? body.linearVelocity : Vector2.zero;
            Vector2 relativeVelocity = batVelocity - obstacle.Velocity;
            Vector2 predictedOffset = toObstacle - relativeVelocity * movingObstacleLookAheadTime;
            float forward = Vector2.Dot(toObstacle, direction);
            float lateral = Mathf.Abs(Vector2.Dot(toObstacle, side));
            float predictedForward = Vector2.Dot(predictedOffset, direction);
            float predictedLateral = Mathf.Abs(Vector2.Dot(predictedOffset, side));
            bool blocksNow = forward >= -navigationRadius && forward <= distance &&
                lateral <= navigationRadius * 1.5f;
            bool blocksSoon = predictedForward >= -navigationRadius && predictedForward <= distance &&
                predictedLateral <= navigationRadius * 1.5f;
            if (!blocksNow && !blocksSoon)
            {
                continue;
            }

            float threatForward = blocksNow ? forward : predictedForward;
            if (threatForward < closestForward)
            {
                closestForward = threatForward;
                closest = obstacle;
            }
        }

        return closestForward <= movingObstacleStopDistance + navigationRadius ? closest : null;
    }

    private bool IsDirectionClear(Vector2 origin, Vector2 direction, float distance)
    {
        int mask = staticObstacleMask.value | movingObstacleMask.value;
        return GetAllowedMoveDistance(origin, direction, distance, mask) >= distance - 0.001f;
    }

    private float GetAllowedMoveDistance(
        Vector2 origin,
        Vector2 direction,
        float wantedDistance,
        int obstacleMask)
    {
        if (wantedDistance <= 0f || obstacleMask == 0)
        {
            return Mathf.Max(0f, wantedDistance);
        }

        physicsQueryCount++;
        int count = Physics2D.CircleCast(
            origin,
            navigationRadius,
            direction,
            CreateContactFilter(obstacleMask),
            castHits,
            wantedDistance + finalMoveSkin);
        float allowed = wantedDistance;
        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = castHits[i].collider;
            if (!IsBlockingCollider(candidate))
            {
                continue;
            }

            allowed = Mathf.Min(allowed, Mathf.Max(0f, castHits[i].distance - finalMoveSkin));
        }

        return allowed;
    }

    private bool IsBlockingCollider(Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled || candidate.isTrigger ||
            candidate.transform == transform || candidate.transform.IsChildOf(transform))
        {
            return false;
        }

        int layerBit = 1 << candidate.gameObject.layer;
        return (layerBit & staticObstacleMask.value) != 0 ||
            ((layerBit & movingObstacleMask.value) != 0 &&
             candidate.GetComponentInParent<MovingNavObstacle2D>() != null);
    }

    private static ContactFilter2D CreateContactFilter(int layerMask)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(layerMask);
        filter.useTriggers = false;
        return filter;
    }

    private void UpdateProgress(float deltaTime)
    {
        if ((body.position - lastProgressPosition).sqrMagnitude >=
            noProgressDistanceThreshold * noProgressDistanceThreshold)
        {
            lastProgressPosition = body.position;
            noProgressElapsed = 0f;
            totalFailureElapsed = 0f;
            consecutiveRepathsWithoutProgress = 0;
            return;
        }

        if (!destinationReached)
        {
            noProgressElapsed += deltaTime;
            totalFailureElapsed += deltaTime;
        }
    }

    private void EvaluateFailure()
    {
        if (destinationReached || mode == NavigationMode.Stopped)
        {
            return;
        }

        bool failed = pathPendingElapsed >= pathPendingTimeout ||
            noValidPathElapsed >= noValidPathTimeout ||
            noProgressElapsed >= noProgressTimeout ||
            dynamicBlockedElapsed >= dynamicBlockedTimeout ||
            totalFailureElapsed >= totalNavigationFailureTimeout ||
            consecutivePathFailures >= maxConsecutivePathFailures ||
            consecutiveRepathsWithoutProgress >= maxConsecutiveRepathWithoutProgress;
        if (!failed)
        {
            return;
        }

        NavigationMode failedMode = mode;
        StopMovement();
        condition = NavigationCondition.Failed;
        chaseNavigationFailed = failedMode == NavigationMode.Chasing;
        returnNavigationFailed = failedMode == NavigationMode.Returning;
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (agent == null || body == null)
        {
            return false;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            return true;
        }

        if (!TrySampleNavMeshPoint(body.position, homeSampleRadius, out Vector3 sampled))
        {
            return false;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
        }

        if (!agent.Warp(sampled))
        {
            return false;
        }

        body.position = new Vector2(sampled.x, sampled.y);
        return agent.isOnNavMesh;
    }

    private bool TrySamplePosition(Vector2 position, float radius, out Vector2 sampled)
    {
        if (TrySampleNavMeshPoint(position, radius, out Vector3 navPosition))
        {
            sampled = new Vector2(navPosition.x, navPosition.y);
            return true;
        }

        sampled = position;
        return false;
    }

    private bool TrySampleNavMeshPoint(Vector2 position, float radius, out Vector3 sampled)
    {
        float queryZ = hasNavigationPlaneZ ? navigationPlaneZ : transform.position.z;
        Vector3 query = new Vector3(position.x, position.y, queryZ);
        if (NavMesh.SamplePosition(query, out NavMeshHit hit, Mathf.Max(0.01f, radius), GetQueryFilter()))
        {
            sampled = hit.position;
            navigationPlaneZ = sampled.z;
            hasNavigationPlaneZ = true;
            return true;
        }

        sampled = query;
        return false;
    }

    private NavMeshQueryFilter GetQueryFilter()
    {
        return new NavMeshQueryFilter
        {
            agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = navMeshAreaMask
        };
    }

    private void SyncAgentPosition()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh && body != null)
        {
            float navZ = hasNavigationPlaneZ ? navigationPlaneZ : agent.nextPosition.z;
            agent.nextPosition = new Vector3(body.position.x, body.position.y, navZ);
        }
    }

    private void ResetNavigationAttempt()
    {
        chaseNavigationFailed = false;
        returnNavigationFailed = false;
        destinationRequested = false;
        destinationReached = false;
        pathPendingElapsed = 0f;
        noValidPathElapsed = 0f;
        noProgressElapsed = 0f;
        dynamicBlockedElapsed = 0f;
        totalFailureElapsed = 0f;
        avoidanceHoldRemaining = 0f;
        consecutivePathFailures = 0;
        consecutiveRepathsWithoutProgress = 0;
        lastProgressPosition = body != null ? body.position : (Vector2)transform.position;
        lastPathRequestPosition = lastProgressPosition;
        blockingObstacle = null;
        condition = NavigationCondition.WaitingForPath;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
    }

    private void ResetProgressTimers()
    {
        noProgressElapsed = 0f;
        noValidPathElapsed = 0f;
        dynamicBlockedElapsed = 0f;
        totalFailureElapsed = 0f;
        lastProgressPosition = body.position;
    }

    private void RandomizeNextRepath(float minimumDelay)
    {
        float min = Mathf.Max(minimumDelay, repathIntervalMin);
        float max = Mathf.Max(min, repathIntervalMax);
        nextRepathTime = Time.time + Random.Range(min, max);
    }

    private void ResolveReferences()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void ConfigureComponents()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.autoBraking = false;
            agent.autoRepath = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.radius = navigationRadius;
            agent.height = Mathf.Max(0.1f, navigationRadius * 2f);
        }
    }

    private void OnValidate()
    {
        navigationRadius = Mathf.Max(0.01f, navigationRadius);
        targetSampleRadius = Mathf.Max(0.01f, targetSampleRadius);
        homeSampleRadius = Mathf.Max(0.01f, homeSampleRadius);
        destinationTolerance = Mathf.Max(0.01f, destinationTolerance);
        repathIntervalMin = Mathf.Max(0.02f, repathIntervalMin);
        repathIntervalMax = Mathf.Max(repathIntervalMin, repathIntervalMax);
        targetMoveRepathThreshold = Mathf.Max(0.01f, targetMoveRepathThreshold);
        invalidPathRetryInterval = Mathf.Max(0.02f, invalidPathRetryInterval);
        movingObstacleProbeDistance = Mathf.Max(0.05f, movingObstacleProbeDistance);
        movingObstacleLookAheadTime = Mathf.Max(0f, movingObstacleLookAheadTime);
        movingObstacleStopDistance = Mathf.Clamp(movingObstacleStopDistance, 0f, movingObstacleProbeDistance);
        movingObstacleAvoidDistance = Mathf.Max(0.05f, movingObstacleAvoidDistance);
        movingObstacleAvoidWeight = Mathf.Max(0.01f, movingObstacleAvoidWeight);
        dynamicBlockRepathDelay = Mathf.Max(0f, dynamicBlockRepathDelay);
        avoidanceDirectionHoldTime = Mathf.Max(0f, avoidanceDirectionHoldTime);
        finalMoveSkin = Mathf.Max(0f, finalMoveSkin);
        pathPendingTimeout = Mathf.Max(0.05f, pathPendingTimeout);
        noValidPathTimeout = Mathf.Max(0.05f, noValidPathTimeout);
        noProgressTimeout = Mathf.Max(0.05f, noProgressTimeout);
        noProgressDistanceThreshold = Mathf.Max(0.001f, noProgressDistanceThreshold);
        dynamicBlockedTimeout = Mathf.Max(0.05f, dynamicBlockedTimeout);
        totalNavigationFailureTimeout = Mathf.Max(0.05f, totalNavigationFailureTimeout);
        maxConsecutivePathFailures = Mathf.Max(1, maxConsecutivePathFailures);
        maxConsecutiveRepathWithoutProgress = Mathf.Max(1, maxConsecutiveRepathWithoutProgress);
        ResolveReferences();
        ConfigureComponents();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Vector3 current = body != null ? (Vector3)body.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(homeNavPosition, navigationRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sampledDestination, destinationTolerance);
        Gizmos.DrawLine(current, current + (Vector3)currentPathDirection);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(current, current + (Vector3)finalMoveDirection);

        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            Vector3[] corners = agent.path.corners;
            Gizmos.color = agent.pathStatus == NavMeshPathStatus.PathComplete ? Color.cyan : Color.red;
            for (int i = 1; i < corners.Length; i++)
            {
                Gizmos.DrawLine(corners[i - 1], corners[i]);
            }
        }

        if (blockingObstacle != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(current, blockingObstacle.transform.position);
        }

    }
#endif
}
