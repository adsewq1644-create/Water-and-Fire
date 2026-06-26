using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
public class FireSpriteEdgeParticles : MonoBehaviour
{
    private const string FlameMaterialPath = "Assets/VFXPACK_FIRE_WALLCOEUR/Material/A_FlameAdd 1.mat";
    private const string FlameTexturePath = "Assets/VFXPACK_FIRE_WALLCOEUR/Texture/a_VFX_flame.png";
    private const int CurrentPresetVersion = 2;
    private const string TopSystemName = "FireEdgeParticles_Top";
    private const string LeftSystemName = "FireEdgeParticles_Left";
    private const string RightSystemName = "FireEdgeParticles_Right";
    private const string EmberSystemName = "FireEdgeParticles_Embers";

    [Header("Assets")]
    [SerializeField] private Material flameMaterial;
    [SerializeField] private Texture2D fallbackFlameTexture;

    [Header("Emission")]
    [SerializeField] private bool playOnAwake = true;
    [Range(0f, 80f)] [SerializeField] private float topEmissionRate = 14f;
    [Range(0f, 60f)] [SerializeField] private float sideEmissionRate = 4f;
    [Range(0f, 40f)] [SerializeField] private float emberEmissionRate = 2f;
    [Range(0.02f, 0.4f)] [SerializeField] private float edgeBandThickness = 0.035f;
    [Range(0f, 0.6f)] [SerializeField] private float outsideOffset = 0.025f;
    [Range(0f, 0.8f)] [SerializeField] private float sideHeightScale = 0.48f;

