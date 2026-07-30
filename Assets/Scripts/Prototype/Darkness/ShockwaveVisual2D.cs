using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShockwaveVisual2D : MonoBehaviour
{
    private const string RuntimeShaderName = "WaterAndFire/Effects/Shockwave2D";

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int ShapeModeId = Shader.PropertyToID("_ShapeMode");
    private static readonly int ArcDirectionId = Shader.PropertyToID("_ArcDirection");
    private static readonly int ArcAngleId = Shader.PropertyToID("_ArcAngle");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");

    [Header("Rendering")]
    [SerializeField] private Material ringMaterial;
    [SerializeField, Min(12)] private int circleSegments = 96;
    [SerializeField, Min(8)] private int semicircleSegments = 56;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 24;

    [Header("Ring")]
    [SerializeField, Min(0.005f)] private float ringWidth = 0.055f;
    [SerializeField, Range(0.01f, 1f)] private float softness = 0.34f;
    [SerializeField, Range(0f, 1f)] private float alpha = 0.72f;
    [SerializeField, Min(1f)] private float glowWidthMultiplier = 2.6f;
    [SerializeField, Range(0f, 1f)] private float glowAlphaMultiplier = 0.22f;
    [SerializeField, Range(0f, 0.45f)] private float edgeFade = 0.14f;

    [Header("Source Tint")]
    [SerializeField, ColorUsage(true, true)] private Color playerTint =
        new Color(0.34f, 1.1f, 1.8f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color coopTint =
        new Color(0.58f, 1.45f, 0.82f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color creatureTint =
        new Color(1.15f, 0.66f, 1.8f, 1f);

    [Header("Animation")]
    [SerializeField, Min(0.001f)] private float startRadius = 0.035f;
    [SerializeField, Range(0f, 0.4f)] private float fadeInFraction = 0.08f;
    [SerializeField, Range(0.2f, 1f)] private float fadeOutStartFraction = 0.62f;

    private LineRenderer glowRing;
    private LineRenderer coreRing;
    private Material runtimeMaterial;
    private Coroutine playRoutine;

    public void Play(ShockwaveRequest request)
    {
        if (request.Radius <= 0f || request.Duration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        EnsureRenderers(request);
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(Animate(request));
    }

    private IEnumerator Animate(ShockwaveRequest request)
    {
        float elapsed = 0f;
        while (elapsed < request.Duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / request.Duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2.1f);
            float currentRadius = Mathf.Lerp(startRadius, request.Radius, easedProgress);
            float envelope = EvaluateEnvelope(progress);

            SetRingPositions(glowRing, request, currentRadius);
            SetRingPositions(coreRing, request, currentRadius);
            ApplyColors(request, envelope);
            ApplyMaterialProperties(request, progress, envelope);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void EnsureRenderers(ShockwaveRequest request)
    {
        Material material = ResolveMaterial();
        glowRing = CreateRing("Glow", material);
        coreRing = CreateRing("Core", material);

        float widthMultiplier = Mathf.Max(0.1f, request.VisualWidthMultiplier);
        glowRing.startWidth = ringWidth * glowWidthMultiplier * widthMultiplier;
        glowRing.endWidth = glowRing.startWidth;
        coreRing.startWidth = ringWidth * widthMultiplier;
        coreRing.endWidth = coreRing.startWidth;

        ConfigureShape(glowRing, request);
        ConfigureShape(coreRing, request);
    }

    private LineRenderer CreateRing(string childName, Material material)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        LineRenderer ring = child.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.alignment = LineAlignment.TransformZ;
        ring.textureMode = LineTextureMode.Stretch;
        ring.numCapVertices = 4;
        ring.numCornerVertices = 2;
        ring.sharedMaterial = material;
        ring.sortingLayerName = sortingLayerName;
        ring.sortingOrder = sortingOrder;
        return ring;
    }

    private void ConfigureShape(LineRenderer ring, ShockwaveRequest request)
    {
        bool fullCircle = request.Shape == ShockwaveShape.FullCircle;
        ring.loop = fullCircle;
        ring.positionCount = fullCircle
            ? Mathf.Max(12, circleSegments)
            : Mathf.Max(8, semicircleSegments) + 1;
    }

    private static void SetRingPositions(
        LineRenderer ring,
        ShockwaveRequest request,
        float radius)
    {
        int pointCount = ring.positionCount;
        bool fullCircle = request.Shape == ShockwaveShape.FullCircle;
        float arcAngle = fullCircle ? 360f : Mathf.Clamp(request.ArcAngle, 1f, 360f);
        Vector2 direction = request.ArcDirection.sqrMagnitude > 0.0001f
            ? request.ArcDirection.normalized
            : Vector2.up;
        float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float startAngle = fullCircle ? 0f : centerAngle - arcAngle * 0.5f;
        float divisor = fullCircle ? pointCount : pointCount - 1f;

        for (int index = 0; index < pointCount; index++)
        {
            float normalized = index / divisor;
            float angle = (startAngle + normalized * arcAngle) * Mathf.Deg2Rad;
            ring.SetPosition(index, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f));
        }
    }

    private void ApplyColors(ShockwaveRequest request, float envelope)
    {
        Color tint = GetTint(request.SourceType);
        coreRing.colorGradient = BuildGradient(
            tint,
            alpha * envelope,
            request.Shape != ShockwaveShape.FullCircle);
        glowRing.colorGradient = BuildGradient(
            tint,
            alpha * glowAlphaMultiplier * envelope,
            request.Shape != ShockwaveShape.FullCircle);
    }

    private Gradient BuildGradient(Color tint, float currentAlpha, bool fadeEnds)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(tint, 0f),
                new GradientColorKey(tint, 1f)
            },
            fadeEnds
                ? new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(currentAlpha, edgeFade),
                    new GradientAlphaKey(currentAlpha, 1f - edgeFade),
                    new GradientAlphaKey(0f, 1f)
                }
                : new[]
                {
                    new GradientAlphaKey(currentAlpha, 0f),
                    new GradientAlphaKey(currentAlpha, 1f)
                });
        return gradient;
    }

    private void ApplyMaterialProperties(
        ShockwaveRequest request,
        float progress,
        float envelope)
    {
        var block = new MaterialPropertyBlock();
        block.SetFloat(ProgressId, progress);
        block.SetFloat(RingWidthId, ringWidth * request.VisualWidthMultiplier);
        block.SetFloat(SoftnessId, softness);
        block.SetFloat(AlphaId, envelope);
        block.SetColor(TintId, Color.white);
        block.SetFloat(ShapeModeId, request.Shape == ShockwaveShape.FullCircle ? 0f : 1f);
        block.SetVector(ArcDirectionId, request.ArcDirection);
        block.SetFloat(ArcAngleId, request.ArcAngle);
        block.SetFloat(EdgeFadeId, edgeFade);
        glowRing.SetPropertyBlock(block);
        coreRing.SetPropertyBlock(block);
    }

    private float EvaluateEnvelope(float progress)
    {
        float fadeIn = fadeInFraction <= 0f
            ? 1f
            : Mathf.Clamp01(progress / fadeInFraction);
        float fadeOut = progress <= fadeOutStartFraction
            ? 1f
            : 1f - Mathf.InverseLerp(fadeOutStartFraction, 1f, progress);
        return Mathf.SmoothStep(0f, 1f, fadeIn) * Mathf.SmoothStep(0f, 1f, fadeOut);
    }

    private Color GetTint(ShockwaveSourceType sourceType)
    {
        switch (sourceType)
        {
            case ShockwaveSourceType.CoopPlayer:
                return coopTint;
            case ShockwaveSourceType.CreatureSnore:
                return creatureTint;
            default:
                return playerTint;
        }
    }

    private Material ResolveMaterial()
    {
        if (ringMaterial != null)
        {
            return ringMaterial;
        }

        Shader shader = Shader.Find(RuntimeShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime_Shockwave2D"
        };
        return runtimeMaterial;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void OnValidate()
    {
        circleSegments = Mathf.Max(12, circleSegments);
        semicircleSegments = Mathf.Max(8, semicircleSegments);
        ringWidth = Mathf.Max(0.005f, ringWidth);
        glowWidthMultiplier = Mathf.Max(1f, glowWidthMultiplier);
        startRadius = Mathf.Max(0.001f, startRadius);
        fadeInFraction = Mathf.Clamp(fadeInFraction, 0f, 0.4f);
        fadeOutStartFraction = Mathf.Clamp(fadeOutStartFraction, 0.2f, 1f);
        edgeFade = Mathf.Clamp(edgeFade, 0f, 0.45f);
    }
}
