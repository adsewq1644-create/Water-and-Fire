using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
public class WaterSpriteWobble : MonoBehaviour
{
    private const string ShaderName = "WaterAndFire/SpriteWaterWobble";
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UseRectPlaceholderMaskId = Shader.PropertyToID("_UseRectPlaceholderMask");
    private static readonly int WaterColorAId = Shader.PropertyToID("_WaterColorA");
    private static readonly int WaterColorBId = Shader.PropertyToID("_WaterColorB");
    private static readonly int StreamColorId = Shader.PropertyToID("_StreamColor");
    private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int BodyAlphaId = Shader.PropertyToID("_BodyAlpha");
    private static readonly int ColorIntensityId = Shader.PropertyToID("_ColorIntensity");
    private static readonly int BackgroundVisibilityId = Shader.PropertyToID("_BackgroundVisibility");
    private static readonly int BackgroundRefractionStrengthId = Shader.PropertyToID("_BackgroundRefractionStrength");
    private static readonly int PixelRefractionSizeId = Shader.PropertyToID("_PixelRefractionSize");
    private static readonly int PixelPatternSizeId = Shader.PropertyToID("_PixelPatternSize");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int RimEmissionId = Shader.PropertyToID("_RimEmission");
    private static readonly int StreamEmissionId = Shader.PropertyToID("_StreamEmission");
    private static readonly int OuterGlowStrengthId = Shader.PropertyToID("_OuterGlowStrength");
    private static readonly int OuterGlowSizeId = Shader.PropertyToID("_OuterGlowSize");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int InnerFlowStrengthId = Shader.PropertyToID("_InnerFlowStrength");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int PatternTilingId = Shader.PropertyToID("_PatternTiling");
    private static readonly int NoiseTilingId = Shader.PropertyToID("_NoiseTiling");
    private static readonly int WobbleStrengthId = Shader.PropertyToID("_WobbleStrength");
    private static readonly int WobbleSpeedId = Shader.PropertyToID("_WobbleSpeed");
    private static readonly int SparkleStrengthId = Shader.PropertyToID("_SparkleStrength");
    private static readonly int RefractionStrengthId = Shader.PropertyToID("_RefractionStrength");
    private static readonly int PatternTexId = Shader.PropertyToID("_PatternTex");
    private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
    private static readonly int RefractionTexId = Shader.PropertyToID("_RefractionTex");
    private static readonly int SilhouetteWobbleId = Shader.PropertyToID("_SilhouetteWobble");

    [Header("Textures")]
    [SerializeField] private Texture2D patternTexture;
    [SerializeField] private Texture2D noiseTexture;
    [SerializeField] private Texture2D refractionTexture;

    [Header("Water Art Controls")]
    [SerializeField] private Color deepWaterColor = new Color(0.0745f, 0.3294f, 0.6706f, 1f);
    [SerializeField] private Color softWaterColor = new Color(0.5843f, 0.8039f, 0.9922f, 1f);
    [SerializeField] private Color innerStreamColor = new Color(0.451f, 1f, 0.8784f, 1f);
    [SerializeField] private Color rimHighlightColor = new Color(0.8588f, 0.9255f, 0.9255f, 1f);
    [ColorUsage(false, true)] [SerializeField] private Color glowColor = new Color(0.2235f, 0.6235f, 0.902f, 1f);
    [Range(0f, 1f)] [SerializeField] private float transparency = 0.72f;
    [Range(0f, 2f)] [SerializeField] private float colorIntensity = 0.92f;
    [Range(0f, 1f)] [SerializeField] private float backgroundVisibility = 0.258f;
    [Range(1f, 16f)] [SerializeField] private float pixelRefractionSize = 3f;
    [Range(8f, 256f)] [SerializeField] private float pixelPatternSize = 43.5f;
    [Range(0f, 2f)] [SerializeField] private float rimStrength = 1.05f;
    [Range(0f, 8f)] [SerializeField] private float emissionIntensity = 8f;
    [Range(0f, 8f)] [SerializeField] private float rimEmission = 0.1f;
    [Range(0f, 6f)] [SerializeField] private float streamEmission = 1.1f;
    [Range(0f, 4f)] [SerializeField] private float outerGlowStrength = 0f;
    [Range(1f, 16f)] [SerializeField] private float outerGlowSize = 1f;
    [Range(0.001f, 0.18f)] [SerializeField] private float edgeSoftness = 0.045f;
    [Range(0.25f, 8f)] [SerializeField] private float patternTiling = 1.35f;
    [Range(0.5f, 20f)] [SerializeField] private float noiseTiling = 3.4f;