    [Header("Look")]
    [ColorUsage(false, true)] [SerializeField] private Color minFlameColor = new Color(1f, 0.22f, 0.03f, 0.85f);
    [ColorUsage(false, true)] [SerializeField] private Color maxFlameColor = new Color(1f, 0.9f, 0.14f, 0.95f);
    [ColorUsage(false, true)] [SerializeField] private Color emberColor = new Color(1f, 0.36f, 0.08f, 0.85f);
    [Range(0.02f, 1f)] [SerializeField] private float minParticleSize = 0.08f;
    [Range(0.02f, 1.2f)] [SerializeField] private float maxParticleSize = 0.22f;
    [Range(0.01f, 0.4f)] [SerializeField] private float emberSize = 0.045f;
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.18f, 0.42f);
    [SerializeField] private int sortingOrderOffset = 3;

    [Header("Motion")]
    [SerializeField] private Vector2 upwardVelocityRange = new Vector2(0.12f, 0.55f);
    [SerializeField] private Vector2 sideVelocityRange = new Vector2(0.02f, 0.12f);
    [SerializeField] private float horizontalWindScale = 0.13f;
    [SerializeField] private float maxWindVelocity = 0.65f;
    [SerializeField] private float noiseStrength = 0.22f;
    [SerializeField] private float noiseFrequency = 0.95f;
    [SerializeField] private float layoutResponse = 18f;
    [SerializeField, HideInInspector] private int presetVersion;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private ParticleSystem topSystem;
    private ParticleSystem leftSystem;
    private ParticleSystem rightSystem;
    private ParticleSystem emberSystem;
    private Material runtimeFallbackMaterial;
    private Bounds smoothedBounds;
    private bool hasBounds;

    private void Awake()
    {
        CacheComponents();
        LoadDefaultAssetsIfNeeded();
        UpgradePresetIfNeeded();
        EnsureParticleSystems();
    }

    private void OnEnable()
    {
        CacheComponents();
        LoadDefaultAssetsIfNeeded();
        UpgradePresetIfNeeded();
        EnsureParticleSystems();
        ApplyStaticConfiguration();
        UpdateLayout(1f);
        UpdateMotion();

        if (playOnAwake)
        {
            PlayAll();
        }
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float blend = 1f - Mathf.Exp(-layoutResponse * Time.deltaTime);
        UpdateLayout(blend);
        UpdateMotion();
        SyncRendererSortingAndLayer();
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void EnsureParticleSystems()
    {
        topSystem = GetOrCreateParticleSystem(TopSystemName);
        leftSystem = GetOrCreateParticleSystem(LeftSystemName);
        rightSystem = GetOrCreateParticleSystem(RightSystemName);
        emberSystem = GetOrCreateParticleSystem(EmberSystemName);
    }

    private void CacheExistingParticleSystems()
    {
        topSystem = FindParticleSystem(TopSystemName);
        leftSystem = FindParticleSystem(LeftSystemName);
        rightSystem = FindParticleSystem(RightSystemName);
        emberSystem = FindParticleSystem(EmberSystemName);
    }

    private ParticleSystem FindParticleSystem(string systemName)
    {
        Transform child = transform.Find(systemName);
        return child != null ? child.GetComponent<ParticleSystem>() : null;
    }

    private ParticleSystem GetOrCreateParticleSystem(string systemName)
    {
        Transform child = transform.Find(systemName);
        if (child == null)
        {
            GameObject childObject = new GameObject(systemName);
            childObject.transform.SetParent(transform, false);
            childObject.layer = gameObject.layer;
            child = childObject.transform;
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        child.gameObject.layer = gameObject.layer;

        ParticleSystem particles = child.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = child.gameObject.AddComponent<ParticleSystem>();
        }

        if (child.GetComponent<ParticleSystemRenderer>() == null)
        {
            child.gameObject.AddComponent<ParticleSystemRenderer>();
        }

        return particles;
    }

    private void ApplyStaticConfiguration()
    {
        bool resumeAfterConfiguration = Application.isPlaying && playOnAwake && IsAnySystemPlaying();
        StopAllForReconfiguration();

        Material material = ResolveParticleMaterial();
        ConfigureFlameSystem(topSystem, topEmissionRate, minParticleSize, maxParticleSize, 220);
        ConfigureFlameSystem(leftSystem, sideEmissionRate, minParticleSize * 0.82f, maxParticleSize * 0.8f, 140);
        ConfigureFlameSystem(rightSystem, sideEmissionRate, minParticleSize * 0.82f, maxParticleSize * 0.8f, 140);
        ConfigureEmberSystem(emberSystem);

        ConfigureRenderer(topSystem, material, sortingOrderOffset + 2);
        ConfigureRenderer(leftSystem, material, sortingOrderOffset + 1);
        ConfigureRenderer(rightSystem, material, sortingOrderOffset + 1);
        ConfigureRenderer(emberSystem, material, sortingOrderOffset + 3);

        if (resumeAfterConfiguration)
        {
            PlayAll();
        }
    }

    private void ConfigureFlameSystem(ParticleSystem particles, float emissionRate, float minSize, float maxSize, int maxParticles)
    {
        if (particles == null)
        {
            return;
        }

        StopForReconfiguration(particles);

        var main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = playOnAwake;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-25f * Mathf.Deg2Rad, 25f * Mathf.Deg2Rad);
        main.startColor = new ParticleSystem.MinMaxGradient(minFlameColor, maxFlameColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;

        var emission = particles.emission;
        emission.enabled = emissionRate > 0f;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.randomPositionAmount = 0.2f;

        var textureSheet = particles.textureSheetAnimation;
        textureSheet.enabled = true;
        textureSheet.mode = ParticleSystemAnimationMode.Grid;
        textureSheet.numTilesX = 3;
        textureSheet.numTilesY = 3;
        textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
        textureSheet.cycleCount = 1;
        textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

        var color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateFlameFadeGradient());

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, CreateFlameSizeCurve());

        var rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        var noise = particles.noise;
        noise.enabled = noiseStrength > 0f;
        noise.strength = new ParticleSystem.MinMaxCurve(noiseStrength);
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);
    }

    private void ConfigureEmberSystem(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        StopForReconfiguration(particles);

        var main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = playOnAwake;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(emberSize * 0.55f, emberSize * 1.35f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);
        main.startColor = new ParticleSystem.MinMaxGradient(emberColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 80;

        var emission = particles.emission;
        emission.enabled = emberEmissionRate > 0f;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(emberEmissionRate);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.randomPositionAmount = 0.35f;

        var textureSheet = particles.textureSheetAnimation;
        textureSheet.enabled = true;
        textureSheet.mode = ParticleSystemAnimationMode.Grid;
        textureSheet.numTilesX = 3;
        textureSheet.numTilesY = 3;
        textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
        textureSheet.cycleCount = 1;
        textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

        var color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateEmberFadeGradient());

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, CreateEmberSizeCurve());

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(noiseStrength * 0.8f);
        noise.frequency = noiseFrequency * 1.2f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(1.0f);
    }

    private void UpdateLayout(float blend)
    {
        Bounds targetBounds = GetLocalSpriteBounds();
        if (!hasBounds)
        {
            smoothedBounds = targetBounds;
            hasBounds = true;
        }
        else
        {
            smoothedBounds.center = Vector3.Lerp(smoothedBounds.center, targetBounds.center, blend);
            smoothedBounds.extents = Vector3.Lerp(smoothedBounds.extents, targetBounds.extents, blend);
        }

        Vector3 center = smoothedBounds.center;
        Vector3 extents = smoothedBounds.extents;
        float width = Mathf.Max(0.05f, smoothedBounds.size.x + outsideOffset * 2f);
        float height = Mathf.Max(0.05f, smoothedBounds.size.y * sideHeightScale);
        float topY = center.y + extents.y + outsideOffset;
        float sideY = center.y + extents.y * 0.08f;
        float leftX = center.x - extents.x - outsideOffset;
        float rightX = center.x + extents.x + outsideOffset;

        SetShape(topSystem, new Vector3(center.x, topY, 0f), new Vector3(width, edgeBandThickness, 1f));
        SetShape(leftSystem, new Vector3(leftX, sideY, 0f), new Vector3(edgeBandThickness, height, 1f));
        SetShape(rightSystem, new Vector3(rightX, sideY, 0f), new Vector3(edgeBandThickness, height, 1f));
        SetShape(emberSystem, new Vector3(center.x, topY + outsideOffset * 0.4f, 0f), new Vector3(width * 0.92f, edgeBandThickness * 1.4f, 1f));
    }

    private void UpdateMotion()
    {
        float horizontalVelocity = body != null ? body.linearVelocity.x : 0f;
        float wind = Mathf.Clamp(-horizontalVelocity * horizontalWindScale, -maxWindVelocity, maxWindVelocity);

        SetVelocity(topSystem, wind - 0.18f, wind + 0.18f, upwardVelocityRange.x, upwardVelocityRange.y);
        SetVelocity(leftSystem, wind - sideVelocityRange.y, wind - sideVelocityRange.x, upwardVelocityRange.x * 0.7f, upwardVelocityRange.y * 0.9f);
        SetVelocity(rightSystem, wind + sideVelocityRange.x, wind + sideVelocityRange.y, upwardVelocityRange.x * 0.7f, upwardVelocityRange.y * 0.9f);
        SetVelocity(emberSystem, wind - 0.28f, wind + 0.28f, upwardVelocityRange.x * 0.75f, upwardVelocityRange.y * 1.15f);
    }

    private void SetShape(ParticleSystem particles, Vector3 position, Vector3 scale)
    {
        if (particles == null)
        {
            return;
        }

        var shape = particles.shape;
        shape.position = position;
        shape.scale = scale;
    }

    private void SetVelocity(ParticleSystem particles, float minX, float maxX, float minY, float maxY)
    {
        if (particles == null)
        {
            return;
        }

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(minX, maxX);
        velocity.y = new ParticleSystem.MinMaxCurve(minY, maxY);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    private Bounds GetLocalSpriteBounds()
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.sprite.bounds;
        }

        return new Bounds(Vector3.zero, Vector3.one);
    }

    private void ConfigureRenderer(ParticleSystem particles, Material material, int orderOffset)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (material != null)
        {
            renderer.material = material;
        }

        renderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + orderOffset : orderOffset;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.28f;
        renderer.enableGPUInstancing = true;
    }

    private void SyncRendererSortingAndLayer()
    {
        SetLayerAndSorting(topSystem, sortingOrderOffset + 2);
        SetLayerAndSorting(leftSystem, sortingOrderOffset + 1);
        SetLayerAndSorting(rightSystem, sortingOrderOffset + 1);
        SetLayerAndSorting(emberSystem, sortingOrderOffset + 3);
    }

    private void SetLayerAndSorting(ParticleSystem particles, int orderOffset)
    {
        if (particles == null || spriteRenderer == null)
        {
            return;
        }

        particles.gameObject.layer = gameObject.layer;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + orderOffset;
    }

    private Material ResolveParticleMaterial()
    {
        if (flameMaterial != null)
        {
            return flameMaterial;
        }

        if (runtimeFallbackMaterial != null)
        {
            return runtimeFallbackMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeFallbackMaterial = new Material(shader)
        {
            name = "Runtime_FireEdgeParticles"
        };

        if (fallbackFlameTexture != null)
        {
            runtimeFallbackMaterial.SetTexture("_MainTex", fallbackFlameTexture);
            if (runtimeFallbackMaterial.HasProperty("_BaseMap"))
            {
                runtimeFallbackMaterial.SetTexture("_BaseMap", fallbackFlameTexture);
            }
        }

        return runtimeFallbackMaterial;
    }

    private Gradient CreateFlameFadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(maxFlameColor, 0f),
                new GradientColorKey(maxFlameColor, 0.32f),
                new GradientColorKey(minFlameColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.12f),
                new GradientAlphaKey(0.55f, 0.58f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private Gradient CreateEmberFadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(emberColor, 0f),
                new GradientColorKey(maxFlameColor, 0.35f),
                new GradientColorKey(minFlameColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.16f),
                new GradientAlphaKey(0.4f, 0.56f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private AnimationCurve CreateFlameSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.72f, 0.72f),
            new Keyframe(1f, 0.18f));
    }

    private AnimationCurve CreateEmberSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.1f));
    }

    private void PlayAll()
    {
        Play(topSystem);
        Play(leftSystem);
        Play(rightSystem);
        Play(emberSystem);
    }

    private void Play(ParticleSystem particles)
    {
        if (particles != null && !particles.isPlaying)
        {
            particles.Play();
        }
    }

    private bool IsAnySystemPlaying()
    {
        return IsPlaying(topSystem) || IsPlaying(leftSystem) || IsPlaying(rightSystem) || IsPlaying(emberSystem);
    }

    private bool IsPlaying(ParticleSystem particles)
    {
        return particles != null && particles.isPlaying;
    }

    private void StopAllForReconfiguration()
    {
        StopForReconfiguration(topSystem);
        StopForReconfiguration(leftSystem);
        StopForReconfiguration(rightSystem);
        StopForReconfiguration(emberSystem);
    }

    private void StopForReconfiguration(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Clear(true);
    }

    private void LoadDefaultAssetsIfNeeded()
    {
#if UNITY_EDITOR
        if (flameMaterial == null)
        {
            flameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
        }

        if (fallbackFlameTexture == null)
        {
            fallbackFlameTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlameTexturePath);
        }
