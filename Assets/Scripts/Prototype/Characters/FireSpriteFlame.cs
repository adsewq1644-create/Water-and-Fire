using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
public class FireSpriteFlame : MonoBehaviour
{
    private const string ShaderName = "WaterAndFire/SpriteFireFlame";
    private const string FlameTexturePath = "Assets/VFXPACK_FIRE_WALLCOEUR/Texture/a_VFX_flame.png";
    private const string SmokeTexturePath = "Assets/VFXPACK_FIRE_WALLCOEUR/Texture/A_Smoke_2.png";
    private const int CurrentPresetVersion = 5;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int FlameTexId = Shader.PropertyToID("_FlameTex");
    private static readonly int SmokeTexId = Shader.PropertyToID("_SmokeTex");
    private static readonly int UseRectPlaceholderMaskId = Shader.PropertyToID("_UseRectPlaceholderMask");
    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int SmokeColorId = Shader.PropertyToID("_SmokeColor");
    private static readonly int BodyAlphaId = Shader.PropertyToID("_BodyAlpha");
    private static readonly int PixelGridId = Shader.PropertyToID("_PixelGrid");
    private static readonly int FlameTilingId = Shader.PropertyToID("_FlameTiling");
    private static readonly int FlameSpeedId = Shader.PropertyToID("_FlameSpeed");
    private static readonly int ShapeShiftId = Shader.PropertyToID("_ShapeShift");
    private static readonly int EdgeBurnId = Shader.PropertyToID("_EdgeBurn");
    private static readonly int CoreStrengthId = Shader.PropertyToID("_CoreStrength");
    private static readonly int WindBendId = Shader.PropertyToID("_WindBend");
    private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
    private static readonly int SmokeAmountId = Shader.PropertyToID("_SmokeAmount");
    private static readonly int EmberAmountId = Shader.PropertyToID("_EmberAmount");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineStrengthId = Shader.PropertyToID("_OutlineStrength");
    private static readonly int OuterFlameWidthId = Shader.PropertyToID("_OuterFlameWidth");
    private static readonly int OuterFlameStrengthId = Shader.PropertyToID("_OuterFlameStrength");
    private static readonly int OuterFlameWobbleId = Shader.PropertyToID("_OuterFlameWobble");
    private static readonly int SilhouetteMeltId = Shader.PropertyToID("_SilhouetteMelt");

    [Header("Textures")]
    [SerializeField] private Texture2D flameTexture;
    [SerializeField] private Texture2D smokeTexture;

