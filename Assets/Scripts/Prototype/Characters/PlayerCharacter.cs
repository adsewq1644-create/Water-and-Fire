using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerCharacter : MonoBehaviour
{
    private const string AimLineShaderName = "WaterAndFire/AimPreviewUnlit";
    private const int AimLineSortingOffset = -1;
    private static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");
    private static readonly Color GroundCastGizmoColor = new Color(1f, 0.85f, 0.05f, 0.85f);
    private static readonly Color ReviveRangeGizmoColor = new Color(0.2f, 1f, 0.25f, 0.8f);
    private static readonly Color ProjectileSpawnGizmoColor = new Color(0.25f, 0.55f, 1f, 0.85f);
    private static readonly Color ProjectileVisualGizmoColor = new Color(0.35f, 0.85f, 1f, 0.7f);
    private static readonly Color ProjectileColliderGizmoColor = new Color(1f, 0.85f, 0.1f, 0.9f);
    private static readonly Color ShockwaveGizmoColor = new Color(1f, 0.92f, 0.35f, 0.5f);
    private const float BouncePlatformGroundIgnoreTime = 0.1f;

    [Header("Identity")]
    [SerializeField] private string playerId = "Player";
    [SerializeField] private ElementType element = ElementType.Water;

    [Header("Input")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode chargeCancelKey = KeyCode.S;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private int fireMouseButton = 0;

    [Header("Fall Rescue")]
    [SerializeField] private bool enableFallRescueInput = true;
    [SerializeField] private KeyCode rescueRequestKey = KeyCode.F;
    [SerializeField] private string stableGroundTag = "SafeGround";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.18f;
    [SerializeField] private float groundAcceleration = 45f;
    [SerializeField] private float groundDeceleration = 55f;
    [SerializeField] private float airAcceleration = 12f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCastDistance = 0.08f;
    [SerializeField] private float sideCastDistance = 0.08f;
    [SerializeField] private float minimumGroundNormalY = 0.65f;

    [Header("Charge Jump")]
    [SerializeField] private float maxChargeTime = 0.7f;
    [SerializeField] private float minJumpPower = 7f;
    [SerializeField] private float maxJumpPower = 14f;
    [SerializeField] private float horizontalJumpPower = 5f;
    [SerializeField, Range(0f, 1f)] private float minHorizontalChargeMultiplier = 0.45f;
    [SerializeField, Range(0f, 1f)] private float chargeMoveSpeedMultiplier = 0.35f;

    [Header("Jump Feel")]
    [SerializeField] private float jumpMotionSpeedMultiplier = 1.25f;
    [SerializeField] private bool preserveJumpArcWhileReducingAirTime = true;

    [Header("Full Charge Feel")]
    [SerializeField, Range(0f, 1f)] private float fullChargeThreshold = 0.95f;
    [SerializeField, Range(0.05f, 1f)] private float fullChargeGravityMultiplier = 0.8f;
    [SerializeField, Range(0.05f, 1f)] private float fullChargeAirSlowMultiplier = 0.9f;

    [Header("Dive")]
    [SerializeField] private float diveSpeed = 18f;
    [SerializeField] private float diveLandingStun = 0.12f;
    [SerializeField] private float shockwaveRadius = 3f;
    [SerializeField] private float shockwaveDuration = 0.25f;
    [SerializeField] private bool shockwaveDelayByDistance = true;
    [SerializeField] private LayerMask shockwaveMask = ~0;

    [Header("Down Slam Bounce Lock")]
    [SerializeField] private bool useApexPercentDownSlamLock = true;
    [SerializeField, Range(0f, 1f)] private float unlockAtApexTimePercent = 0.35f;
    [SerializeField] private float minDownSlamBounceLockTime = 0.12f;
    [SerializeField] private float maxDownSlamBounceLockTime = 0.35f;
    [SerializeField] private bool debugDownSlamBounceLock;

    [Header("Combat")]
    [SerializeField] private float projectileCooldown = 2f;
    [SerializeField] private float projectileMaxForce = 14f;
    [SerializeField] private float projectileMinForce = 3f;
    [SerializeField] private float projectileLifetime = 6f;
    [SerializeField] private float dragForceScale = 0.08f;
    [SerializeField] private float projectileSpawnOffset = 0.55f;
    [SerializeField] private float projectileVisualScale = 1f;
    [SerializeField] private float projectileColliderRadius = 0.16f;
    [SerializeField] private float projectileSpeedMultiplier = 1f;
    [SerializeField] private float projectileMaxUpwardVelocity = 0f;
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

    [Header("Debug")]
    [SerializeField] private bool showChargeDebug = true;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private LineRenderer trajectoryLine;
    private LineRenderer pullLine;
    private Material aimLineMaterial;
    private PlayerCharacter partner;
    private Vector3 spawnPosition;
    private Vector3 lastSafePosition;
    private bool hasLastSafePosition;
    private float coyoteTimer;
    private bool grounded;
    private bool jumpConsumedUntilLanding;
    private bool isChargingJump;
    private bool stationaryJumpCharge;
    private float jumpChargeTimer;
    private bool jumpInputReleasedAfterLaunch = true;
    private bool diveUsed;
    private bool isDiving;
    private bool fullChargeJumpActive;
    private float diveStartY;
    private float lastDiveFallDistance;
    private float diveLandingStunTimer;
    private bool suppressDiveLandingStunThisImpact;
    private float bouncePlatformGroundIgnoreTimer;
    private float downSlamBounceLockTimer;
    private float lastDownSlamBounceLockDuration;
    private bool leafBounceCurveActive;
    private float leafBounceCurveTimer;
    private float leafBounceCurveDuration;
    private float leafBounceStartVelocityX;
    private float leafBounceTargetVelocityX;
    private bool leafBounceAirControlLocked;
    private RaycastHit2D lastGroundHit;
    private float slipperyExitCarryTimer;
    private Vector2 slipperyExitCarryVelocity;
    private float nextFireTime;
    private bool dragging;
    private bool externalInputLocked;
    private float externalKnockbackInputLockTimer;
    private Vector2 dragStartScreen;
    private float originalGravityScale;
    private bool originalTrigger;
    private RigidbodyType2D originalBodyType;
    private int originalLayer;
    private int aimVisualLayer;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[6];
    private readonly RaycastHit2D[] sideHits = new RaycastHit2D[6];
    private readonly RaycastHit2D[] projectileSpawnHits = new RaycastHit2D[8];
    private static PhysicsMaterial2D frictionlessMaterial;

    public string PlayerId => playerId;
    public ElementType Element => element;
    public PlayerLifeState LifeState { get; private set; } = PlayerLifeState.Alive;
    public HeldToolType HeldTool { get; private set; } = HeldToolType.None;
    public float ReviveProgress { get; private set; }
    public Collider2D BodyCollider => bodyCollider;
    public bool IsAliveLike => LifeState == PlayerLifeState.Alive || LifeState == PlayerLifeState.ReviveCaster;
    public bool IsDeadLike => LifeState == PlayerLifeState.Dead || LifeState == PlayerLifeState.ReviveTarget;
    public bool IsChargingJump => isChargingJump;
    public bool IsDiving => isDiving;
    public bool IsDiveBounceGroundIgnored => bouncePlatformGroundIgnoreTimer > 0f;
    public bool IsGrounded => grounded;
    public bool IsOnStableGround => grounded && IsStableGroundCollider(lastGroundHit.collider);
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
    public bool HasLastSafePosition => hasLastSafePosition;
    public Vector3 LastSafePosition => hasLastSafePosition ? lastSafePosition : spawnPosition;
    public float LastDiveFallDistance => lastDiveFallDistance;
    public float CurrentMoveInput => GetMoveInput();
    public KeyCode RescueRequestKey => rescueRequestKey;
    public float JumpChargeNormalized => maxChargeTime <= 0f ? 1f : Mathf.Clamp01(jumpChargeTimer / maxChargeTime);
    public int JumpChargeStep => Mathf.Clamp(Mathf.FloorToInt(JumpChargeNormalized * 3f) + 1, 1, 3);

    public void SetInputLocked(bool locked)
    {
        externalInputLocked = locked;
        if (!locked)
        {
            return;
        }

        dragging = false;
        SetAimVisualsVisible(false);
    }

    public void SuppressDiveLandingStunThisImpact()
    {
        suppressDiveLandingStunThisImpact = true;
    }

    public void ApplyBouncePlatformVelocity(Vector2 velocity)
    {
        ApplyDiveBounce(velocity);
    }

    public void ApplyDiveBounce(Vector2 velocity)
    {
        leafBounceCurveActive = false;
        leafBounceAirControlLocked = false;
        isChargingJump = false;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        isDiving = false;
        fullChargeJumpActive = false;
        RestoreDefaultGravity();
        diveLandingStunTimer = 0f;
        grounded = false;
        lastGroundHit = default;
        coyoteTimer = 0f;
        jumpConsumedUntilLanding = true;
        jumpInputReleasedAfterLaunch = false;
        diveUsed = false;
        bouncePlatformGroundIgnoreTimer = Mathf.Max(bouncePlatformGroundIgnoreTimer, BouncePlatformGroundIgnoreTime);
        StartDownSlamBounceLock(velocity.y);

        if (body != null)
        {
            body.linearVelocity = velocity;
        }
    }

    public void ApplyExternalKnockback(Vector2 velocity, float inputLockDuration)
    {
        if (!IsAliveLike || body == null)
        {
            return;
        }

        leafBounceCurveActive = false;
        leafBounceAirControlLocked = false;
        isChargingJump = false;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        isDiving = false;
        diveUsed = false;
        fullChargeJumpActive = false;
        suppressDiveLandingStunThisImpact = false;
        dragging = false;
        SetAimVisualsVisible(false);
        RestoreDefaultGravity();
        diveLandingStunTimer = 0f;
        grounded = false;
        lastGroundHit = default;
        coyoteTimer = 0f;
        bouncePlatformGroundIgnoreTimer = Mathf.Max(bouncePlatformGroundIgnoreTimer, BouncePlatformGroundIgnoreTime);
        ClearDownSlamBounceLock();
        jumpConsumedUntilLanding = true;
        jumpInputReleasedAfterLaunch = false;
        externalKnockbackInputLockTimer = Mathf.Max(externalKnockbackInputLockTimer, Mathf.Max(0f, inputLockDuration));
        body.linearVelocity = velocity;
    }

    public void ApplyLeafDiveBounce(
        Vector2 initialVelocity,
        float targetHorizontalVelocity,
        float horizontalBuildDuration)
    {
        ApplyDiveBounce(initialVelocity);

        leafBounceStartVelocityX = initialVelocity.x;
        leafBounceTargetVelocityX = targetHorizontalVelocity;
        leafBounceCurveDuration = Mathf.Max(0f, horizontalBuildDuration);
        leafBounceCurveTimer = 0f;
        leafBounceAirControlLocked = true;
        leafBounceCurveActive = leafBounceCurveDuration > 0f &&
            !Mathf.Approximately(leafBounceStartVelocityX, leafBounceTargetVelocityX);

        if (!leafBounceCurveActive && body != null)
        {
            body.linearVelocity = new Vector2(leafBounceTargetVelocityX, body.linearVelocity.y);
        }
    }

    public void Configure(string id, ElementType characterElement, KeyCode left, KeyCode right, KeyCode jump, KeyCode alternateJump, KeyCode interact, int mouseButton)
    {
        playerId = id;
        element = characterElement;
        moveLeftKey = left;
        moveRightKey = right;
        jumpKey = KeyCode.Space;
        interactKey = interact;
        fireMouseButton = mouseButton;
        ApplyElementColor();
        EnsureElementVisualEffect();
    }

    public void SetElementForTesting(ElementType characterElement, string testPlayerId = null)
    {
        element = characterElement;
        if (!string.IsNullOrWhiteSpace(testPlayerId))
        {
            playerId = testPlayerId;
        }

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
        lastSafePosition = position;
        hasLastSafePosition = true;
    }

    public void ReturnToStartForTesting()
    {
        dragging = false;
        SetAimVisualsVisible(false);
        HeldTool = HeldToolType.None;
        ReviveToAlive();
        TeleportForFallRescue(spawnPosition);
    }

    public void TeleportForFallRescue(Vector3 position, bool rememberAsSafePosition = true)
    {
        transform.position = position;
        ResetJumpActionState();

        if (body != null)
        {
            body.bodyType = originalBodyType;
            body.gravityScale = originalGravityScale;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = originalTrigger;
        }

        grounded = false;
        lastGroundHit = default;
        if (rememberAsSafePosition)
        {
            lastSafePosition = position;
            hasLastSafePosition = true;
        }

        ApplyPartnerCollisionIgnore();
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

        externalInputLocked = false;
        LifeState = PlayerLifeState.Dead;
        dragging = false;
        ResetJumpActionState();
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
        externalInputLocked = false;
        LifeState = PlayerLifeState.Alive;
        ReviveProgress = 0f;
        ResetJumpActionState();
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
        jumpKey = KeyCode.Space;
        ApplyFrictionlessColliderMaterial();
        spriteRenderer = GetComponent<SpriteRenderer>();
        SetupAimVisuals();
        originalGravityScale = body.gravityScale;
        originalTrigger = bodyCollider.isTrigger;
        originalBodyType = body.bodyType;
        originalLayer = gameObject.layer;
        spawnPosition = transform.position;
        lastSafePosition = spawnPosition;
        hasLastSafePosition = true;
        ApplyElementColor();
        EnsureElementVisualEffect();
    }

    private void SetupAimVisuals()
    {
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
            EnsureComponent<WaterSpiritShaderController>();
            return;
        }

        if (element == ElementType.Fire)
        {
            RemoveComponentIfPresent<WaterSpiritShaderController>();
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
        if (externalKnockbackInputLockTimer > 0f)
        {
            externalKnockbackInputLockTimer = Mathf.Max(0f, externalKnockbackInputLockTimer - Time.deltaTime);
        }

        UpdateDownSlamBounceLock();

        if (!CanReceiveInput())
        {
            SetAimVisualsVisible(false);
            return;
        }

        UpdateDiveLandingStun();
        UpdateGroundState();
        HandleFallRescueInput();
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
        UpdateSlipperyExitCarryTimer();
        UpdateLeafBounceCurve();
    }

    private void UpdateLeafBounceCurve()
    {
        if (!leafBounceCurveActive || body == null)
        {
            return;
        }

        float direction = Mathf.Sign(leafBounceTargetVelocityX);
        if (grounded || isDiving || (!Mathf.Approximately(direction, 0f) && IsSideBlocked(direction)))
        {
            leafBounceCurveActive = false;
            return;
        }

        leafBounceCurveTimer += Time.fixedDeltaTime;
        float normalizedTime = leafBounceCurveDuration <= 0f
            ? 1f
            : Mathf.Clamp01(leafBounceCurveTimer / leafBounceCurveDuration);
        float curvedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
        float velocityX = Mathf.Lerp(leafBounceStartVelocityX, leafBounceTargetVelocityX, curvedTime);
        body.linearVelocity = new Vector2(velocityX, body.linearVelocity.y);

        if (normalizedTime >= 1f)
        {
            leafBounceCurveActive = false;
        }
    }

    private bool CanReceiveInput()
    {
        return !externalInputLocked && externalKnockbackInputLockTimer <= 0f &&
            (LifeState == PlayerLifeState.Alive || LifeState == PlayerLifeState.ReviveCaster);
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
        if (isDiving)
        {
            body.linearVelocity = new Vector2(0f, -Mathf.Abs(diveSpeed));
            return;
        }

        if (diveLandingStunTimer > 0f)
        {
            float stoppedX = Mathf.MoveTowards(body.linearVelocity.x, 0f, groundDeceleration * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(stoppedX, body.linearVelocity.y);
            return;
        }

        if (isChargingJump)
        {
            float chargeMove = GetMoveInput();
            if (stationaryJumpCharge)
            {
                chargeMove = 0f;
            }

            if (IsSideBlocked(chargeMove))
            {
                chargeMove = 0f;
            }

            if (TryApplyGroundSlipperyMovement(chargeMove))
            {
                return;
            }

            float targetX = chargeMove * moveSpeed * chargeMoveSpeedMultiplier;
            float acceleration = Mathf.Approximately(chargeMove, 0f) ? groundDeceleration : groundAcceleration;
            float nextX = Mathf.MoveTowards(body.linearVelocity.x, targetX, acceleration * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(nextX, body.linearVelocity.y);
            return;
        }

        float move = GetMoveInput();
        float currentX = body.linearVelocity.x;

        if (grounded)
        {
            if (IsSideBlocked(move))
            {
                move = 0f;
            }

            if (TryApplyGroundSlipperyMovement(move))
            {
                return;
            }

            float targetX = move * moveSpeed;
            float acceleration = Mathf.Approximately(move, 0f) ? groundDeceleration : groundAcceleration;
            float nextX = Mathf.MoveTowards(currentX, targetX, acceleration * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(nextX, body.linearVelocity.y);
            return;
        }

        if (leafBounceAirControlLocked)
        {
            return;
        }

        bool preserveSlipperyExit = IsSlipperyExitCarryActive();
        if (preserveSlipperyExit)
        {
            currentX = PreserveSlipperyExitHorizontalVelocity(currentX);
        }

        if (!Mathf.Approximately(move, 0f) && !IsSideBlocked(move))
        {
            float fullChargeAirMultiplier = fullChargeJumpActive ? fullChargeAirSlowMultiplier : 1f;
            float targetX = move * moveSpeed * fullChargeAirMultiplier;
            float maxDelta = airAcceleration * airControlMultiplier * fullChargeAirMultiplier * Time.fixedDeltaTime;
            currentX = Mathf.MoveTowards(currentX, targetX, maxDelta);
        }

        body.linearVelocity = new Vector2(currentX, body.linearVelocity.y);
    }

    private bool TryApplyGroundSlipperyMovement(float move)
    {
        SlipperySurface2D slippery = FindSlipperySurface(lastGroundHit.collider);
        if (slippery == null || !slippery.TryGetGroundSlideDirection(lastGroundHit.normal, out Vector2 slideDirection))
        {
            return false;
        }

        Vector2 targetVelocity = slideDirection * slippery.MaxGroundSlideSpeed;
        if (!Mathf.Approximately(move, 0f) && slippery.GroundInputControl > 0f)
        {
            targetVelocity.x += move * moveSpeed * slippery.GroundInputControl;
        }

        Vector2 nextVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            targetVelocity,
            slippery.GroundSlideAcceleration * Time.fixedDeltaTime);

        body.linearVelocity = nextVelocity;
        RememberSlipperyExitCarry(slippery, nextVelocity);
        return true;
    }

    private void RememberSlipperyExitCarry(SlipperySurface2D slippery, Vector2 velocity)
    {
        if (slippery == null || slippery.ExitCarryTime <= 0f)
        {
            return;
        }

        slipperyExitCarryTimer = slippery.ExitCarryTime;
        slipperyExitCarryVelocity = velocity;
    }

    private bool IsSlipperyExitCarryActive()
    {
        return slipperyExitCarryTimer > 0f && !grounded;
    }

    private float PreserveSlipperyExitHorizontalVelocity(float currentX)
    {
        if (Mathf.Abs(slipperyExitCarryVelocity.x) <= Mathf.Abs(currentX))
        {
            return currentX;
        }

        return slipperyExitCarryVelocity.x;
    }

    private void UpdateSlipperyExitCarryTimer()
    {
        if (grounded)
        {
            slipperyExitCarryTimer = 0f;
            slipperyExitCarryVelocity = Vector2.zero;
            return;
        }

        if (slipperyExitCarryTimer > 0f)
        {
            slipperyExitCarryTimer = Mathf.Max(0f, slipperyExitCarryTimer - Time.fixedDeltaTime);
        }
    }

    private float GetMoveInput()
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

        return Mathf.Clamp(move, -1f, 1f);
    }

    private void HandleFallRescueInput()
    {
        if (!enableFallRescueInput || rescueRequestKey == KeyCode.None || !Input.GetKeyDown(rescueRequestKey))
        {
            return;
        }

        FallRescueManager rescueManager = FindFirstObjectByType<FallRescueManager>();
        if (rescueManager != null)
        {
            rescueManager.RequestRescue(this);
        }
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
        if (bouncePlatformGroundIgnoreTimer > 0f)
        {
            bouncePlatformGroundIgnoreTimer = Mathf.Max(0f, bouncePlatformGroundIgnoreTimer - Time.deltaTime);
            grounded = false;
            lastGroundHit = default;
            coyoteTimer = 0f;
            return;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask);
        filter.useTriggers = false;

        bool wasGrounded = grounded;
        grounded = false;
        lastGroundHit = default;
        int hitCount = bodyCollider.Cast(Vector2.down, filter, groundHits, groundCastDistance);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = groundHits[i];
            if (hit.collider != null && !hit.collider.isTrigger && !IsPlayerCollider(hit.collider) && hit.normal.y >= minimumGroundNormalY)
            {
                grounded = true;
                lastGroundHit = hit;
                break;
            }
        }

        if (!wasGrounded && grounded && body.linearVelocity.y <= 0.01f)
        {
            HandleLanding(lastGroundHit);
        }

        if (grounded)
        {
            if (body.linearVelocity.y <= 0.01f && !isDiving)
            {
                jumpConsumedUntilLanding = false;
                diveUsed = false;
                jumpInputReleasedAfterLaunch = true;
            }

            coyoteTimer = jumpConsumedUntilLanding ? 0f : coyoteTime;
            RememberCurrentSafePosition();
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

    private void RememberCurrentSafePosition()
    {
        if (!IsAliveLike || isDiving || !IsOnStableGround)
        {
            return;
        }

        lastSafePosition = transform.position;
        hasLastSafePosition = true;
    }

    private bool IsStableGroundCollider(Collider2D groundCollider)
    {
        if (groundCollider == null || groundCollider.isTrigger)
        {
            return false;
        }

        SlipperySurface2D slippery = FindSlipperySurface(groundCollider);
        if (slippery != null && slippery.PreventSafeGround)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(stableGroundTag))
        {
            return true;
        }

        Transform current = groundCollider.transform;
        while (current != null)
        {
            if (current.CompareTag(stableGroundTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private SlipperySurface2D FindSlipperySurface(Collider2D source)
    {
        return source != null ? source.GetComponentInParent<SlipperySurface2D>() : null;
    }

    private void HandleJump()
    {
        bool jumpPressed = Input.GetKeyDown(jumpKey);
        bool jumpReleased = Input.GetKeyUp(jumpKey);
        bool jumpHeld = Input.GetKey(jumpKey);

        if (isDiving || diveLandingStunTimer > 0f)
        {
            return;
        }

        if (isChargingJump)
        {
            if (Input.GetKeyDown(chargeCancelKey))
            {
                CancelJumpCharge();
                return;
            }

            jumpChargeTimer = Mathf.Min(jumpChargeTimer + Time.deltaTime, Mathf.Max(0f, maxChargeTime));
            if (jumpReleased && !jumpHeld)
            {
                LaunchChargedJump();
            }
            return;
        }

        if (grounded || coyoteTimer > 0f)
        {
            if (jumpPressed && !jumpConsumedUntilLanding)
            {
                StartJumpCharge();
            }
            return;
        }

        if (!jumpHeld)
        {
            jumpInputReleasedAfterLaunch = true;
        }

        if (jumpPressed && jumpInputReleasedAfterLaunch && !diveUsed && downSlamBounceLockTimer <= 0f)
        {
            StartDive();
        }
    }

    private void StartJumpCharge()
    {
        float initialMoveInput = GetMoveInput();
        isChargingJump = true;
        stationaryJumpCharge = Mathf.Approximately(initialMoveInput, 0f);
        jumpChargeTimer = 0f;
        isDiving = false;
        float chargeStartX = stationaryJumpCharge ? 0f : body.linearVelocity.x;
        body.linearVelocity = new Vector2(chargeStartX, Mathf.Min(0f, body.linearVelocity.y));
    }

    private void CancelJumpCharge()
    {
        isChargingJump = false;
        bool wasStationaryCharge = stationaryJumpCharge;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        fullChargeJumpActive = false;
        if (wasStationaryCharge)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private void LaunchChargedJump()
    {
        float charge = JumpChargeNormalized;
        float verticalVelocity = Mathf.Lerp(minJumpPower, maxJumpPower, charge);
        float horizontalCharge = Mathf.Lerp(minHorizontalChargeMultiplier, 1f, charge);
        float direction = GetMoveInput();
        bool fullChargeJump = charge >= fullChargeThreshold;
        float trajectorySpeedScale = fullChargeJump ? GetFullChargeTrajectorySpeedScale() : 1f;
        float horizontalVelocity = Mathf.Approximately(direction, 0f)
            ? 0f
            : Mathf.Sign(direction) * horizontalJumpPower * horizontalCharge;
        if (fullChargeJump)
        {
            horizontalVelocity *= trajectorySpeedScale;
            verticalVelocity *= trajectorySpeedScale;
        }

        float jumpMotionScale = GetJumpMotionVelocityScale();
        horizontalVelocity *= jumpMotionScale;
        verticalVelocity *= jumpMotionScale;

        isChargingJump = false;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        jumpConsumedUntilLanding = true;
        jumpInputReleasedAfterLaunch = false;
        diveUsed = false;
        fullChargeJumpActive = fullChargeJump;
        ApplyJumpGravityForCurrentJump();
        coyoteTimer = 0f;
        grounded = false;
        body.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);
    }

    private void StartDive()
    {
        leafBounceCurveActive = false;
        isChargingJump = false;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        isDiving = true;
        diveUsed = true;
        fullChargeJumpActive = false;
        RestoreDefaultGravity();
        jumpInputReleasedAfterLaunch = false;
        ClearDownSlamBounceLock();
        diveStartY = transform.position.y;
        lastDiveFallDistance = 0f;
        body.linearVelocity = new Vector2(0f, -Mathf.Abs(diveSpeed));
    }

    private void HandleLanding(RaycastHit2D groundHit)
    {
        leafBounceAirControlLocked = false;
        bool landedFromDive = isDiving;
        if (landedFromDive)
        {
            CompleteDiveLanding(groundHit);
        }

        isChargingJump = false;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        isDiving = false;
        fullChargeJumpActive = false;
        RestoreDefaultGravity();
        diveUsed = false;
        jumpConsumedUntilLanding = false;
        jumpInputReleasedAfterLaunch = true;
    }

    private void CompleteDiveLanding(RaycastHit2D groundHit)
    {
        Vector2 impactPoint = GetImpactPoint(groundHit);
        lastDiveFallDistance = Mathf.Max(0f, diveStartY - impactPoint.y);
        DispatchDiveImpact(groundHit.collider, impactPoint);
        DispatchShockwave(impactPoint);
        CreateShockwaveVisual(impactPoint);

        bool suppressStun = suppressDiveLandingStunThisImpact;
        suppressDiveLandingStunThisImpact = false;

        if (!suppressStun && body.linearVelocity.y <= 0.1f)
        {
            diveLandingStunTimer = Mathf.Max(diveLandingStunTimer, diveLandingStun);
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private Vector2 GetImpactPoint(RaycastHit2D groundHit)
    {
        if (groundHit.collider != null)
        {
            return groundHit.point;
        }

        Bounds bounds = bodyCollider.bounds;
        return new Vector2(bounds.center.x, bounds.min.y);
    }

    private void DispatchDiveImpact(Collider2D hitCollider, Vector2 impactPoint)
    {
        if (hitCollider == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDiveImpactReceiver receiver)
            {
                receiver.OnDiveImpact(impactPoint, gameObject);
            }
        }
    }

    private void DispatchShockwave(Vector2 origin)
    {
        if (shockwaveRadius <= 0f)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, shockwaveRadius, shockwaveMask);
        var invokedReceivers = new HashSet<MonoBehaviour>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == bodyCollider || IsPlayerCollider(hit))
            {
                continue;
            }

            Vector2 closestPoint = hit.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, closestPoint);
            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>();
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null || !invokedReceivers.Add(behaviour))
                {
                    continue;
                }

                if (behaviour is IShockwaveContextReceiver contextReceiver)
                {
                    var context = new ShockwaveContext(origin, this, shockwaveRadius, distance);
                    if (shockwaveDelayByDistance && shockwaveRadius > 0f)
                    {
                        float delay = Mathf.Clamp01(distance / shockwaveRadius) * Mathf.Max(0f, shockwaveDuration);
                        StartCoroutine(DispatchShockwaveAfterDelay(contextReceiver, context, delay));
                    }
                    else
                    {
                        contextReceiver.OnShockwaveReceived(context);
                    }
                }
                else if (behaviour is IShockwaveReceiver receiver)
                {
                    if (shockwaveDelayByDistance && shockwaveRadius > 0f)
                    {
                        float delay = Mathf.Clamp01(distance / shockwaveRadius) * Mathf.Max(0f, shockwaveDuration);
                        StartCoroutine(DispatchShockwaveAfterDelay(receiver, origin, distance, delay));
                    }
                    else
                    {
                        receiver.OnShockwave(origin, distance, gameObject);
                    }
                }
            }
        }
    }

    private IEnumerator DispatchShockwaveAfterDelay(IShockwaveReceiver receiver, Vector2 origin, float distance, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        MonoBehaviour receiverBehaviour = receiver as MonoBehaviour;
        if (receiverBehaviour != null)
        {
            receiver.OnShockwave(origin, distance, gameObject);
        }
    }

    private IEnumerator DispatchShockwaveAfterDelay(IShockwaveContextReceiver receiver, ShockwaveContext context, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        MonoBehaviour receiverBehaviour = receiver as MonoBehaviour;
        if (receiverBehaviour != null)
        {
            receiver.OnShockwaveReceived(context);
        }
    }

    private void CreateShockwaveVisual(Vector2 origin)
    {
        if (shockwaveDuration <= 0f || shockwaveRadius <= 0f)
        {
            return;
        }

        var ringObject = new GameObject("DiveShockwaveVisual");
        ringObject.transform.position = origin;
        ringObject.layer = aimVisualLayer >= 0 ? aimVisualLayer : gameObject.layer;

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 48;
        ring.startWidth = 0.045f;
        ring.endWidth = 0.045f;
        ring.sortingOrder = GetAimLineSortingOrder(24);
        if (spriteRenderer != null)
        {
            ring.sortingLayerID = spriteRenderer.sortingLayerID;
        }
        if (aimLineMaterial != null)
        {
            ring.sharedMaterial = aimLineMaterial;
        }

        StartCoroutine(AnimateShockwaveVisual(ringObject, ring, origin));
    }

    private IEnumerator AnimateShockwaveVisual(GameObject ringObject, LineRenderer ring, Vector2 origin)
    {
        float elapsed = 0f;
        while (elapsed < shockwaveDuration && ring != null)
        {
            elapsed += Time.deltaTime;
            float t = shockwaveDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / shockwaveDuration);
            float radius = Mathf.Lerp(0.05f, shockwaveRadius, t);
            Color color = new Color(1f, 0.92f, 0.35f, Mathf.Lerp(0.55f, 0f, t));
            ring.startColor = color;
            ring.endColor = color;
            SetShockwaveRingPositions(ring, origin, radius);
            yield return null;
        }

        if (ringObject != null)
        {
            Destroy(ringObject);
        }
    }

    private void SetShockwaveRingPositions(LineRenderer ring, Vector2 origin, float radius)
    {
        int count = ring.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                origin.x + Mathf.Cos(angle) * radius,
                origin.y + Mathf.Sin(angle) * radius,
                transform.position.z);
            ring.SetPosition(i, point);
        }
    }

    private void UpdateDiveLandingStun()
    {
        if (diveLandingStunTimer <= 0f)
        {
            return;
        }

        diveLandingStunTimer = Mathf.Max(0f, diveLandingStunTimer - Time.deltaTime);
    }

    private void UpdateDownSlamBounceLock()
    {
        if (downSlamBounceLockTimer <= 0f)
        {
            return;
        }

        downSlamBounceLockTimer = Mathf.Max(0f, downSlamBounceLockTimer - Time.deltaTime);
    }

    private void StartDownSlamBounceLock(float bounceVelocityY)
    {
        if (!useApexPercentDownSlamLock || bounceVelocityY <= 0f)
        {
            downSlamBounceLockTimer = 0f;
            lastDownSlamBounceLockDuration = 0f;
            return;
        }

        float gravityAbs = GetCurrentGravityAbs();
        float timeToApex = gravityAbs > 0.0001f ? bounceVelocityY / gravityAbs : minDownSlamBounceLockTime;
        float lockDuration = timeToApex * unlockAtApexTimePercent;
        lockDuration = Mathf.Clamp(lockDuration, minDownSlamBounceLockTime, maxDownSlamBounceLockTime);

        downSlamBounceLockTimer = Mathf.Max(downSlamBounceLockTimer, lockDuration);
        lastDownSlamBounceLockDuration = lockDuration;
    }

    private float GetCurrentGravityAbs()
    {
        float gravityScale = body != null ? body.gravityScale : originalGravityScale;
        if (Mathf.Approximately(gravityScale, 0f))
        {
            gravityScale = originalGravityScale;
        }

        return Mathf.Abs(Physics2D.gravity.y * gravityScale);
    }

    private void ClearDownSlamBounceLock()
    {
        downSlamBounceLockTimer = 0f;
        lastDownSlamBounceLockDuration = 0f;
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

        Vector2 previewVelocity = GetPreviewProjectileVelocity(dragVector);
        FireProjectile(previewVelocity);
        nextFireTime = Time.time + projectileCooldown;
    }

    private void FireProjectile(Vector2 previewVelocity)
    {
        Vector2 direction = previewVelocity.sqrMagnitude > 0f ? previewVelocity.normalized : Vector2.right;
        var projectileObject = new GameObject(playerId + "_" + element + "Projectile");
        projectileObject.layer = gameObject.layer;
        projectileObject.transform.position = GetSafeProjectileSpawnPosition(direction);

        var visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(projectileObject.transform, false);
        visualObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, projectileVisualScale);

        var renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GamePrototypeManager.Instance != null ? GamePrototypeManager.Instance.ProjectileSprite : null;
        renderer.color = element == ElementType.Water ? new Color(0.2f, 0.65f, 1f, 1f) : new Color(1f, 0.35f, 0.1f, 1f);
        renderer.sortingOrder = 5;

        projectileObject.AddComponent<Rigidbody2D>();
        var projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
        projectileCollider.radius = Mathf.Max(0.01f, projectileColliderRadius);
        var projectile = projectileObject.AddComponent<ElementProjectile>();
        float speedMultiplier = Mathf.Max(0.01f, projectileSpeedMultiplier);
        projectile.Initialize(element, this, previewVelocity * speedMultiplier, projectileLifetime, speedMultiplier * speedMultiplier);
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
        Vector2 initialVelocity = GetPreviewProjectileVelocity(dragVector);
        Vector2 start = GetSafeProjectileSpawnPosition(initialVelocity.normalized);
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

    private Vector2 GetPreviewProjectileVelocity(Vector2 dragVector)
    {
        float force = Mathf.Clamp(dragVector.magnitude * dragForceScale, projectileMinForce, projectileMaxForce);
        Vector2 velocity = dragVector.normalized * force;
        if (projectileMaxUpwardVelocity > 0f && velocity.y > projectileMaxUpwardVelocity)
        {
            velocity.y = projectileMaxUpwardVelocity;
        }

        return velocity;
    }

    private Vector2 GetSafeProjectileSpawnPosition(Vector2 direction)
    {
        Vector2 origin = transform.position;
        float spawnDistance = Mathf.Max(0f, projectileSpawnOffset);
        if (spawnDistance <= 0f || direction.sqrMagnitude <= 0f)
        {
            return origin;
        }

        direction.Normalize();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask);
        filter.useTriggers = false;

        float radius = Mathf.Max(0.01f, projectileColliderRadius);
        int hitCount = Physics2D.CircleCast(origin, radius, direction, filter, projectileSpawnHits, spawnDistance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = projectileSpawnHits[i].collider;
            if (hitCollider == null || hitCollider == bodyCollider)
            {
                continue;
            }

            PlayerCharacter hitPlayer = hitCollider.GetComponentInParent<PlayerCharacter>();
            if (hitPlayer == this)
            {
                continue;
            }

            spawnDistance = Mathf.Min(spawnDistance, Mathf.Max(0f, projectileSpawnHits[i].distance - 0.01f));
        }

        return origin + direction * spawnDistance;
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
        if (isDiving || diveLandingStunTimer > 0f || HeldTool != HeldToolType.Umbrella || body.linearVelocity.y >= 0f)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, -3.5f));
    }

    private void ResetJumpActionState()
    {
        leafBounceCurveActive = false;
        leafBounceAirControlLocked = false;
        isChargingJump = false;
        stationaryJumpCharge = false;
        jumpChargeTimer = 0f;
        isDiving = false;
        diveUsed = false;
        fullChargeJumpActive = false;
        RestoreDefaultGravity();
        jumpConsumedUntilLanding = false;
        jumpInputReleasedAfterLaunch = true;
        diveLandingStunTimer = 0f;
        externalKnockbackInputLockTimer = 0f;
        ClearDownSlamBounceLock();
        coyoteTimer = 0f;
    }

    private void ApplyJumpGravityForCurrentJump()
    {
        if (body == null)
        {
            return;
        }

        float gravityMultiplier = preserveJumpArcWhileReducingAirTime
            ? GetJumpMotionVelocityScale() * GetJumpMotionVelocityScale()
            : 1f;

        if (fullChargeJumpActive)
        {
            gravityMultiplier *= fullChargeGravityMultiplier;
        }

        body.gravityScale = originalGravityScale * gravityMultiplier;
    }

    private float GetFullChargeTrajectorySpeedScale()
    {
        return Mathf.Sqrt(Mathf.Clamp(fullChargeGravityMultiplier, 0.05f, 1f));
    }

    private float GetJumpMotionVelocityScale()
    {
        return Mathf.Max(0.01f, jumpMotionSpeedMultiplier);
    }

    private void RestoreDefaultGravity()
    {
        if (body == null)
        {
            return;
        }

        body.gravityScale = originalGravityScale;
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

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        bool showCharge = showChargeDebug && isChargingJump;
        bool showBounceLock = debugDownSlamBounceLock && downSlamBounceLockTimer > 0f;
        if (!showCharge && !showBounceLock)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 1.25f);
        if (screenPosition.z < 0f)
        {
            return;
        }

        Color previousColor = GUI.color;
        float screenY = Screen.height - screenPosition.y - 18f;

        if (showCharge)
        {
            Rect barBackground = new Rect(screenPosition.x - 42f, screenY, 84f, 9f);
            Rect barFill = new Rect(
                barBackground.x + 1f,
                barBackground.y + 1f,
                (barBackground.width - 2f) * JumpChargeNormalized,
                barBackground.height - 2f);

            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(barBackground, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(new Color(0.25f, 0.75f, 1f, 0.95f), new Color(1f, 0.92f, 0.15f, 1f), JumpChargeNormalized);
            GUI.DrawTexture(barFill, Texture2D.whiteTexture);

            screenY += 13f;
        }

        if (showBounceLock)
        {
            float normalizedLock = lastDownSlamBounceLockDuration > 0f
                ? downSlamBounceLockTimer / lastDownSlamBounceLockDuration
                : 0f;
            float unlockProgress = Mathf.Clamp01(1f - normalizedLock);
            Rect barBackground = new Rect(screenPosition.x - 42f, screenY, 84f, 7f);
            Rect barFill = new Rect(
                barBackground.x + 1f,
                barBackground.y + 1f,
                (barBackground.width - 2f) * unlockProgress,
                barBackground.height - 2f);

            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(barBackground, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(new Color(1f, 0.45f, 0.1f, 0.95f), new Color(0.35f, 1f, 0.35f, 1f), unlockProgress);
            GUI.DrawTexture(barFill, Texture2D.whiteTexture);
        }

        GUI.color = previousColor;
    }

    private void OnDrawGizmosSelected()
    {
        DrawGroundCastGizmo();
        DrawShockwaveGizmo();
        DrawReviveRangeGizmo();
        DrawProjectileGizmos();
    }

    private void DrawGroundCastGizmo()
    {
        Collider2D colliderToDraw = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
        if (colliderToDraw == null)
        {
            return;
        }

        Gizmos.color = GroundCastGizmoColor;
        Vector3 castOffset = Vector3.down * Mathf.Max(0f, groundCastDistance);
        if (colliderToDraw is BoxCollider2D box)
        {
            Vector2 worldCenter = (Vector2)transform.position + box.offset + (Vector2)castOffset;
            Vector2 worldSize = Vector2.Scale(box.size, transform.lossyScale);
            Gizmos.DrawWireCube(worldCenter, worldSize);
            return;
        }

        if (colliderToDraw is CircleCollider2D circle)
        {
            Vector2 worldCenter = (Vector2)transform.position + circle.offset + (Vector2)castOffset;
            float worldRadius = circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Gizmos.DrawWireSphere(worldCenter, worldRadius);
        }
    }

    private void DrawReviveRangeGizmo()
    {
        Gizmos.color = ReviveRangeGizmoColor;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, reviveRange));
    }

    private void DrawShockwaveGizmo()
    {
        Gizmos.color = ShockwaveGizmoColor;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, shockwaveRadius));
    }

    private void DrawProjectileGizmos()
    {
        float spawnOffset = Mathf.Max(0f, projectileSpawnOffset);
        Vector3 sampleSpawn = transform.position + Vector3.right * spawnOffset;
        float visualRadius = Mathf.Max(0.01f, projectileVisualScale) * 0.5f;
        float colliderRadius = Mathf.Max(0.01f, projectileColliderRadius);

        Gizmos.color = ProjectileSpawnGizmoColor;
        Gizmos.DrawWireSphere(transform.position, spawnOffset);

        Gizmos.color = ProjectileVisualGizmoColor;
        Gizmos.DrawWireSphere(sampleSpawn, visualRadius);

        Gizmos.color = ProjectileColliderGizmoColor;
        Gizmos.DrawWireSphere(sampleSpawn, colliderRadius);
    }

    private void OnValidate()
    {
        jumpKey = KeyCode.Space;

        moveSpeed = Mathf.Max(0f, moveSpeed);
        airControlMultiplier = Mathf.Clamp01(airControlMultiplier);
        groundAcceleration = Mathf.Max(0f, groundAcceleration);
        groundDeceleration = Mathf.Max(0f, groundDeceleration);
        airAcceleration = Mathf.Max(0f, airAcceleration);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        groundCastDistance = Mathf.Max(0f, groundCastDistance);
        sideCastDistance = Mathf.Max(0f, sideCastDistance);
        minimumGroundNormalY = Mathf.Clamp01(minimumGroundNormalY);
        maxChargeTime = Mathf.Max(0f, maxChargeTime);
        minJumpPower = Mathf.Max(0f, minJumpPower);
        maxJumpPower = Mathf.Max(minJumpPower, maxJumpPower);
        horizontalJumpPower = Mathf.Max(0f, horizontalJumpPower);
        minHorizontalChargeMultiplier = Mathf.Clamp01(minHorizontalChargeMultiplier);
        jumpMotionSpeedMultiplier = Mathf.Max(0.01f, jumpMotionSpeedMultiplier);
        fullChargeThreshold = Mathf.Clamp01(fullChargeThreshold);
        fullChargeGravityMultiplier = Mathf.Clamp(fullChargeGravityMultiplier, 0.05f, 1f);
        fullChargeAirSlowMultiplier = Mathf.Clamp(fullChargeAirSlowMultiplier, 0.05f, 1f);

        diveSpeed = Mathf.Max(0f, diveSpeed);
        diveLandingStun = Mathf.Max(0f, diveLandingStun);
        shockwaveRadius = Mathf.Max(0f, shockwaveRadius);
        shockwaveDuration = Mathf.Max(0f, shockwaveDuration);
        unlockAtApexTimePercent = Mathf.Clamp01(unlockAtApexTimePercent);
        minDownSlamBounceLockTime = Mathf.Max(0f, minDownSlamBounceLockTime);
        maxDownSlamBounceLockTime = Mathf.Max(minDownSlamBounceLockTime, maxDownSlamBounceLockTime);
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(aimLineMaterial);
    }

    private void DestroyRuntimeMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }
}
