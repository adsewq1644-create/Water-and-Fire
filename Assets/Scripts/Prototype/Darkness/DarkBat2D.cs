using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(NavMeshAgent), typeof(BatNavMeshMotor2D))]
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
    [SerializeField] private BatNavMeshMotor2D motor;
    [SerializeField] private AudioSource wingAudio;
    [SerializeField] private ParticleSystem deathParticles;

    [Header("Movement Rules")]
    [SerializeField] private float chaseSpeed = 7f;
    [SerializeField] private float returnSpeed = 5f;
    [SerializeField] private float attackDistance = 0.3f;
    [SerializeField] private float chaseLeashRadius = 6f;
    [SerializeField] private float maxChaseDuration = 2.5f;
    [SerializeField] private float postAttackDelay = 0.3f;

    [Header("Visual Curled Flight")]
    [SerializeField] private bool useCurledFlight = true;
    [SerializeField] private float curlRadius = 0.45f;
    [SerializeField] private float curlAngularSpeed = 8f;
    [SerializeField] private float curlRadiusSmoothSpeed = 6f;
    [SerializeField] private float targetApproachDistance = 1.2f;
    [SerializeField, Range(0f, 1f)] private float targetApproachRadiusMultiplier = 0.15f;
    [SerializeField, Range(0f, 1f)] private float narrowSpaceCurlMultiplier = 0.15f;
    [SerializeField, Range(0f, 1f)] private float movingObstacleCurlMultiplier = 0.1f;
    [SerializeField, Range(0f, 1f)] private float returnCurlMultiplier = 0.6f;
    [SerializeField] private float visualCollisionRadius = 0.2f;
    [SerializeField] private bool clockwiseCurl = true;
    [SerializeField] private bool alternateInitialCurlDirection = true;

    [Header("Knockback")]
    [SerializeField] private float horizontalKnockback = 8f;
    [SerializeField] private float verticalKnockback = 4f;
    [SerializeField] private float inputLockDuration = 0.15f;
    [SerializeField] private float contactHitCooldown = 0.75f;

    [Header("Visibility")]
    [SerializeField] private float visibilityCheckInterval = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private readonly Dictionary<PlayerCharacter, float> nextAllowedHitTime =
        new Dictionary<PlayerCharacter, float>();

    private Vector2 homePosition;
    private Vector2 activationOrigin;
    private Vector2 currentVisualOffset;
    private PlayerCharacter attackTarget;
    private float chaseElapsed;
    private float recoveryElapsed;
    private float curlPhase;
    private float currentCurlRadius;
    private int curlDirectionSign = 1;
    private float nextVisibilityCheckTime;
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
        if (motor != null)
        {
            motor.InitializeHome(homePosition);
        }

        InitializeCurl(true);
        nextVisibilityCheckTime = Time.time + Random.Range(0f, visibilityCheckInterval);
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        nextAllowedHitTime.Clear();
        if (motor != null)
        {
            motor.StopMovement();
        }

        if (wingAudio != null)
        {
            wingAudio.Stop();
        }
    }

    private void Update()
    {
        if (state != BatState.Dead && Time.time >= nextVisibilityCheckTime)
        {
            SetVisible(IsInsideActiveFireLight());
            nextVisibilityCheckTime = Time.time + visibilityCheckInterval;
        }

        UpdateVisualCurl(Time.deltaTime);
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
        InitializeCurl(false);
        if (motor != null)
        {
            motor.BeginChase(attackTarget.transform, chaseSpeed);
        }

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
        currentVisualOffset = Vector2.zero;
        currentCurlRadius = 0f;
        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
        }

        if (motor != null)
        {
            motor.StopMovement();
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
            Vector2.Distance(attackTarget.transform.position, activationOrigin) > chaseLeashRadius ||
            motor == null || motor.HasChaseNavigationFailed)
        {
            BeginReturn();
            return;
        }

        Vector2 attackPosition = hitCollider != null ? hitCollider.bounds.center : (Vector2)visualRoot.position;
        if (Vector2.Distance(attackPosition, attackTarget.transform.position) <= attackDistance)
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
        if (motor == null || motor.HasReturnNavigationFailed)
        {
            // Stay at the last safe NavMesh position. A later vibration can start a new chase.
            return;
        }

        if (!motor.IsDestinationReached)
        {
            return;
        }

        motor.CompleteAtDestination();
        attackTarget = null;
        currentVisualOffset = Vector2.zero;
        currentCurlRadius = 0f;
        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
        }

        state = BatState.Dormant;
    }

    private void BeginReturn()
    {
        attackTarget = null;
        if (motor != null)
        {
            motor.BeginReturn(homePosition, returnSpeed);
        }

        state = BatState.Returning;
    }

    private void UpdateVisualCurl(float deltaTime)
    {
        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        bool flying = useCurledFlight && motor != null &&
            (state == BatState.Chasing || state == BatState.Returning) &&
            motor.Condition != BatNavMeshMotor2D.NavigationCondition.Failed;
        float targetRadius = flying ? CalculateTargetCurlRadius() : 0f;
        float radiusBlend = 1f - Mathf.Exp(-curlRadiusSmoothSpeed * Mathf.Max(0f, deltaTime));
        currentCurlRadius = Mathf.Lerp(currentCurlRadius, targetRadius, radiusBlend);

        if (flying)
        {
            curlPhase += curlAngularSpeed * curlDirectionSign * deltaTime;
        }

        Vector2 forward = motor != null ? motor.CurrentPathDirection : Vector2.right;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.right;
        }

        forward.Normalize();
        Vector2 right = new Vector2(-forward.y, forward.x);
        Vector2 desiredOffset = right * Mathf.Sin(curlPhase) * currentCurlRadius -
            forward * Mathf.Cos(curlPhase) * currentCurlRadius;
        currentVisualOffset = motor != null
            ? motor.ClampVisualOffset(desiredOffset, visualCollisionRadius)
            : desiredOffset;

        Vector3 localOffset = transform.InverseTransformVector(currentVisualOffset);
        visualRoot.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
    }

    private float CalculateTargetCurlRadius()
    {
        float radius = curlRadius;
        if (state == BatState.Returning)
        {
            radius *= returnCurlMultiplier;
        }

        if (motor.IsBlockedByMovingObstacle)
        {
            radius *= movingObstacleCurlMultiplier;
        }

        Vector2 destination = state == BatState.Chasing && attackTarget != null
            ? (Vector2)attackTarget.transform.position
            : motor.HomeNavPosition;
        float distance = Vector2.Distance(transform.position, destination);
        if (distance < targetApproachDistance)
        {
            float approach = Mathf.InverseLerp(0f, targetApproachDistance, distance);
            radius *= Mathf.Lerp(targetApproachRadiusMultiplier, 1f, approach);
        }

        Vector2 unclamped = GetRawOrbitOffset(radius);
        Vector2 clamped = motor.ClampVisualOffset(unclamped, visualCollisionRadius);
        if (unclamped.sqrMagnitude > 0.0001f && clamped.sqrMagnitude < unclamped.sqrMagnitude * 0.5f)
        {
            radius *= narrowSpaceCurlMultiplier;
        }

        return radius;
    }

    private Vector2 GetRawOrbitOffset(float radius)
    {
        Vector2 forward = motor != null ? motor.CurrentPathDirection : Vector2.right;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.right;
        }

        forward.Normalize();
        Vector2 right = new Vector2(-forward.y, forward.x);
        return right * Mathf.Sin(curlPhase) * radius - forward * Mathf.Cos(curlPhase) * radius;
    }

    private void InitializeCurl(bool resetPhase)
    {
        if (alternateInitialCurlDirection)
        {
            curlDirectionSign = (GetInstanceID() & 1) == 0 ? 1 : -1;
        }
        else
        {
            curlDirectionSign = clockwiseCurl ? -1 : 1;
        }

        if (resetPhase)
        {
            curlPhase = Mathf.Abs(GetInstanceID() % 628) * 0.01f;
        }
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
            Vector2 direction = motor != null ? motor.FinalMoveDirection : Vector2.right;
            horizontalDirection = Mathf.Approximately(direction.x, 0f) ? 1f : Mathf.Sign(direction.x);
        }

        player.ApplyExternalKnockback(
            new Vector2(horizontalDirection * horizontalKnockback, verticalKnockback),
            inputLockDuration);

        if (player == attackTarget)
        {
            recoveryElapsed = 0f;
            if (motor != null)
            {
                motor.StopMovement();
            }

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

        if (motor == null)
        {
            motor = GetComponent<BatNavMeshMotor2D>();
        }

        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualRoot");
            visualRoot = candidate != null ? candidate : transform;
        }

        if (hitCollider == null && visualRoot != null)
        {
            hitCollider = visualRoot.GetComponentInChildren<Collider2D>(true);
        }

        if (hitCollider == null)
        {
            hitCollider = GetComponentInChildren<Collider2D>(true);
        }

        if (renderers == null || renderers.Length == 0)
        {
            renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void ConfigurePhysics()
    {
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
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
        postAttackDelay = Mathf.Max(0f, postAttackDelay);
        curlRadius = Mathf.Max(0f, curlRadius);
        curlAngularSpeed = Mathf.Max(0f, curlAngularSpeed);
        curlRadiusSmoothSpeed = Mathf.Max(0.01f, curlRadiusSmoothSpeed);
        targetApproachDistance = Mathf.Max(0.01f, targetApproachDistance);
        visualCollisionRadius = Mathf.Max(0.01f, visualCollisionRadius);
        horizontalKnockback = Mathf.Max(0f, horizontalKnockback);
        verticalKnockback = Mathf.Max(0f, verticalKnockback);
        inputLockDuration = Mathf.Max(0f, inputLockDuration);
        contactHitCooldown = Mathf.Max(0f, contactHitCooldown);
        visibilityCheckInterval = Mathf.Max(0.02f, visibilityCheckInterval);
        ResolveReferences();
        ConfigurePhysics();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Vector3 home = Application.isPlaying ? (Vector3)homePosition : transform.position;
        Gizmos.color = Color.cyan;
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

        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)currentVisualOffset);
        }

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.1f,
            $"{state} | Target {(attackTarget != null ? attackTarget.name : "none")} | Curl {currentCurlRadius:0.00}");
    }
#endif
}