    [Header("Fire Art Controls")]
    [ColorUsage(false, true)] [SerializeField] private Color coreColor = new Color(1f, 0.92f, 0.22f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color midColor = new Color(1f, 0.45f, 0.06f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color edgeColor = new Color(0.95f, 0.17f, 0.03f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color glowColor = new Color(1f, 0.34f, 0.06f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color outlineColor = new Color(0.38f, 0.04f, 0.015f, 1f);
    [SerializeField] private Color smokeColor = new Color(0.07f, 0.055f, 0.045f, 1f);
    [Range(0f, 1f)] [SerializeField] private float bodyAlpha = 0.98f;
    [Range(8f, 128f)] [SerializeField] private float pixelGrid = 44f;
    [Range(0.5f, 6f)] [SerializeField] private float flameTiling = 2.8f;
    [Range(0f, 1f)] [SerializeField] private float smokeAmount = 0.14f;
    [Range(0f, 1f)] [SerializeField] private float emberAmount = 0.32f;
    [Range(0f, 5f)] [SerializeField] private float emissionIntensity = 1.45f;
    [Range(0f, 2f)] [SerializeField] private float edgeBurn = 1.2f;
    [Range(0f, 2f)] [SerializeField] private float coreStrength = 1.25f;
    [Range(0f, 24f)] [SerializeField] private float outlineWidth = 0f;
    [Range(0f, 2f)] [SerializeField] private float outlineStrength = 0f;
    [Range(0f, 32f)] [SerializeField] private float outerFlameWidth = 6f;
    [Range(0f, 3f)] [SerializeField] private float outerFlameStrength = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float outerFlameWobble = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float silhouetteMelt = 0.58f;

    [Header("Motion Response")]
    [SerializeField] private float idleFlameSpeed = 2.6f;
    [SerializeField] private float movingFlameSpeed = 4.4f;
    [SerializeField] private float idleShapeShift = 0.44f;
    [SerializeField] private float movingShapeShift = 0.72f;
    [SerializeField] private float windStrength = 0.55f;
    [SerializeField] private float maxWindBend = 0.95f;
    [SerializeField] private float speedForMaxWind = 7f;
    [SerializeField] private float response = 20f;
    [SerializeField, HideInInspector] private int presetVersion;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;
    private bool useRectPlaceholderMask;
    private float currentWindBend;
    private float currentFlameSpeed;
    private float currentShapeShift;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        propertyBlock = new MaterialPropertyBlock();
        LoadDefaultTexturesIfNeeded();
        UpgradePresetIfNeeded();
        useRectPlaceholderMask = ShouldUseRectPlaceholderMask();
        ApplyFireMaterial();

        currentWindBend = 0f;
        currentFlameSpeed = idleFlameSpeed;
        currentShapeShift = idleShapeShift;
        ApplyShaderValues();
    }

    private void OnEnable()
    {
        LoadDefaultTexturesIfNeeded();

        if (propertyBlock != null)
        {
            ApplyShaderValues();
        }
    }

    private void Update()
    {
        if (spriteRenderer == null || body == null)
        {
            return;
        }

        float horizontalSpeed = body.linearVelocity.x;
        float speed01 = Mathf.Clamp01(Mathf.Abs(horizontalSpeed) / Mathf.Max(0.0001f, speedForMaxWind));
        float targetWindBend = Mathf.Clamp(-horizontalSpeed / Mathf.Max(0.0001f, speedForMaxWind), -1f, 1f) * maxWindBend;
        float targetFlameSpeed = Mathf.Lerp(idleFlameSpeed, movingFlameSpeed, speed01);
        float targetShapeShift = Mathf.Lerp(idleShapeShift, movingShapeShift, speed01);
        float blend = 1f - Mathf.Exp(-response * Time.deltaTime);

        currentWindBend = Mathf.Lerp(currentWindBend, targetWindBend, blend);
        currentFlameSpeed = Mathf.Lerp(currentFlameSpeed, targetFlameSpeed, blend);
        currentShapeShift = Mathf.Lerp(currentShapeShift, targetShapeShift, blend);
        ApplyShaderValues();
    }

    private void ApplyFireMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning("Fire sprite shader not found: " + ShaderName, this);
            return;
        }

        runtimeMaterial = new Material(shader);
        runtimeMaterial.name = "Runtime_FireSpriteFlame";
        if (spriteRenderer.sprite != null && spriteRenderer.sprite.texture != null)
        {
            runtimeMaterial.SetTexture(MainTexId, spriteRenderer.sprite.texture);
        }

        ApplyTexture(FlameTexId, flameTexture);
        ApplyTexture(SmokeTexId, smokeTexture);
        spriteRenderer.material = runtimeMaterial;
    }

    private void LoadDefaultTexturesIfNeeded()
    {
#if UNITY_EDITOR
        if (flameTexture == null)
        {
            flameTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlameTexturePath);
        }

        if (smokeTexture == null)
        {
            smokeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexturePath);
        }
#endif
    }

    private bool ShouldUseRectPlaceholderMask()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return false;
        }

        Texture2D sourceTexture = spriteRenderer.sprite.texture;
        return sourceTexture != null && sourceTexture.width <= 16 && sourceTexture.height <= 16;
    }

    private void ApplyDefaultPreset()
    {
        coreColor = new Color(1f, 0.92f, 0.22f, 1f);
        midColor = new Color(1f, 0.45f, 0.06f, 1f);
        edgeColor = new Color(0.95f, 0.17f, 0.03f, 1f);
        glowColor = new Color(1f, 0.34f, 0.06f, 1f);
        outlineColor = new Color(0.38f, 0.04f, 0.015f, 1f);
        smokeColor = new Color(0.07f, 0.055f, 0.045f, 1f);
        bodyAlpha = 0.98f;
        pixelGrid = 44f;
        flameTiling = 2.8f;
        smokeAmount = 0.14f;
        emberAmount = 0.32f;
        emissionIntensity = 1.45f;
        edgeBurn = 1.2f;
        coreStrength = 1.25f;
        outlineWidth = 0f;
        outlineStrength = 0f;
        outerFlameWidth = 6f;
        outerFlameStrength = 0.55f;
        outerFlameWobble = 0.85f;
        silhouetteMelt = 0.58f;

        idleFlameSpeed = 2.6f;
        movingFlameSpeed = 4.4f;
        idleShapeShift = 0.44f;
        movingShapeShift = 0.72f;
        windStrength = 0.55f;
        maxWindBend = 0.95f;
        speedForMaxWind = 7f;
        response = 20f;
        presetVersion = CurrentPresetVersion;
    }

    private void UpgradePresetIfNeeded()
    {
        if (presetVersion == CurrentPresetVersion)
        {
            return;
        }

        ApplyDefaultPreset();
    }

    private void ApplyTexture(int propertyId, Texture texture)
    {
        if (texture == null || runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetTexture(propertyId, texture);
    }

    private void ApplyShaderValues()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(UseRectPlaceholderMaskId, useRectPlaceholderMask ? 1f : 0f);
        propertyBlock.SetColor(CoreColorId, coreColor);
        propertyBlock.SetColor(MidColorId, midColor);
        propertyBlock.SetColor(EdgeColorId, edgeColor);
        propertyBlock.SetColor(GlowColorId, glowColor);
        propertyBlock.SetColor(OutlineColorId, outlineColor);
        propertyBlock.SetColor(SmokeColorId, smokeColor);
        propertyBlock.SetFloat(BodyAlphaId, bodyAlpha);
        propertyBlock.SetFloat(PixelGridId, pixelGrid);
        propertyBlock.SetFloat(FlameTilingId, flameTiling);
        propertyBlock.SetFloat(FlameSpeedId, currentFlameSpeed);
        propertyBlock.SetFloat(ShapeShiftId, currentShapeShift);
        propertyBlock.SetFloat(EdgeBurnId, edgeBurn);
        propertyBlock.SetFloat(CoreStrengthId, coreStrength);
        propertyBlock.SetFloat(WindBendId, currentWindBend);
        propertyBlock.SetFloat(WindStrengthId, windStrength);
        propertyBlock.SetFloat(SmokeAmountId, smokeAmount);
        propertyBlock.SetFloat(EmberAmountId, emberAmount);
        propertyBlock.SetFloat(EmissionIntensityId, emissionIntensity);
        propertyBlock.SetFloat(OutlineWidthId, outlineWidth);
        propertyBlock.SetFloat(OutlineStrengthId, outlineStrength);
        propertyBlock.SetFloat(OuterFlameWidthId, outerFlameWidth);
        propertyBlock.SetFloat(OuterFlameStrengthId, outerFlameStrength);
        propertyBlock.SetFloat(OuterFlameWobbleId, outerFlameWobble);
        propertyBlock.SetFloat(SilhouetteMeltId, silhouetteMelt);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnValidate()
    {
        LoadDefaultTexturesIfNeeded();

        if (!Application.isPlaying || spriteRenderer == null || propertyBlock == null)
        {
            return;
        }

        ApplyShaderValues();
    }

    private void Reset()
    {
        LoadDefaultTexturesIfNeeded();
        ApplyDefaultPreset();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }
    }
}