    [Header("Motion Response")]
    [SerializeField] private float idleWobbleStrength = 0.12f;
    [SerializeField] private float movingWobbleStrength = 0.4f;
    [SerializeField] private float idleWobbleSpeed = 0.9f;
    [SerializeField] private float movingWobbleSpeed = 3.5f;
    [SerializeField] private float idleSparkleStrength = 3f;
    [SerializeField] private float movingSparkleStrength = 7f;
    [SerializeField] private float idleRefractionStrength = 0.15f;
    [SerializeField] private float movingRefractionStrength = 0.5f;
    [SerializeField] private float idleSilhouetteWobble = 0.7f;
    [SerializeField] private float movingSilhouetteWobble = 1.4f;
    [SerializeField] private float idleInnerFlowStrength = 0.55f;
    [SerializeField] private float movingInnerFlowStrength = 1.2f;
    [SerializeField] private float idleBackgroundRefraction = 1.15f;
    [SerializeField] private float movingBackgroundRefraction = 0.34f;
    [SerializeField] private float speedForMaxWobble = 8f;
    [SerializeField] private float response = 30f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;
    private float currentStrength;
    private float currentSpeed;
    private float currentSparkle;
    private float currentRefraction;
    private float currentSilhouetteWobble;
    private float currentInnerFlow;
    private float currentBackgroundRefraction;
    private bool useRectPlaceholderMask;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        propertyBlock = new MaterialPropertyBlock();
        LoadDefaultTexturesIfNeeded();
        ApplyDefaultPreset();
        useRectPlaceholderMask = ShouldUseRectPlaceholderMask();
        ApplyWaterMaterial();

        currentStrength = idleWobbleStrength;
        currentSpeed = idleWobbleSpeed;
        currentSparkle = idleSparkleStrength;
        currentRefraction = idleRefractionStrength;
        currentSilhouetteWobble = idleSilhouetteWobble;
        currentInnerFlow = idleInnerFlowStrength;
        currentBackgroundRefraction = idleBackgroundRefraction;
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

