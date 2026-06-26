using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerCharacter : MonoBehaviour
{
    private const string AimLineShaderName = "WaterAndFire/AimPreviewUnlit";
    private const int AimLineSortingOffset = -1;
    private static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");

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
    private Material aimLineMaterial;
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
    private int originalLayer;
    private int aimVisualLayer;
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
        EnsureElementVisualEffect();
    }

    public void SetPartner(PlayerCharacter other)
    {
        partner = other;
        ApplyPartnerCollisionIgnore();
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
        ApplyPartnerCollisionIgnore();
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
        ApplyPartnerCollisionIgnore();
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
        SetupAimVisuals();
        originalGravityScale = body.gravityScale;
        originalTrigger = bodyCollider.isTrigger;
        originalBodyType = body.bodyType;
        originalLayer = gameObject.layer;
        spawnPosition = transform.position;
        ApplyElementColor();
        EnsureElementVisualEffect();
    }

    private void SetupAimVisuals()
    {
        LineRenderer legacyLine = GetComponent<LineRenderer>();
        if (legacyLine != null)
        {
            legacyLine.enabled = false;
            Destroy(legacyLine);
        }

        aimVisualLayer = LayerMask.NameToLayer("Default");
        Shader lineShader = Shader.Find(AimLineShaderName);
        if (lineShader == null)
        {
            lineShader = Shader.Find("Sprites/Default");
        }

        if (lineShader != null)
        {
            aimLineMaterial = new Material(lineShader)
            {
                name = "Runtime_AimLine"
            };
            if (aimLineMaterial.HasProperty(MaxBrightnessId))
            {
                aimLineMaterial.SetFloat(MaxBrightnessId, 0.24f);
            }
        }

        trajectoryLine = CreateAimLineRenderer("TrajectoryLine", 20);
        trajectoryLine.enabled = false;
        trajectoryLine.positionCount = trajectoryPoints;
        trajectoryLine.startWidth = 0.04f;
        trajectoryLine.endWidth = 0.04f;
        trajectoryLine.startColor = new Color(0.55f, 0.55f, 0.55f, 0.46f);
        trajectoryLine.endColor = new Color(0.45f, 0.45f, 0.45f, 0.12f);

        pullLine = CreateAimLineRenderer("PullForceLine", 21);
        pullLine.enabled = false;
        pullLine.positionCount = 2;
        pullLine.startWidth = pullLineWidth;
        pullLine.endWidth = pullLineWidth;
        pullLine.startColor = new Color(0.55f, 0.55f, 0.55f, 0.52f);
        pullLine.endColor = new Color(0.38f, 0.38f, 0.38f, 0.52f);
    }

    private LineRenderer CreateAimLineRenderer(string objectName, int sortingOrder)
    {
        var lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        lineObject.layer = aimVisualLayer >= 0 ? aimVisualLayer : 0;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sortingOrder = GetAimLineSortingOrder(sortingOrder);
        if (spriteRenderer != null)
        {
            line.sortingLayerID = spriteRenderer.sortingLayerID;
        }
        if (aimLineMaterial != null)
        {
            line.sharedMaterial = aimLineMaterial;
        }

        return line;
    }

    private void EnsureElementVisualEffect()
    {
        if (element == ElementType.Water)
        {
            RemoveComponentIfPresent<FireSpriteFlame>();
            RemoveComponentIfPresent<FireSpriteEdgeParticles>();
            EnsureComponent<WaterSpriteWobble>();
            return;
        }

        if (element == ElementType.Fire)
        {
            RemoveComponentIfPresent<WaterSpriteWobble>();
            EnsureComponent<FireSpriteFlame>();
            EnsureComponent<FireSpriteEdgeParticles>();
        }
    }

    private void EnsureComponent<T>() where T : Component
    {
        if (!TryGetComponent<T>(out _))
        {
            gameObject.AddComponent<T>();
        }
    }

    private void RemoveComponentIfPresent<T>() where T : Component
    {
        if (!TryGetComponent<T>(out T effect))
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(effect);
        }
        else
        {
            DestroyImmediate(effect);
        }
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

    private void ApplyPartnerCollisionIgnore()
    {
        if (bodyCollider == null || partner == null || partner.BodyCollider == null)
        {
            return;
        }

        Physics2D.IgnoreCollision(bodyCollider, partner.BodyCollider, true);
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

        projectileObject.AddComponent<Rigidbody2D>();
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
        EnsureAimVisualLayers();

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = visible;
        }
        if (pullLine != null)
        {
            pullLine.enabled = visible;
        }
    }

    private void EnsureAimVisualLayers()
    {
        int targetLayer = aimVisualLayer >= 0 ? aimVisualLayer : 0;
        if (trajectoryLine != null)
        {
            trajectoryLine.gameObject.layer = targetLayer;
            SyncAimLineSorting(trajectoryLine, 20);
        }

        if (pullLine != null)
        {
            pullLine.gameObject.layer = targetLayer;
            SyncAimLineSorting(pullLine, 21);
        }
    }

    private void SyncAimLineSorting(LineRenderer line, int fallbackSortingOrder)
    {
        if (line == null)
        {
            return;
        }

        line.sortingOrder = GetAimLineSortingOrder(fallbackSortingOrder);
        if (spriteRenderer != null)
        {
            line.sortingLayerID = spriteRenderer.sortingLayerID;
        }
    }

    private int GetAimLineSortingOrder(int fallbackSortingOrder)
    {
        if (spriteRenderer == null)
        {
            return fallbackSortingOrder;
        }

        return spriteRenderer.sortingOrder + AimLineSortingOffset;
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
        if (element == ElementType.Water || element == ElementType.Fire)
        {
            if (glowLayer >= 0)
            {
                gameObject.layer = glowLayer;
            }

            EnsureAimVisualLayers();
            return;
        }

        int defaultLayer = LayerMask.NameToLayer("Default");
        gameObject.layer = originalLayer == glowLayer && defaultLayer >= 0 ? defaultLayer : originalLayer;
        EnsureAimVisualLayers();
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        transform.rotation = Quaternion.identity;

        if (LifeState == PlayerLifeState.Dead)
        {
            spriteRenderer.color = deadColor;
            return;
        }

        if (LifeState == PlayerLifeState.ReviveCaster || LifeState == PlayerLifeState.ReviveTarget)
        {
            spriteRenderer.color = revivingColor;
            return;
        }

        spriteRenderer.color = aliveColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 center = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        Gizmos.DrawWireCube(center, groundCheckSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reviveRange);
    }

    private void OnDestroy()
    {
        if (aimLineMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(aimLineMaterial);
        }
        else
        {
            DestroyImmediate(aimLineMaterial);
        }
    }
}
