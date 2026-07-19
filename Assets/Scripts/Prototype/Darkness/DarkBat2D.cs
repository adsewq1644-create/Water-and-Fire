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

    [Header("Knockback")]
    [SerializeField] private float horizontalKnockback = 8f;
    [SerializeField] private float verticalKnockback = 4f;
    [SerializeField] private float inputLockDuration = 0.15f;
    [SerializeField] private float contactHitCooldown = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private readonly Dictionary<PlayerCharacter, float> nextAllowedHitTime = new Dictionary<PlayerCharacter, float>();
    private Vector2 homePosition;
    private Vector2 activationOrigin;
    private Vector2 movementDirection;
    private PlayerCharacter attackTarget;
    private float chaseElapsed;
    private float recoveryElapsed;
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

        MoveTowards(attackTarget.transform.position, chaseSpeed);
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
            body.MovePosition(homePosition);
            movementDirection = Vector2.zero;
            attackTarget = null;
            state = BatState.Dormant;
            return;
        }

        MoveTowards(homePosition, returnSpeed);
    }

    private void MoveTowards(Vector2 destination, float speed)
    {
        Vector2 current = body.position;
        Vector2 delta = destination - current;
        movementDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.zero;
        body.MovePosition(Vector2.MoveTowards(current, destination, Mathf.Max(0f, speed) * Time.fixedDeltaTime));
    }

    private void BeginReturn()
    {
        attackTarget = null;
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
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)activationOrigin : transform.position, chaseLeashRadius);

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
#endif
}