    private bool ShouldUseRectPlaceholderMask()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return false;
        }

        Texture2D sourceTexture = spriteRenderer.sprite.texture;
        return sourceTexture != null && sourceTexture.width <= 16 && sourceTexture.height <= 16;
    }

    private void Update()
    {
        if (spriteRenderer == null || body == null)
        {
            return;
        }

        float speed01 = Mathf.Clamp01(body.linearVelocity.magnitude / speedForMaxWobble);
        float targetStrength = Mathf.Lerp(idleWobbleStrength, movingWobbleStrength, speed01);
        float targetSpeed = Mathf.Lerp(idleWobbleSpeed, movingWobbleSpeed, speed01);
        float targetSparkle = Mathf.Lerp(idleSparkleStrength, movingSparkleStrength, speed01);
        float targetRefraction = Mathf.Lerp(idleRefractionStrength, movingRefractionStrength, speed01);
        float targetSilhouetteWobble = Mathf.Lerp(idleSilhouetteWobble, movingSilhouetteWobble, speed01);
        float targetInnerFlow = Mathf.Lerp(idleInnerFlowStrength, movingInnerFlowStrength, speed01);
        float targetBackgroundRefraction = Mathf.Lerp(idleBackgroundRefraction, movingBackgroundRefraction, speed01);
        float blend = 1f - Mathf.Exp(-response * Time.deltaTime);

        currentStrength = Mathf.Lerp(currentStrength, targetStrength, blend);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, blend);
        currentSparkle = Mathf.Lerp(currentSparkle, targetSparkle, blend);
        currentRefraction = Mathf.Lerp(currentRefraction, targetRefraction, blend);
        currentSilhouetteWobble = Mathf.Lerp(currentSilhouetteWobble, targetSilhouetteWobble, blend);
        currentInnerFlow = Mathf.Lerp(currentInnerFlow, targetInnerFlow, blend);
        currentBackgroundRefraction = Mathf.Lerp(currentBackgroundRefraction, targetBackgroundRefraction, blend);
        ApplyShaderValues();
    }

    private void ApplyWaterMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning("Water sprite shader not found: " + ShaderName, this);
            return;
        }

        runtimeMaterial = new Material(shader);
        runtimeMaterial.name = "Runtime_WaterSpriteWobble";
        if (spriteRenderer.sprite != null && spriteRenderer.sprite.texture != null)
        {
            runtimeMaterial.SetTexture(MainTexId, spriteRenderer.sprite.texture);
        }

        ApplyTexture(PatternTexId, patternTexture);
        ApplyTexture(NoiseTexId, noiseTexture);
        ApplyTexture(RefractionTexId, refractionTexture);
        spriteRenderer.material = runtimeMaterial;
    }

    private void LoadDefaultTexturesIfNeeded()
    {
        if (patternTexture == null)
        {
            patternTexture = Resources.Load<Texture2D>("WaterShader/Water_MainPattern");
        }

        if (noiseTexture == null)
        {
            noiseTexture = Resources.Load<Texture2D>("WaterShader/Water_Noise");
        }

        if (refractionTexture == null)
        {
            refractionTexture = Resources.Load<Texture2D>("WaterShader/Water_Refraction");
        }
    }

    private void ApplyDefaultPreset()
    {
        deepWaterColor = new Color(0.0745f, 0.3294f, 0.6706f, 1f);
        softWaterColor = new Color(0.5843f, 0.8039f, 0.9922f, 1f);
        innerStreamColor = new Color(0.451f, 1f, 0.8784f, 1f);
        rimHighlightColor = new Color(0.8588f, 0.9255f, 0.9255f, 1f);
        glowColor = new Color(0.2235f, 0.6235f, 0.902f, 1f);

        transparency = 0.72f;
        colorIntensity = 0.92f;
        backgroundVisibility = 0.258f;
        pixelRefractionSize = 3f;
        pixelPatternSize = 43.5f;
        rimStrength = 1.05f;
        emissionIntensity = 8f;
        rimEmission = 0.1f;
        streamEmission = 1.1f;
        outerGlowStrength = 0f;
        outerGlowSize = 1f;
        edgeSoftness = 0.045f;
        patternTiling = 1.35f;
        noiseTiling = 3.4f;

        idleWobbleStrength = 0.12f;
        movingWobbleStrength = 0.4f;
        idleWobbleSpeed = 0.9f;
        movingWobbleSpeed = 3.5f;
        idleSparkleStrength = 3f;
        movingSparkleStrength = 7f;
        idleRefractionStrength = 0.15f;
        movingRefractionStrength = 0.5f;
        idleSilhouetteWobble = 0.7f;
        movingSilhouetteWobble = 1.4f;
        idleInnerFlowStrength = 0.55f;
        movingInnerFlowStrength = 1.2f;
        idleBackgroundRefraction = 1.15f;
        movingBackgroundRefraction = 0.34f;
        speedForMaxWobble = 8f;
        response = 30f;
    }

    private void ApplyTexture(int propertyId, Texture texture)
    {
        if (texture == null || runtimeMaterial == null)
        {
            return;
        }

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        runtimeMaterial.SetTexture(propertyId, texture);
    }

    private void ApplyShaderValues()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(UseRectPlaceholderMaskId, useRectPlaceholderMask ? 1f : 0f);
        propertyBlock.SetColor(WaterColorAId, deepWaterColor);
        propertyBlock.SetColor(WaterColorBId, softWaterColor);
        propertyBlock.SetColor(StreamColorId, innerStreamColor);
        propertyBlock.SetColor(FoamColorId, rimHighlightColor);
        propertyBlock.SetColor(GlowColorId, glowColor);
        propertyBlock.SetFloat(BodyAlphaId, transparency);
        propertyBlock.SetFloat(ColorIntensityId, colorIntensity);
        propertyBlock.SetFloat(BackgroundVisibilityId, backgroundVisibility);
        propertyBlock.SetFloat(BackgroundRefractionStrengthId, currentBackgroundRefraction);
        propertyBlock.SetFloat(PixelRefractionSizeId, pixelRefractionSize);
        propertyBlock.SetFloat(PixelPatternSizeId, pixelPatternSize);
        propertyBlock.SetFloat(EmissionIntensityId, emissionIntensity);
        propertyBlock.SetFloat(RimEmissionId, rimEmission);
        propertyBlock.SetFloat(StreamEmissionId, streamEmission);
        propertyBlock.SetFloat(OuterGlowStrengthId, outerGlowStrength);
        propertyBlock.SetFloat(OuterGlowSizeId, outerGlowSize);
        propertyBlock.SetFloat(RimStrengthId, rimStrength);
        propertyBlock.SetFloat(InnerFlowStrengthId, currentInnerFlow);
        propertyBlock.SetFloat(EdgeSoftnessId, edgeSoftness);
        propertyBlock.SetFloat(PatternTilingId, patternTiling);
        propertyBlock.SetFloat(NoiseTilingId, noiseTiling);
        propertyBlock.SetFloat(WobbleStrengthId, currentStrength);
        propertyBlock.SetFloat(WobbleSpeedId, currentSpeed);
        propertyBlock.SetFloat(SparkleStrengthId, currentSparkle);
        propertyBlock.SetFloat(RefractionStrengthId, currentRefraction);
        propertyBlock.SetFloat(SilhouetteWobbleId, currentSilhouetteWobble);
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
