using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerCharacter : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string playerId = "Player";
    [SerializeField] private ElementType element = ElementType.Water;

    [Header("Input")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode jumpKey = KeyCode.W;
    [SerializeField] private KeyCode alternateJumpKey = KeyCode.Space;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private int fireMouseButton = 0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpVelocity = 13f;
    [SerializeField] private float jumpCutMultiplier = 0.45f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.7f, 0.12f);
    [SerializeField] private float groundCheckOffset = 0.55f;
    [SerializeField] private float groundCastDistance = 0.08f;
    [SerializeField] private float sideCastDistance = 0.08f;
    [SerializeField] private float minimumGroundNormalY = 0.65f;

    [Header("Combat")]
    [SerializeField] private float projectileCooldown = 2f;
    [SerializeField] private float projectileMaxForce = 14f;
    [SerializeField] private float projectileMinForce = 3f;
    [SerializeField] private float projectileLifetime = 6f;
    [SerializeField] private float dragForceScale = 0.08f;
    [SerializeField] private float projectileSpawnOffset = 0.55f;
    [SerializeField] private int trajectoryPoints = 18;
    [SerializeField] private float trajectoryStep = 0.08f;
    [SerializeField] private float pullLineWidth = 0.055f;
    [SerializeField] private float pullLineMinLength = 0.25f;
    [SerializeField] private float pullLineMaxLength = 1.5f;

    [Header("Revive")]
    [SerializeField] private float reviveRange = 1.6f;
    [SerializeField] private float reviveDuration = 5f;

    [Header("Visuals")]
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color revivingColor = new Color(1f, 1f, 0.55f, 1f);

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private LineRenderer trajectoryLine;
    private LineRenderer pullLine;
    private PlayerCharacter partner;
    private Vector3 spawnPosition;
    private float coyoteTimer;
    private bool grounded;
    private bool jumpConsumedUntilLanding;
    private float nextFireTime;
    private bool dragging;
    private Vector2 dragStartScreen;
    private float originalGravityScale;
    private bool originalTrigger;
    private RigidbodyType2D originalBodyType;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[6];
    private readonly RaycastHit2D[] sideHits = new RaycastHit2D[6];
    private static PhysicsMaterial2D frictionlessMaterial;

    public string PlayerId => playerId;
    public ElementType Element => element;
    public PlayerLifeState LifeState { get; private set; } = PlayerLifeState.Alive;
    public HeldToolType HeldTool { get; private set; } = HeldToolType.None;
    public float ReviveProgress { get; private set; }
    public Collider2D BodyCollider => bodyCollider;
    public bool IsAliveLike => LifeState == PlayerLifeState.Alive || LifeState == PlayerLifeState.ReviveCaster;
    public bool IsDeadLike => LifeState == PlayerLifeState.Dead || LifeState == PlayerLifeState.ReviveTarget;

    public void Configure(string id, ElementType characterElement, KeyCode left, KeyCode right, KeyCode jump, KeyCode alternateJump, KeyCode interact, int mouseButton)
    {
        playerId = id;
        element = characterElement;
        moveLeftKey = left;
        moveRightKey = right;
        jumpKey = jump;
        alternateJumpKey = alternateJump;
        interactKey = interact;
        fireMouseButton = mouseButton;
        ApplyElementColor();
    }

    public void SetPartner(PlayerCharacter other)
    {
        partner = other;
        if (bodyCollider != null && other != null && other.BodyCollider != null)
        {
            Physics2D.IgnoreCollision(bodyCollider, other.BodyCollider, true);
        }
    }

    public void SetSpawn(Vector3 position)
    {
        spawnPosition = position;
    }

    public void ResetForFullPartyDeath()
    {
        ReviveProgress = 0f;
        HeldTool = HeldToolType.None;
        transform.position = spawnPosition;
        ReviveToAlive();
    }

    public void Kill(string reason)
    {
        if (IsDeadLike)
        {
            return;
        }

        LifeState = PlayerLifeState.Dead;
        dragging = false;
        body.linearVelocity = Vector2.zero;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        bodyCollider.isTrigger = true;
        UpdateVisualState();
        GamePrototypeManager.Instance?.NotifyPlayerDied(this);
    }

    public void ReviveToAlive()
    {
        LifeState = PlayerLifeState.Alive;
        ReviveProgress = 0f;
        body.bodyType = originalBodyType;
        body.gravityScale = originalGravityScale;
        bodyCollider.isTrigger = originalTrigger;
        body.linearVelocity = Vector2.zero;
        UpdateVisualState();
    }

    public float GetReviveNormalized()
    {
        return Mathf.Clamp01(ReviveProgress / reviveDuration);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        ApplyFrictionlessColliderMaterial();
        spriteRenderer = GetComponent<SpriteRenderer>();
        trajectoryLine = GetComponent<LineRenderer>();
        if (trajectoryLine == null)
        {
            trajectoryLine = gameObject.AddComponent<LineRenderer>();
        }
        trajectoryLine.enabled = false;
        trajectoryLine.positionCount = trajectoryPoints;
        trajectoryLine.startWidth = 0.04f;
        trajectoryLine.endWidth = 0.04f;
        trajectoryLine.useWorldSpace = true;
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.startColor = new Color(1f, 1f, 1f, 0.75f);
        trajectoryLine.endColor = new Color(1f, 1f, 1f, 0.25f);
        trajectoryLine.sortingOrder = 20;

        var pullLineObject = new GameObject("PullForceLine");
        pullLineObject.transform.SetParent(transform);
        pullLine = pullLineObject.AddComponent<LineRenderer>();
        pullLine.enabled = false;
        pullLine.positionCount = 2;
        pullLine.startWidth = pullLineWidth;
        pullLine.endWidth = pullLineWidth;
        pullLine.useWorldSpace = true;
        pullLine.material = new Material(Shader.Find("Sprites/Default"));
        pullLine.startColor = new Color(1f, 1f, 1f, 0.9f);
        pullLine.endColor = element == ElementType.Water ? new Color(0.2f, 0.65f, 1f, 0.9f) : new Color(1f, 0.35f, 0.1f, 0.9f);
        pullLine.sortingOrder = 21;
        originalGravityScale = body.gravityScale;
        originalTrigger = bodyCollider.isTrigger;
        originalBodyType = body.bodyType;
        spawnPosition = transform.position;
        ApplyElementColor();
        EnsureWaterVisualEffect();
    }

    private void EnsureWaterVisualEffect()
    {
        if (element != ElementType.Water || GetComponent<WaterSpriteWobble>() != null)
        {
            return;
        }

        gameObject.AddComponent<WaterSpriteWobble>();
    }

    private void ApplyFrictionlessColliderMaterial()
    {
        if (frictionlessMaterial == null)
        {
            frictionlessMaterial = new PhysicsMaterial2D("PlayerFrictionless");
            frictionlessMaterial.friction = 0f;
            frictionlessMaterial.bounciness = 0f;
        }

        bodyCollider.sharedMaterial = frictionlessMaterial;
    }

    private void Update()
    {
        if (!CanReceiveInput())
        {
            SetAimVisualsVisible(false);
            return;
        }

        UpdateGroundState();
        HandleJump();
        HandleProjectileInput();
        HandleReviveInput();
    }

    private void FixedUpdate()
    {
        if (!CanReceiveInput())
        {
            return;
        }

        HandleHorizontalMovement();
        ApplyHeldToolMovementModifiers();
    }

    private bool CanReceiveInput()
    {
        return LifeState == PlayerLifeState.Alive || LifeState == PlayerLifeState.ReviveCaster;
    }

    private void HandleHorizontalMovement()
    {
        float move = 0f;
        if (Input.GetKey(moveLeftKey))
        {
            move -= 1f;
        }
        if (Input.GetKey(moveRightKey))
        {
            move += 1f;
        }

        if (IsSideBlocked(move))
        {
            move = 0f;
        }

        body.linearVelocity = new Vector2(move * moveSpeed, body.linearVelocity.y);
    }

    private bool IsSideBlocked(float move)
    {
        if (Mathf.Approximately(move, 0f))
        {
            return false;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask);
        filter.useTriggers = false;

        Vector2 direction = move > 0f ? Vector2.right : Vector2.left;
        int hitCount = bodyCollider.Cast(direction, filter, sideHits, sideCastDistance);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = sideHits[i];
            if (hit.collider == null || hit.collider.isTrigger)
            {
                continue;
            }

            if (IsPlayerCollider(hit.collider))
            {
                continue;
            }

            bool blocksRight = move > 0f && hit.normal.x < -0.5f;
            bool blocksLeft = move < 0f && hit.normal.x > 0.5f;
            bool isWallLikeSide = hit.normal.y < minimumGroundNormalY;
            if ((blocksRight || blocksLeft) && isWallLikeSide)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateGroundState()
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask);
        filter.useTriggers = false;

        grounded = false;
        int hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundCastDistance);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = groundHits[i];
            if (hit.collider != null && !hit.collider.isTrigger && !IsPlayerCollider(hit.collider) && hit.normal.y >= minimumGroundNormalY)
            {
                grounded = true;
                break;
            }
        }

        if (grounded)
        {
            if (body.linearVelocity.y <= 0.01f)
            {
                jumpConsumedUntilLanding = false;
            }

            coyoteTimer = jumpConsumedUntilLanding ? 0f : coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        return other.GetComponentInParent<PlayerCharacter>() != null;
    }

    private void HandleJump()
    {
        bool jumpPressed = Input.GetKeyDown(jumpKey) || Input.GetKeyDown(alternateJumpKey);
        bool jumpReleased = Input.GetKeyUp(jumpKey) || Input.GetKeyUp(alternateJumpKey);

        if (jumpPressed && coyoteTimer > 0f && !jumpConsumedUntilLanding)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
            coyoteTimer = 0f;
            jumpConsumedUntilLanding = true;
        }

        if (jumpReleased && body.linearVelocity.y > 0f)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpCutMultiplier);
        }
    }

    private void HandleProjectileInput()
    {
        if (Time.time < nextFireTime)
        {
            SetAimVisualsVisible(false);
            return;
        }

        if (Input.GetMouseButtonDown(fireMouseButton))
        {
            dragging = true;
            dragStartScreen = Input.mousePosition;
        }

        if (dragging)
        {
            UpdateTrajectoryPreview();
        }

        if (!dragging || !Input.GetMouseButtonUp(fireMouseButton))
        {
            return;
        }

        dragging = false;
        SetAimVisualsVisible(false);
        Vector2 dragEndScreen = Input.mousePosition;
        Vector2 dragVector = dragStartScreen - dragEndScreen;
        if (dragVector.sqrMagnitude < 4f)
        {
            return;
        }

        float force = Mathf.Clamp(dragVector.magnitude * dragForceScale, projectileMinForce, projectileMaxForce);
        Vector2 direction = dragVector.normalized;
        FireProjectile(direction * force);
        nextFireTime = Time.time + projectileCooldown;
    }

    private void FireProjectile(Vector2 velocity)
    {
        Vector2 direction = velocity.sqrMagnitude > 0f ? velocity.normalized : Vector2.right;
        var projectileObject = new GameObject(playerId + "_" + element + "Projectile");
        projectileObject.layer = gameObject.layer;
        projectileObject.transform.position = (Vector2)transform.position + direction * projectileSpawnOffset;

        var renderer = projectileObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GamePrototypeManager.Instance != null ? GamePrototypeManager.Instance.ProjectileSprite : null;
        renderer.color = element == ElementType.Water ? new Color(0.2f, 0.65f, 1f, 1f) : new Color(1f, 0.35f, 0.1f, 1f);
        renderer.sortingOrder = 5;

        var projectileBody = projectileObject.AddComponent<Rigidbody2D>();
        var projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
        projectileCollider.radius = 0.16f;
        var projectile = projectileObject.AddComponent<ElementProjectile>();
        projectile.Initialize(element, this, velocity, projectileLifetime);
    }

    private void UpdateTrajectoryPreview()
    {
        Vector2 dragVector = dragStartScreen - (Vector2)Input.mousePosition;
        if (dragVector.sqrMagnitude < 4f)
        {
            SetAimVisualsVisible(false);
            return;
        }

        float force = Mathf.Clamp(dragVector.magnitude * dragForceScale, projectileMinForce, projectileMaxForce);
        Vector2 initialVelocity = dragVector.normalized * force;
        Vector2 start = (Vector2)transform.position + initialVelocity.normalized * projectileSpawnOffset;
        Vector2 gravity = Physics2D.gravity;

        SetAimVisualsVisible(true);
        UpdatePullLine(initialVelocity.normalized, force);
        for (int i = 0; i < trajectoryPoints; i++)
        {
            float time = i * trajectoryStep;
            Vector2 point = start + initialVelocity * time + 0.5f * gravity * time * time;
            trajectoryLine.SetPosition(i, point);
        }
    }

    private void UpdatePullLine(Vector2 fireDirection, float force)
    {
        if (pullLine == null)
        {
            return;
        }

        Vector2 center = transform.position;
        float forceRatio = Mathf.InverseLerp(projectileMinForce, projectileMaxForce, force);
        float length = Mathf.Lerp(pullLineMinLength, pullLineMaxLength, forceRatio);
        Vector2 pullDirection = -fireDirection;
        pullLine.SetPosition(0, center);
        pullLine.SetPosition(1, center + pullDirection * length);
    }

    private void SetAimVisualsVisible(bool visible)
    {
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = visible;
        }
        if (pullLine != null)
        {
            pullLine.enabled = visible;
        }
    }

    private void HandleReviveInput()
    {
        if (partner == null || !partner.IsDeadLike)
        {
            if (LifeState == PlayerLifeState.ReviveCaster)
            {
                LifeState = PlayerLifeState.Alive;
                UpdateVisualState();
            }
            return;
        }

        float distance = Vector2.Distance(transform.position, partner.transform.position);
        bool canRevive = distance <= reviveRange && Input.GetKey(interactKey);
        if (!canRevive)
        {
            if (LifeState == PlayerLifeState.ReviveCaster)
            {
                LifeState = PlayerLifeState.Alive;
                UpdateVisualState();
            }
            if (partner.LifeState == PlayerLifeState.ReviveTarget)
            {
                partner.LifeState = PlayerLifeState.Dead;
                partner.UpdateVisualState();
            }
            return;
        }

        LifeState = PlayerLifeState.ReviveCaster;
        partner.LifeState = PlayerLifeState.ReviveTarget;
        partner.ReviveProgress += Time.deltaTime;
        UpdateVisualState();
        partner.UpdateVisualState();

        if (partner.ReviveProgress >= reviveDuration)
        {
            LifeState = PlayerLifeState.Alive;
            partner.ReviveToAlive();
            UpdateVisualState();
        }
    }

    private void ApplyHeldToolMovementModifiers()
    {
        if (HeldTool != HeldToolType.Umbrella || body.linearVelocity.y >= 0f)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, -3.5f));
    }

    private void ApplyElementColor()
    {
        aliveColor = element == ElementType.Water ? new Color(0.25f, 0.75f, 1f, 1f) : new Color(1f, 0.28f, 0.1f, 1f);
        ApplyGlowLayer();
        UpdateVisualState();
    }

    private void ApplyGlowLayer()
    {
        int glowLayer = LayerMask.NameToLayer("Glow");
        if (glowLayer >= 0)
        {
            gameObject.layer = glowLayer;
        }
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (LifeState == PlayerLifeState.Dead)
        {
            spriteRenderer.color = deadColor;
            transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            return;
        }

        if (LifeState == PlayerLifeState.ReviveCaster || LifeState == PlayerLifeState.ReviveTarget)
        {
            spriteRenderer.color = revivingColor;
            return;
        }

        spriteRenderer.color = aliveColor;
        transform.rotation = Quaternion.identity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 center = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        Gizmos.DrawWireCube(center, groundCheckSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reviveRange);
    }
}
