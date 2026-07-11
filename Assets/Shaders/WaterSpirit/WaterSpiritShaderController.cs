using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class WaterSpiritShaderController : MonoBehaviour
{
    private const string WaterShaderName = "WaterAndFire/WaterSpirit";

    private static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int FlowSpeedMultiplierId = Shader.PropertyToID("_FlowSpeedMultiplier");
    private static readonly int DistortionMultiplierId = Shader.PropertyToID("_DistortionMultiplier");
    private static readonly int VisualStretchId = Shader.PropertyToID("_VisualStretch");
    private static readonly int FlowPullId = Shader.PropertyToID("_FlowPull");
    private static readonly int LandingPulseId = Shader.PropertyToID("_LandingPulse");
    private static readonly int LandingPulseProgressId = Shader.PropertyToID("_LandingPulseProgress");
    private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("Movement Response")]
    [Min(0.01f)] [SerializeField] private float speedForMaxResponse = 7f;
    [Range(1f, 3f)] [SerializeField] private float movementFlowMultiplier = 1.35f;
    [Range(1f, 3f)] [SerializeField] private float movementDistortionMultiplier = 1.28f;
    [Range(0f, 0.35f)] [SerializeField] private float horizontalFlowPull = 0.08f;
    [Range(0f, 0.25f)] [SerializeField] private float horizontalStretchStrength = 0.045f;
    [Min(0.01f)] [SerializeField] private float stateSmoothSpeed = 8f;

    [Header("Airborne Response")]
    [Range(0f, 0.4f)] [SerializeField] private float airborneStretchStrength = 0.11f;
    [Range(0f, 0.4f)] [SerializeField] private float fallingStretchStrength = 0.08f;
    [Range(0f, 0.35f)] [SerializeField] private float verticalFlowPull = 0.12f;
    [Min(0.01f)] [SerializeField] private float verticalSpeedForMaxResponse = 10f;

    [Header("Landing Pulse")]
    [Range(0f, 3f)] [SerializeField] private float landingPulseStrength = 1f;
    [Min(0.05f)] [SerializeField] private float landingPulseDuration = 0.34f;
    [Min(0f)] [SerializeField] private float minimumLandingSpeed = 1.5f;
    [Range(0f, 1f)] [SerializeField] private float minimumGroundNormalY = 0.55f;

    private readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[8];

    private MaterialPropertyBlock propertyBlock;
    private Sprite cachedSprite;
    private Vector4 spriteUvRect = new Vector4(0f, 0f, 1f, 1f);
    private float timeOffset;
    private float currentFlowMultiplier = 1f;
    private float currentDistortionMultiplier = 1f;
    private Vector2 currentStretch;
    private Vector2 currentFlowPull;
    private float pulseElapsed;
    private bool pulseActive;
    private bool wasGrounded;
    private float previousVerticalVelocity;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        EnsurePropertyBlock();
        timeOffset = Mathf.Abs(GetInstanceID() * 0.0173f) % 29f;
        UpdateSpriteRect(true);
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsurePropertyBlock();
        timeOffset = Mathf.Abs(GetInstanceID() * 0.0173f) % 29f;
        wasGrounded = ResolveGrounded();
        previousVerticalVelocity = body != null ? body.linearVelocity.y : 0f;
        UpdateSpriteRect(true);
        ApplyShaderValues(0f, 0f);
    }

    private void OnValidate()
    {
        speedForMaxResponse = Mathf.Max(0.01f, speedForMaxResponse);
        verticalSpeedForMaxResponse = Mathf.Max(0.01f, verticalSpeedForMaxResponse);
        stateSmoothSpeed = Mathf.Max(0.01f, stateSmoothSpeed);
        landingPulseDuration = Mathf.Max(0.05f, landingPulseDuration);

        CacheReferences();
        EnsurePropertyBlock();
        UpdateSpriteRect(true);

        if (!Application.isPlaying)
        {
            ApplyShaderValues(0f, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || body == null)
        {
            return;
        }

        bool grounded = ResolveGrounded();
        if (!wasGrounded && grounded && previousVerticalVelocity <= -minimumLandingSpeed)
        {
            TriggerLandingPulse();
        }

        wasGrounded = grounded;
        previousVerticalVelocity = body.linearVelocity.y;
    }

    private void Update()
    {
        CacheReferences();
        EnsurePropertyBlock();
        UpdateSpriteRect(false);

        Vector2 velocity = Application.isPlaying && body != null ? body.linearVelocity : Vector2.zero;
        bool grounded = !Application.isPlaying || ResolveGrounded();

        float horizontalResponse = Mathf.Clamp01(Mathf.Abs(velocity.x) / speedForMaxResponse);
        float verticalResponse = Mathf.Clamp01(Mathf.Abs(velocity.y) / verticalSpeedForMaxResponse);
        float rising = !grounded ? Mathf.Clamp01(velocity.y / verticalSpeedForMaxResponse) : 0f;
        float falling = !grounded ? Mathf.Clamp01(-velocity.y / verticalSpeedForMaxResponse) : 0f;

        float targetFlowMultiplier = Mathf.Lerp(1f, movementFlowMultiplier, horizontalResponse);
        float targetDistortionMultiplier = Mathf.Lerp(1f, movementDistortionMultiplier, horizontalResponse);
        targetDistortionMultiplier += verticalResponse * 0.08f;

        Vector2 targetStretch = new Vector2(
            horizontalResponse * horizontalStretchStrength,
            rising * airborneStretchStrength + falling * fallingStretchStrength);

        Vector2 targetFlowPull = new Vector2(
            Mathf.Sign(velocity.x) * horizontalResponse * horizontalFlowPull,
            (rising - falling) * verticalFlowPull);

        float deltaTime = Application.isPlaying ? Time.deltaTime : 1f / 60f;
        float blend = 1f - Mathf.Exp(-stateSmoothSpeed * Mathf.Max(deltaTime, 0.0001f));
        currentFlowMultiplier = Mathf.Lerp(currentFlowMultiplier, targetFlowMultiplier, blend);
        currentDistortionMultiplier = Mathf.Lerp(currentDistortionMultiplier, targetDistortionMultiplier, blend);
        currentStretch = Vector2.Lerp(currentStretch, targetStretch, blend);
        currentFlowPull = Vector2.Lerp(currentFlowPull, targetFlowPull, blend);

        float pulseEnvelope = UpdateLandingPulse(deltaTime, out float pulseProgress);
        ApplyShaderValues(pulseEnvelope, pulseProgress);
    }

    public void TriggerLandingPulse()
    {
        pulseElapsed = 0f;
        pulseActive = true;
    }

    private void CacheReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }
    }

    private void EnsurePropertyBlock()
    {
        propertyBlock ??= new MaterialPropertyBlock();
    }

    private bool ResolveGrounded()
    {
        if (playerCharacter != null)
        {
            return playerCharacter.IsGrounded;
        }

        if (body == null)
        {
            return false;
        }

        int contactCount = body.GetContacts(contactBuffer);
        for (int index = 0; index < contactCount; index++)
        {
            if (contactBuffer[index].normal.y >= minimumGroundNormalY)
            {
                return true;
            }
        }

        return false;
    }

    private float UpdateLandingPulse(float deltaTime, out float progress)
    {
        if (!pulseActive)
        {
            progress = 0f;
            return 0f;
        }

        pulseElapsed += Mathf.Max(deltaTime, 0f);
        progress = Mathf.Clamp01(pulseElapsed / landingPulseDuration);
        float envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress * 0.28f);

        if (progress >= 1f)
        {
            pulseActive = false;
            progress = 0f;
            return 0f;
        }

        return envelope * landingPulseStrength;
    }

    private void UpdateSpriteRect(bool force)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Sprite sprite = targetRenderer.sprite;
        if (!force && sprite == cachedSprite)
        {
            return;
        }

        cachedSprite = sprite;
        if (sprite == null || sprite.texture == null)
        {
            spriteUvRect = new Vector4(0f, 0f, 1f, 1f);
            return;
        }

        Rect textureRect;
        try
        {
            textureRect = sprite.textureRect;
        }
        catch (UnityException)
        {
            textureRect = sprite.rect;
        }

        float textureWidth = Mathf.Max(sprite.texture.width, 1f);
        float textureHeight = Mathf.Max(sprite.texture.height, 1f);
        spriteUvRect = new Vector4(
            textureRect.xMin / textureWidth,
            textureRect.yMin / textureHeight,
            textureRect.width / textureWidth,
            textureRect.height / textureHeight);
    }

    private void ApplyShaderValues(float pulseEnvelope, float pulseProgress)
    {
        if (targetRenderer == null || propertyBlock == null)
        {
            return;
        }

        Material material = targetRenderer.sharedMaterial;
        if (material == null || material.shader == null || material.shader.name != WaterShaderName)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(SpriteUvRectId, spriteUvRect);
        propertyBlock.SetFloat(FlowSpeedMultiplierId, currentFlowMultiplier);
        propertyBlock.SetFloat(DistortionMultiplierId, currentDistortionMultiplier);
        propertyBlock.SetVector(VisualStretchId, new Vector4(currentStretch.x, currentStretch.y, 0f, 0f));
        propertyBlock.SetVector(FlowPullId, new Vector4(currentFlowPull.x, currentFlowPull.y, 0f, 0f));
        propertyBlock.SetFloat(LandingPulseId, pulseEnvelope);
        propertyBlock.SetFloat(LandingPulseProgressId, pulseProgress);
        propertyBlock.SetFloat(TimeOffsetId, timeOffset);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}