#endif
    }

    private void ApplyDefaultPreset()
    {
        topEmissionRate = 14f;
        sideEmissionRate = 4f;
        emberEmissionRate = 2f;
        edgeBandThickness = 0.035f;
        outsideOffset = 0.025f;
        sideHeightScale = 0.48f;
        minFlameColor = new Color(1f, 0.22f, 0.03f, 0.85f);
        maxFlameColor = new Color(1f, 0.9f, 0.14f, 0.95f);
        emberColor = new Color(1f, 0.36f, 0.08f, 0.85f);
        minParticleSize = 0.08f;
        maxParticleSize = 0.22f;
        emberSize = 0.045f;
        lifetimeRange = new Vector2(0.18f, 0.42f);
        sortingOrderOffset = 3;
        upwardVelocityRange = new Vector2(0.12f, 0.55f);
        sideVelocityRange = new Vector2(0.02f, 0.12f);
        horizontalWindScale = 0.13f;
        maxWindVelocity = 0.65f;
        noiseStrength = 0.22f;
        noiseFrequency = 0.95f;
        layoutResponse = 18f;
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

    private void Reset()
    {
        LoadDefaultAssetsIfNeeded();
        ApplyDefaultPreset();
    }

    private void OnValidate()
    {
        lifetimeRange.x = Mathf.Max(0.02f, lifetimeRange.x);
        lifetimeRange.y = Mathf.Max(lifetimeRange.x, lifetimeRange.y);
        upwardVelocityRange.y = Mathf.Max(upwardVelocityRange.x, upwardVelocityRange.y);
        sideVelocityRange.y = Mathf.Max(sideVelocityRange.x, sideVelocityRange.y);
        LoadDefaultAssetsIfNeeded();

        if (!Application.isPlaying)
        {
            return;
        }

        CacheComponents();
        CacheExistingParticleSystems();
        if (topSystem == null || leftSystem == null || rightSystem == null || emberSystem == null)
        {
            return;
        }

        ApplyStaticConfiguration();
        UpdateLayout(1f);
        UpdateMotion();
    }

    private void OnDestroy()
    {
        if (runtimeFallbackMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeFallbackMaterial);
        }
        else
        {
            DestroyImmediate(runtimeFallbackMaterial);
        }
    }
}
