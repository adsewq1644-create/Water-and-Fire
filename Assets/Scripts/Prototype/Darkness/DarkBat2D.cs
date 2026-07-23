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
    [SerializeField] private float returnFailureRetryDelay = 0.3f;

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
    private PlayerCharacter attackTarget;
    private float chaseElapsed;
    private float recoveryElapsed;
    private float returnFailureRetryElapsed;
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
        ResetVisualOffset();
        nextVisibilityCheckTime = Time.time + Random.Range(0f, visibilityCheckInterval);
        SetVisible(false);
    }

    private void Start()
    {
        // NavMeshSurface registers its baked data during OnEnable. Waiting until Start
        // prevents bats from sampling the world before that data is available.
        if (motor != null)
        {
            motor.InitializeHome(homePosition);
        }
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
        returnFailureRetryElapsed = 0f;
        ResetVisualOffset();
        if (motor != null)
        {
            if (!motor.AgentIsOnNavMesh)
            {
                motor.InitializeHome(homePosition);
            }

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
        ResetVisualOffset();

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
        if (motor == null)
        {
            return;
        }

        if (motor.HasReturnNavigationFailed)
        {
            returnFailureRetryElapsed += Time.fixedDeltaTime;
            if (returnFailureRetryElapsed >= returnFailureRetryDelay)
            {
                returnFailureRetryElapsed = 0f;
                motor.BeginReturn(homePosition, returnSpeed);
            }
            return;
        }

        returnFailureRetryElapsed = 0f;

        if (!motor.IsDestinationReached)
        {
            return;
        }

        motor.CompleteAtDestination();
        attackTarget = null;
        returnFailureRetryElapsed = 0f;
        ResetVisualOffset();

        state = BatState.Dormant;
    }

    private void BeginReturn()
    {
        attackTarget = null;
        returnFailureRetryElapsed = 0f;
        if (motor != null)
        {
            motor.BeginReturn(homePosition, returnSpeed);
        }

        state = BatState.Returning;
    }

    private void ResetVisualOffset()
    {
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.localPosition = Vector3.zero;
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
            if (source == null || !source.IsLightActive)
            {
                continue;
            }

            float radius = source.LightRadius;
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
        returnFailureRetryDelay = Mathf.Max(0.05f, returnFailureRetryDelay);
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

    }
#endif
}
