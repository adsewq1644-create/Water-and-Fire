using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrumbleDustWarningVisual2D : MonoBehaviour
{
    private enum EdgeRegion
    {
        Top,
        Left,
        Right,
        Bottom
    }

    private readonly struct EdgeSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly Vector2 Normal;
        public readonly EdgeRegion Region;

        public EdgeSegment(Vector2 start, Vector2 end, Vector2 normal, EdgeRegion region)
        {
            Start = start;
            End = end;
            Normal = normal;
            Region = region;
        }

        public float Length => Vector2.Distance(Start, End);
    }

    private readonly struct DustPoint
    {
        public readonly Vector3 LocalPosition;
        public readonly float BaseAlpha;
        public readonly float BaseSize;
        public readonly float RevealThreshold;
        public readonly float FlickerPhase;

        public DustPoint(
            Vector3 localPosition,
            float baseAlpha,
            float baseSize,
            float revealThreshold,
            float flickerPhase)
        {
            LocalPosition = localPosition;
            BaseAlpha = baseAlpha;
            BaseSize = baseSize;
            RevealThreshold = revealThreshold;
            FlickerPhase = flickerPhase;
        }
    }

    private sealed class DustGroupRuntime
    {
        public readonly EdgeRegion Region;
        public readonly float PhaseOffset;
        public readonly List<DustPoint> Points = new List<DustPoint>(64);

        public Transform Root;
        public ParticleSystem Particles;
        public ParticleSystem.Particle[] Buffer = new ParticleSystem.Particle[0];
        public Vector3 BaseLocalPosition;

        public DustGroupRuntime(EdgeRegion region, float phaseOffset)
        {
            Region = region;
            PhaseOffset = phaseOffset;
        }
    }

    private readonly struct FallingSource
    {
        public readonly DustGroupRuntime Group;
        public readonly int PointIndex;

        public FallingSource(DustGroupRuntime group, int pointIndex)
        {
            Group = group;
            PointIndex = pointIndex;
        }
    }

    [Header("References")]
    [SerializeField] private Transform warningVisualRoot;
    [SerializeField] private Transform topDustGroup;
    [SerializeField] private Transform leftDustGroup;
    [SerializeField] private Transform rightDustGroup;
    [SerializeField] private Transform bottomDustGroup;
    [SerializeField] private ParticleSystem landingBurstParticles;
    [SerializeField] private ParticleSystem fallingDustParticles;
    [SerializeField] private Collider2D[] platformColliders;
    [SerializeField] private Material particleMaterial;

    [Header("Shake")]
    [SerializeField, Min(0f)] private float startShakeAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float endShakeAmplitude = 0.17f;
    [SerializeField, Min(0.1f)] private float startShakeFrequency = 2.2f;
    [SerializeField, Min(0.1f)] private float endShakeFrequency = 8f;
    [SerializeField, Range(0f, 0.5f)] private float verticalMotionRatio = 0.15f;
    [SerializeField, Range(0f, 0.25f)] private float secondaryNoiseAmount = 0.08f;
    [SerializeField] private AnimationCurve warningIntensityCurve =
        AnimationCurve.EaseInOut(0f, 0.68f, 1f, 1.2f);
    [SerializeField] private AnimationCurve shakeAmplitudeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve shakeFrequencyCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Edge Intensity")]
    [SerializeField, Range(0f, 2f)] private float topIntensityMultiplier = 1f;
    [SerializeField, Range(0f, 2f)] private float sideIntensityMultiplier = 0.65f;
    [SerializeField, Range(0f, 2f)] private float bottomIntensityMultiplier = 0.4f;

    [Header("Outline Dust Layer")]
    [SerializeField, ColorUsage(true, true)] private Color dustColor =
        new Color(1f, 0.94f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float outlineAlpha = 0.78f;
    [SerializeField, Min(0.03f)] private float outlineParticleSpacing = 0.105f;
    [SerializeField, Min(0.005f)] private float outlineParticleSizeMin = 0.035f;
    [SerializeField, Min(0.005f)] private float outlineParticleSizeMax = 0.085f;
    [SerializeField, Range(0f, 0.1f)] private float outlineThickness = 0.025f;
    [SerializeField, Range(0f, 0.8f)] private float outlineGapAmount = 0.3f;

    [Header("Dust")]
    [SerializeField, Range(0, 64)] private int landingBurstCount = 14;
    [SerializeField, Range(0, 64)] private int respawnBurstCount = 7;
    [SerializeField, Range(0f, 1f)] private float respawnBurstAlphaMultiplier = 0.48f;
    [SerializeField, Min(0f)] private float fallingDustStartRate = 2.5f;
    [SerializeField, Min(0f)] private float fallingDustEndRate = 16f;
    [SerializeField, Min(1f)] private float preBreakDustRateMultiplier = 2.1f;
    [SerializeField, Range(0, 96)] private int breakReleaseBurstCount = 26;
    [SerializeField, Min(0.1f)] private float breakAfterimageDuration = 0.7f;
    [SerializeField, Min(0.05f)] private float fallingDustLifetimeMin = 0.55f;
    [SerializeField, Min(0.05f)] private float fallingDustLifetimeMax = 1.1f;
    [SerializeField, Min(0.005f)] private float fallingDustSizeMin = 0.025f;
    [SerializeField, Min(0.005f)] private float fallingDustSizeMax = 0.065f;
    [SerializeField, Min(0f)] private float fallingDustSpeedMin = 0.35f;
    [SerializeField, Min(0f)] private float fallingDustSpeedMax = 0.9f;

    [Header("Pre-Break")]
    [SerializeField, Range(0.5f, 0.95f)] private float preBreakStartNormalizedTime = 0.72f;
    [SerializeField, Range(1, 6)] private int preBreakBlinkCount = 2;
    [SerializeField, Min(0.1f)] private float preBreakBlinkSpeed = 1f;

    [Header("Rendering")]
    [SerializeField] private int particleSortingOrder = 5;

    private readonly List<EdgeSegment> edgeSegments = new List<EdgeSegment>(32);
    private readonly List<FallingSource> fallingSources = new List<FallingSource>(192);

    private DustGroupRuntime topRuntime;
    private DustGroupRuntime leftRuntime;
    private DustGroupRuntime rightRuntime;
    private DustGroupRuntime bottomRuntime;
    private DustGroupRuntime[] groups;

    private Material runtimeParticleMaterial;
    private float warningDuration = 2f;
    private float warningElapsed;
    private float warningProgress;
    private float shakePhase;
    private float fallingEmissionAccumulator;
    private float releaseStartedAt;
    private float lastAppliedIntensity = 1f;
    private int emissionSequence;
    private bool warningActive;
    private bool releaseActive;
    private bool preBreakForced;
    private bool landingBurstPlayed;

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeHierarchy();
        StopAndReset();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        EnsureRuntimeHierarchy();
        StopAndReset();
    }

    private void Update()
    {
        if (!releaseActive)
        {
            return;
        }

        float normalized = Mathf.Clamp01(
            (Time.time - releaseStartedAt) / Mathf.Max(0.1f, breakAfterimageDuration));
        ApplyOutlineAppearance(warningProgress, lastAppliedIntensity, 1f - normalized);
        if (normalized >= 1f)
        {
            releaseActive = false;
            ClearOutlineParticles();
            RestoreGroupPositions();
        }
    }

    private void OnDisable()
    {
        StopAndReset();
    }

    private void OnDestroy()
    {
        if (runtimeParticleMaterial != null)
        {
            Destroy(runtimeParticleMaterial);
        }
    }

    public void PlayLandingBurst()
    {
        PlayLandingBurst(FindTopCenter());
    }

    public void PlayLandingBurst(Vector2 worldPosition)
    {
        if (landingBurstPlayed || !isActiveAndEnabled)
        {
            return;
        }

        EnsureRuntimeHierarchy();
        landingBurstPlayed = true;
        EmitSurfaceBurst(worldPosition, landingBurstCount, 1f, 1f);
    }

    public void PlayRespawnBurst()
    {
        if (!isActiveAndEnabled || respawnBurstCount <= 0)
        {
            return;
        }

        ResolveReferences();
        EnsureRuntimeHierarchy();
        NormalizeVisualRootScale();
        EmitSurfaceBurst(
            FindTopCenter(),
            respawnBurstCount,
            respawnBurstAlphaMultiplier,
            0.62f);
    }

    public void PlayImmediateBreak(Vector2 worldImpactPosition)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!warningActive)
        {
            BeginWarning(Mathf.Max(0.1f, breakAfterimageDuration));
        }

        preBreakForced = true;
        SetWarningProgress(1f);
        EmitImmediateImpactDust(worldImpactPosition);
        PlayBreakRelease();
    }

    private void EmitSurfaceBurst(
        Vector2 worldPosition,
        int count,
        float alphaMultiplier,
        float speedMultiplier)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(25f, 155f, Hash01(emissionSequence++));
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float speed =
                Mathf.Lerp(0.25f, 0.75f, Hash01(emissionSequence++)) *
                speedMultiplier;
            Vector2 position = worldPosition + RandomDisc(emissionSequence++) * 0.06f;
            EmitWorldParticle(
                landingBurstParticles,
                position,
                direction * speed,
                Mathf.Lerp(outlineParticleSizeMin, outlineParticleSizeMax, Hash01(emissionSequence++)),
                Mathf.Lerp(0.35f, 0.7f, Hash01(emissionSequence++)),
                outlineAlpha * alphaMultiplier);
        }
    }

    private void EmitImmediateImpactDust(Vector2 worldPosition)
    {
        int count = Mathf.Max(6, breakReleaseBurstCount / 3);
        for (int i = 0; i < count; i++)
        {
            float horizontal = Mathf.Lerp(-0.24f, 0.24f, Hash01(emissionSequence++));
            float fallSpeed = Mathf.Lerp(
                Mathf.Max(0.45f, fallingDustSpeedMin),
                Mathf.Max(1.15f, fallingDustSpeedMax),
                Hash01(emissionSequence++));
            Vector2 position = worldPosition + RandomDisc(emissionSequence++) * 0.075f;
            EmitWorldParticle(
                fallingDustParticles,
                position,
                new Vector2(horizontal, -fallSpeed),
                Mathf.Lerp(fallingDustSizeMin, fallingDustSizeMax, Hash01(emissionSequence++)),
                Mathf.Lerp(fallingDustLifetimeMin, fallingDustLifetimeMax, Hash01(emissionSequence++)),
                outlineAlpha);
        }
    }

    public void BeginWarning(float totalDuration)
    {
        ResolveReferences();
        EnsureRuntimeHierarchy();
        NormalizeVisualRootScale();

        warningDuration = Mathf.Max(0.1f, totalDuration);
        warningElapsed = 0f;
        warningProgress = 0f;
        shakePhase = 0f;
        fallingEmissionAccumulator = 0f;
        releaseStartedAt = 0f;
        lastAppliedIntensity = 1f;
        emissionSequence = 0;
        warningActive = true;
        releaseActive = false;
        preBreakForced = false;
        landingBurstPlayed = false;

        RestoreGroupPositions();
        BuildEdgeSegments();
        BuildOutlineDustLayer();
        SetWarningProgress(0f);
    }

    public void SetWarningProgress(float normalizedProgress)
    {
        if (!warningActive)
        {
            return;
        }

        warningProgress = Mathf.Clamp01(normalizedProgress);
        warningElapsed += Time.deltaTime;

        float amplitudeProgress = EvaluateCurve(shakeAmplitudeCurve, warningProgress);
        float frequencyProgress = EvaluateCurve(shakeFrequencyCurve, warningProgress);
        float amplitude = Mathf.Lerp(startShakeAmplitude, endShakeAmplitude, amplitudeProgress);
        float frequency = Mathf.Lerp(startShakeFrequency, endShakeFrequency, frequencyProgress);
        shakePhase += Time.deltaTime * frequency * Mathf.PI * 2f;

        float intensity = Mathf.Max(0f, EvaluateCurve(warningIntensityCurve, warningProgress));
        if (warningProgress >= preBreakStartNormalizedTime || preBreakForced)
        {
            float preBreakProgress = preBreakForced
                ? 1f
                : Mathf.InverseLerp(preBreakStartNormalizedTime, 1f, warningProgress);
            intensity *= Mathf.Lerp(1f, 1.28f, preBreakProgress);
        }
        lastAppliedIntensity = intensity;

        MoveDustGroups(amplitude);
        ApplyOutlineAppearance(warningProgress, intensity, 1f);
        EmitFallingDust(Time.deltaTime);
    }

    public void PlayPreBreakWarning()
    {
        preBreakForced = true;
    }

    public void PlayBreakRelease()
    {
        if (!warningActive && !releaseActive)
        {
            return;
        }

        warningActive = false;
        releaseActive = true;
        releaseStartedAt = Time.time;
        ReleaseOutlineParticles();
        EmitBreakReleaseBurst();
    }

    public void StopAndReset()
    {
        warningActive = false;
        releaseActive = false;
        preBreakForced = false;
        landingBurstPlayed = false;
        warningElapsed = 0f;
        warningProgress = 0f;
        shakePhase = 0f;
        fallingEmissionAccumulator = 0f;
        releaseStartedAt = 0f;
        lastAppliedIntensity = 1f;

        ClearOutlineParticles();
        StopAndClear(landingBurstParticles);
        StopAndClear(fallingDustParticles);
        RestoreGroupPositions();
    }

    private void MoveDustGroups(float amplitude)
    {
        if (groups == null)
        {
            return;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            DustGroupRuntime group = groups[i];
            if (group?.Root == null)
            {
                continue;
            }

            float phase = shakePhase + group.PhaseOffset * Mathf.PI * 2f;
            float smoothNoise = Mathf.PerlinNoise(
                17.3f + i * 4.7f,
                warningElapsed * 3.1f) * 2f - 1f;
            float horizontalOffset =
                Mathf.Sin(phase) * amplitude +
                smoothNoise * amplitude * secondaryNoiseAmount;
            float verticalOffset =
                Mathf.Sin(phase * 0.73f + 0.6f) *
                amplitude *
                verticalMotionRatio;

            group.Root.localPosition =
                group.BaseLocalPosition +
                new Vector3(horizontalOffset, verticalOffset, 0f);
        }
    }

    private void ApplyOutlineAppearance(float progress, float intensity, float fadeMultiplier)
    {
        if (groups == null)
        {
            return;
        }

        float preBreakProgress = progress >= preBreakStartNormalizedTime
            ? Mathf.InverseLerp(preBreakStartNormalizedTime, 1f, progress)
            : 0f;
        float blinkPhase =
            preBreakProgress *
            preBreakBlinkCount *
            Mathf.PI *
            2f *
            preBreakBlinkSpeed;
        float topBlink = preBreakProgress > 0f
            ? Mathf.Lerp(0.72f, 1.3f, 0.5f + 0.5f * Mathf.Sin(blinkPhase))
            : 1f;

        for (int i = 0; i < groups.Length; i++)
        {
            DustGroupRuntime group = groups[i];
            if (group?.Particles == null || group.Points.Count == 0)
            {
                continue;
            }

            int count = group.Particles.GetParticles(group.Buffer);
            int usableCount = Mathf.Min(count, group.Points.Count);
            float regionIntensity = GetRegionIntensity(group.Region);
            float regionBlink = group.Region == EdgeRegion.Top ? topBlink : 1f;

            for (int particleIndex = 0; particleIndex < usableCount; particleIndex++)
            {
                DustPoint point = group.Points[particleIndex];
                ParticleSystem.Particle particle = group.Buffer[particleIndex];
                float reveal = progress + 0.001f >= point.RevealThreshold ? 1f : 0f;
                float flicker =
                    0.84f +
                    0.16f *
                    Mathf.Sin(
                        warningElapsed * 2.2f +
                        point.FlickerPhase +
                        group.PhaseOffset * Mathf.PI * 2f);
                float alpha =
                    outlineAlpha *
                    point.BaseAlpha *
                    regionIntensity *
                    intensity *
                    regionBlink *
                    flicker *
                    reveal *
                    fadeMultiplier;

                Color color = dustColor;
                color.a = Mathf.Clamp01(alpha);
                particle.startColor = color;
                particle.startSize =
                    point.BaseSize *
                    Mathf.Lerp(0.95f, 1.12f, progress) *
                    (1f + Mathf.Sin(point.FlickerPhase + warningElapsed * 1.4f) * 0.04f);
                group.Buffer[particleIndex] = particle;
            }

            group.Particles.SetParticles(group.Buffer, count);
        }
    }

    private void EmitFallingDust(float deltaTime)
    {
        if (fallingSources.Count == 0 || fallingDustParticles == null)
        {
            return;
        }

        float acceleration = warningProgress * warningProgress;
        float rate = Mathf.Lerp(fallingDustStartRate, fallingDustEndRate, acceleration);
        if (warningProgress >= preBreakStartNormalizedTime || preBreakForced)
        {
            float preBreakProgress = preBreakForced
                ? 1f
                : Mathf.InverseLerp(preBreakStartNormalizedTime, 1f, warningProgress);
            rate *= Mathf.Lerp(1f, preBreakDustRateMultiplier, preBreakProgress);
        }

        fallingEmissionAccumulator += Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime);
        int emitCount = Mathf.FloorToInt(fallingEmissionAccumulator);
        fallingEmissionAccumulator -= emitCount;

        for (int i = 0; i < emitCount; i++)
        {
            EmitFallingParticle(1f);
        }
    }

    private void EmitFallingParticle(float intensity)
    {
        if (fallingSources.Count == 0)
        {
            return;
        }

        int index = Mathf.Clamp(
            Mathf.FloorToInt(Hash01(emissionSequence++) * fallingSources.Count),
            0,
            fallingSources.Count - 1);
        FallingSource source = fallingSources[index];
        if (source.Group?.Root == null ||
            source.PointIndex < 0 ||
            source.PointIndex >= source.Group.Points.Count)
        {
            return;
        }

        DustPoint point = source.Group.Points[source.PointIndex];
        Vector2 position = source.Group.Root.TransformPoint(point.LocalPosition);
        position += RandomDisc(emissionSequence++) * 0.025f;
        float horizontal = Mathf.Lerp(-0.08f, 0.08f, Hash01(emissionSequence++));
        float fallSpeed = Mathf.Lerp(
            fallingDustSpeedMin,
            fallingDustSpeedMax,
            Hash01(emissionSequence++));
        float alpha = Mathf.Clamp01(
            outlineAlpha *
            GetRegionIntensity(source.Group.Region) *
            0.58f *
            intensity);

        EmitWorldParticle(
            fallingDustParticles,
            position,
            new Vector2(horizontal, -fallSpeed),
            Mathf.Lerp(fallingDustSizeMin, fallingDustSizeMax, Hash01(emissionSequence++)),
            Mathf.Lerp(fallingDustLifetimeMin, fallingDustLifetimeMax, Hash01(emissionSequence++)),
            alpha);
    }

    private void EmitBreakReleaseBurst()
    {
        for (int i = 0; i < breakReleaseBurstCount; i++)
        {
            EmitFallingParticle(1.15f);
        }
    }

    private void ReleaseOutlineParticles()
    {
        if (groups == null)
        {
            return;
        }

        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            DustGroupRuntime group = groups[groupIndex];
            if (group?.Particles == null)
            {
                continue;
            }

            int count = group.Particles.GetParticles(group.Buffer);
            Vector3 localDown = group.Root.InverseTransformVector(Vector3.down).normalized;
            Vector3 localRight = group.Root.InverseTransformVector(Vector3.right).normalized;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = group.Buffer[i];
                float fallSpeed = Mathf.Lerp(
                    fallingDustSpeedMin,
                    fallingDustSpeedMax,
                    Hash01(emissionSequence++));
                float drift = Mathf.Lerp(-0.12f, 0.12f, Hash01(emissionSequence++));
                particle.velocity = localDown * fallSpeed + localRight * drift;
                particle.remainingLifetime =
                    breakAfterimageDuration *
                    Mathf.Lerp(0.72f, 1f, Hash01(emissionSequence++));
                group.Buffer[i] = particle;
            }
            group.Particles.SetParticles(group.Buffer, count);
        }
    }

    private void BuildOutlineDustLayer()
    {
        ClearOutlineParticles();
        fallingSources.Clear();

        for (int segmentIndex = 0; segmentIndex < edgeSegments.Count; segmentIndex++)
        {
            EdgeSegment segment = edgeSegments[segmentIndex];
            DustGroupRuntime group = GetGroup(segment.Region);
            if (group?.Root == null || group.Particles == null)
            {
                continue;
            }

            float spacingMultiplier = segment.Region switch
            {
                EdgeRegion.Top => 0.78f,
                EdgeRegion.Bottom => 1.35f,
                _ => 1.05f
            };
            float spacing = Mathf.Max(0.03f, outlineParticleSpacing * spacingMultiplier);
            int samples = Mathf.Max(2, Mathf.CeilToInt(segment.Length / spacing) + 1);
            Vector2 tangent = (segment.End - segment.Start).normalized;

            for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
            {
                float t = samples <= 1 ? 0.5f : sampleIndex / (float)(samples - 1);
                float gapNoise = Mathf.PerlinNoise(
                    7.17f + segmentIndex * 1.83f,
                    t * 4.2f + (int)segment.Region * 3.4f);
                float regionGap = segment.Region switch
                {
                    EdgeRegion.Top => outlineGapAmount * 0.62f,
                    EdgeRegion.Bottom => Mathf.Min(0.82f, outlineGapAmount * 1.45f),
                    _ => Mathf.Min(0.8f, outlineGapAmount * 1.08f)
                };
                if (gapNoise < regionGap)
                {
                    continue;
                }

                int hashSeed = segmentIndex * 1009 + sampleIndex * 37 + (int)segment.Region * 7919;
                float tangentJitter = Mathf.Lerp(-spacing * 0.18f, spacing * 0.18f, Hash01(hashSeed));
                float normalJitter = Mathf.Lerp(
                    -outlineThickness * 0.35f,
                    outlineThickness,
                    Hash01(hashSeed + 1));
                Vector2 worldPosition =
                    Vector2.Lerp(segment.Start, segment.End, t) +
                    tangent * tangentJitter +
                    segment.Normal * normalJitter;
                Vector3 localPosition = group.Root.InverseTransformPoint(worldPosition);
                float threshold = Hash01(hashSeed + 2) < 0.64f
                    ? 0f
                    : Mathf.Lerp(0.12f, 0.68f, Hash01(hashSeed + 3));
                float baseAlpha = Mathf.Lerp(0.72f, 1f, Hash01(hashSeed + 4));
                float baseSize = Mathf.Lerp(
                    outlineParticleSizeMin,
                    outlineParticleSizeMax,
                    Hash01(hashSeed + 5));
                float flickerPhase = Hash01(hashSeed + 6) * Mathf.PI * 2f;

                int pointIndex = group.Points.Count;
                group.Points.Add(
                    new DustPoint(
                        localPosition,
                        baseAlpha,
                        baseSize,
                        threshold,
                        flickerPhase));
                fallingSources.Add(new FallingSource(group, pointIndex));
            }
        }

        EmitStaticGroupParticles(topRuntime);
        EmitStaticGroupParticles(leftRuntime);
        EmitStaticGroupParticles(rightRuntime);
        EmitStaticGroupParticles(bottomRuntime);
    }

    private void EmitStaticGroupParticles(DustGroupRuntime group)
    {
        if (group?.Particles == null || group.Points.Count == 0)
        {
            return;
        }

        group.Buffer = new ParticleSystem.Particle[group.Points.Count];
        float lifetime = warningDuration + breakAfterimageDuration + 1.5f;
        for (int i = 0; i < group.Points.Count; i++)
        {
            DustPoint point = group.Points[i];
            Color color = dustColor;
            color.a = 0f;
            var emit = new ParticleSystem.EmitParams
            {
                position = point.LocalPosition,
                velocity = Vector3.zero,
                startSize = point.BaseSize,
                startLifetime = lifetime,
                startColor = color
            };
            group.Particles.Emit(emit, 1);
        }

        group.Particles.GetParticles(group.Buffer);
    }

    private void BuildEdgeSegments()
    {
        edgeSegments.Clear();
        ResolveReferences();
        if (platformColliders == null)
        {
            return;
        }

        for (int i = 0; i < platformColliders.Length; i++)
        {
            Collider2D platformCollider = platformColliders[i];
            if (platformCollider == null || !platformCollider.enabled || platformCollider.isTrigger)
            {
                continue;
            }

            switch (platformCollider)
            {
                case BoxCollider2D box:
                    AddBoxEdges(box);
                    break;
                case PolygonCollider2D polygon:
                    AddPolygonEdges(polygon);
                    break;
                case EdgeCollider2D edge:
                    AddEdgeColliderEdges(edge);
                    break;
                case CompositeCollider2D composite:
                    AddCompositeEdges(composite);
                    break;
                default:
                    AddBoundsEdges(platformCollider.bounds);
                    break;
            }
        }
    }

    private void AddBoxEdges(BoxCollider2D box)
    {
        Vector2 half = box.size * 0.5f;
        Vector2 offset = box.offset;
        Vector2 bottomLeft = box.transform.TransformPoint(offset + new Vector2(-half.x, -half.y));
        Vector2 bottomRight = box.transform.TransformPoint(offset + new Vector2(half.x, -half.y));
        Vector2 topRight = box.transform.TransformPoint(offset + new Vector2(half.x, half.y));
        Vector2 topLeft = box.transform.TransformPoint(offset + new Vector2(-half.x, half.y));

        AddSegment(bottomLeft, bottomRight, -box.transform.up, EdgeRegion.Bottom);
        AddSegment(bottomRight, topRight, box.transform.right, EdgeRegion.Right);
        AddSegment(topRight, topLeft, box.transform.up, EdgeRegion.Top);
        AddSegment(topLeft, bottomLeft, -box.transform.right, EdgeRegion.Left);
    }

    private void AddPolygonEdges(PolygonCollider2D polygon)
    {
        for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
        {
            Vector2[] path = polygon.GetPath(pathIndex);
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 start = polygon.transform.TransformPoint(path[i] + polygon.offset);
                Vector2 end = polygon.transform.TransformPoint(
                    path[(i + 1) % path.Length] + polygon.offset);
                AddAutomaticallyClassifiedSegment(start, end, polygon.bounds);
            }
        }
    }

    private void AddEdgeColliderEdges(EdgeCollider2D edge)
    {
        Vector2[] points = edge.points;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 start = edge.transform.TransformPoint(points[i] + edge.offset);
            Vector2 end = edge.transform.TransformPoint(points[i + 1] + edge.offset);
            AddAutomaticallyClassifiedSegment(start, end, edge.bounds);
        }
    }

    private void AddCompositeEdges(CompositeCollider2D composite)
    {
        var points = new Vector2[composite.pointCount];
        for (int pathIndex = 0; pathIndex < composite.pathCount; pathIndex++)
        {
            int count = composite.GetPath(pathIndex, points);
            for (int i = 0; i < count - 1; i++)
            {
                Vector2 start = composite.transform.TransformPoint(points[i] + composite.offset);
                Vector2 end = composite.transform.TransformPoint(points[i + 1] + composite.offset);
                AddAutomaticallyClassifiedSegment(start, end, composite.bounds);
            }
        }
    }

    private void AddBoundsEdges(Bounds bounds)
    {
        Vector2 bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        Vector2 bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        Vector2 topRight = new Vector2(bounds.max.x, bounds.max.y);
        Vector2 topLeft = new Vector2(bounds.min.x, bounds.max.y);

        AddSegment(bottomLeft, bottomRight, Vector2.down, EdgeRegion.Bottom);
        AddSegment(bottomRight, topRight, Vector2.right, EdgeRegion.Right);
        AddSegment(topRight, topLeft, Vector2.up, EdgeRegion.Top);
        AddSegment(topLeft, bottomLeft, Vector2.left, EdgeRegion.Left);
    }

    private void AddAutomaticallyClassifiedSegment(Vector2 start, Vector2 end, Bounds bounds)
    {
        Vector2 midpoint = (start + end) * 0.5f;
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        Vector2 radial = midpoint - (Vector2)bounds.center;
        if (Vector2.Dot(normal, radial) < 0f)
        {
            normal = -normal;
        }

        EdgeRegion region = ClassifyRegion(normal);
        AddSegment(start, end, normal, region);
    }

    private EdgeRegion ClassifyRegion(Vector2 worldNormal)
    {
        float up = Vector2.Dot(worldNormal.normalized, transform.up);
        float right = Vector2.Dot(worldNormal.normalized, transform.right);
        if (Mathf.Abs(up) >= Mathf.Abs(right))
        {
            return up >= 0f ? EdgeRegion.Top : EdgeRegion.Bottom;
        }
        return right >= 0f ? EdgeRegion.Right : EdgeRegion.Left;
    }

    private void AddSegment(
        Vector2 start,
        Vector2 end,
        Vector2 normal,
        EdgeRegion region)
    {
        if ((end - start).sqrMagnitude < 0.0001f)
        {
            return;
        }

        edgeSegments.Add(
            new EdgeSegment(
                start,
                end,
                normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up,
                region));
    }

    private void ResolveReferences()
    {
        if (platformColliders == null || platformColliders.Length == 0)
        {
            platformColliders = GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void EnsureRuntimeHierarchy()
    {
        if (warningVisualRoot == null)
        {
            warningVisualRoot = FindOrCreateTransform(transform, "CrumbleWarningVisualRoot");
        }

        NormalizeVisualRootScale();

        topDustGroup = ResolveOrCreateGroup(topDustGroup, "TopDustGroup");
        leftDustGroup = ResolveOrCreateGroup(leftDustGroup, "LeftDustGroup");
        rightDustGroup = ResolveOrCreateGroup(rightDustGroup, "RightDustGroup");
        bottomDustGroup = ResolveOrCreateGroup(bottomDustGroup, "BottomDustGroup");

        topRuntime ??= new DustGroupRuntime(EdgeRegion.Top, 0f);
        leftRuntime ??= new DustGroupRuntime(EdgeRegion.Left, 0.15f);
        rightRuntime ??= new DustGroupRuntime(EdgeRegion.Right, -0.12f);
        bottomRuntime ??= new DustGroupRuntime(EdgeRegion.Bottom, 0.25f);

        ConfigureGroup(topRuntime, topDustGroup);
        ConfigureGroup(leftRuntime, leftDustGroup);
        ConfigureGroup(rightRuntime, rightDustGroup);
        ConfigureGroup(bottomRuntime, bottomDustGroup);
        groups = new[] { topRuntime, leftRuntime, rightRuntime, bottomRuntime };

        landingBurstParticles = ResolveOrCreateWorldParticles(
            landingBurstParticles,
            "LandingBurstParticles");
        fallingDustParticles = ResolveOrCreateWorldParticles(
            fallingDustParticles,
            "FallingDustParticles");
    }

    private Transform ResolveOrCreateGroup(Transform current, string objectName)
    {
        Transform result = current != null
            ? current
            : FindOrCreateTransform(warningVisualRoot, objectName);
        if (result.GetComponent<ParticleSystem>() == null)
        {
            result.gameObject.AddComponent<ParticleSystem>();
        }
        return result;
    }

    private void ConfigureGroup(DustGroupRuntime group, Transform root)
    {
        group.Root = root;
        group.BaseLocalPosition = root != null ? root.localPosition : Vector3.zero;
        group.Particles = root != null ? root.GetComponent<ParticleSystem>() : null;
        ConfigureParticleSystem(group.Particles, ParticleSystemSimulationSpace.Local, 320);
    }

    private ParticleSystem ResolveOrCreateWorldParticles(
        ParticleSystem current,
        string objectName)
    {
        if (current != null)
        {
            ConfigureParticleSystem(current, ParticleSystemSimulationSpace.World, 512);
            return current;
        }

        Transform root = FindOrCreateTransform(warningVisualRoot, objectName);
        ParticleSystem particles = root.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = root.gameObject.AddComponent<ParticleSystem>();
        }
        ConfigureParticleSystem(particles, ParticleSystemSimulationSpace.World, 512);
        return particles;
    }

    private void ConfigureParticleSystem(
        ParticleSystem particles,
        ParticleSystemSimulationSpace simulationSpace,
        int maxParticles)
    {
        if (particles == null)
        {
            return;
        }

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = simulationSpace;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = maxParticles;
        main.gravityModifier = 0f;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var noise = particles.noise;
        noise.enabled = false;
        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = false;
        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = false;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = particleSortingOrder;
        SpriteRenderer referenceRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (referenceRenderer != null)
        {
            renderer.sortingLayerID = referenceRenderer.sortingLayerID;
        }
        renderer.sharedMaterial = particleMaterial != null
            ? particleMaterial
            : GetRuntimeParticleMaterial();
    }

    private Material GetRuntimeParticleMaterial()
    {
        if (runtimeParticleMaterial != null)
        {
            return runtimeParticleMaterial;
        }

        Shader shader = Shader.Find("WaterAndFire/AmbientDust2D");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        runtimeParticleMaterial = shader != null ? new Material(shader) : null;
        if (runtimeParticleMaterial != null)
        {
            runtimeParticleMaterial.name = "CrumbleDustWarning_Runtime";
        }
        return runtimeParticleMaterial;
    }

    private void NormalizeVisualRootScale()
    {
        if (warningVisualRoot == null || warningVisualRoot.parent == null)
        {
            return;
        }

        Vector3 parentScale = warningVisualRoot.parent.lossyScale;
        warningVisualRoot.localScale = new Vector3(
            SafeInverse(parentScale.x),
            SafeInverse(parentScale.y),
            SafeInverse(parentScale.z));
    }

    private void RestoreGroupPositions()
    {
        if (groups == null)
        {
            return;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            DustGroupRuntime group = groups[i];
            if (group?.Root != null)
            {
                group.Root.localPosition = group.BaseLocalPosition;
            }
        }
    }

    private void ClearOutlineParticles()
    {
        if (groups == null)
        {
            return;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            DustGroupRuntime group = groups[i];
            if (group == null)
            {
                continue;
            }

            StopAndClear(group.Particles);
            group.Points.Clear();
            group.Buffer = new ParticleSystem.Particle[0];
        }
        fallingSources.Clear();
    }

    private DustGroupRuntime GetGroup(EdgeRegion region)
    {
        return region switch
        {
            EdgeRegion.Top => topRuntime,
            EdgeRegion.Left => leftRuntime,
            EdgeRegion.Right => rightRuntime,
            _ => bottomRuntime
        };
    }

    private float GetRegionIntensity(EdgeRegion region)
    {
        return region switch
        {
            EdgeRegion.Top => topIntensityMultiplier,
            EdgeRegion.Bottom => bottomIntensityMultiplier,
            _ => sideIntensityMultiplier
        };
    }

    private Vector2 FindTopCenter()
    {
        BuildEdgeSegments();
        Vector2 total = Vector2.zero;
        int count = 0;
        for (int i = 0; i < edgeSegments.Count; i++)
        {
            if (edgeSegments[i].Region != EdgeRegion.Top)
            {
                continue;
            }
            total += (edgeSegments[i].Start + edgeSegments[i].End) * 0.5f;
            count++;
        }
        return count > 0 ? total / count : (Vector2)transform.position;
    }

    private void EmitWorldParticle(
        ParticleSystem particles,
        Vector2 position,
        Vector2 velocity,
        float size,
        float lifetime,
        float alpha)
    {
        if (particles == null)
        {
            return;
        }

        Color color = dustColor;
        color.a = Mathf.Clamp01(alpha);
        var emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startSize = Mathf.Max(0.001f, size),
            startLifetime = Mathf.Max(0.05f, lifetime),
            startColor = color
        };
        particles.Emit(emit, 1);
    }

    private static Transform FindOrCreateTransform(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing;
        }

        var child = new GameObject(objectName);
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void StopAndClear(ParticleSystem particles)
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static float EvaluateCurve(AnimationCurve curve, float value)
    {
        return curve != null && curve.length > 0
            ? curve.Evaluate(Mathf.Clamp01(value))
            : Mathf.Clamp01(value);
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private static float Hash01(int value)
    {
        unchecked
        {
            uint hash = (uint)value;
            hash ^= 2747636419u;
            hash *= 2654435769u;
            hash ^= hash >> 16;
            hash *= 2654435769u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static Vector2 RandomDisc(int seed)
    {
        float angle = Hash01(seed) * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(Hash01(seed + 1));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void OnValidate()
    {
        startShakeAmplitude = Mathf.Max(0f, startShakeAmplitude);
        endShakeAmplitude = Mathf.Max(startShakeAmplitude, endShakeAmplitude);
        startShakeFrequency = Mathf.Max(0.1f, startShakeFrequency);
        endShakeFrequency = Mathf.Max(startShakeFrequency, endShakeFrequency);
        verticalMotionRatio = Mathf.Clamp(verticalMotionRatio, 0f, 0.5f);
        secondaryNoiseAmount = Mathf.Clamp(secondaryNoiseAmount, 0f, 0.25f);
        topIntensityMultiplier = Mathf.Max(0f, topIntensityMultiplier);
        sideIntensityMultiplier = Mathf.Max(0f, sideIntensityMultiplier);
        bottomIntensityMultiplier = Mathf.Max(0f, bottomIntensityMultiplier);
        outlineAlpha = Mathf.Clamp01(outlineAlpha);
        outlineParticleSpacing = Mathf.Max(0.03f, outlineParticleSpacing);
        outlineParticleSizeMin = Mathf.Max(0.005f, outlineParticleSizeMin);
        outlineParticleSizeMax = Mathf.Max(outlineParticleSizeMin, outlineParticleSizeMax);
        outlineThickness = Mathf.Max(0f, outlineThickness);
        outlineGapAmount = Mathf.Clamp(outlineGapAmount, 0f, 0.8f);
        landingBurstCount = Mathf.Clamp(landingBurstCount, 0, 64);
        respawnBurstCount = Mathf.Clamp(respawnBurstCount, 0, 64);
        respawnBurstAlphaMultiplier = Mathf.Clamp01(respawnBurstAlphaMultiplier);
        fallingDustStartRate = Mathf.Max(0f, fallingDustStartRate);
        fallingDustEndRate = Mathf.Max(fallingDustStartRate, fallingDustEndRate);
        preBreakDustRateMultiplier = Mathf.Max(1f, preBreakDustRateMultiplier);
        breakAfterimageDuration = Mathf.Max(0.1f, breakAfterimageDuration);
        fallingDustLifetimeMin = Mathf.Max(0.05f, fallingDustLifetimeMin);
        fallingDustLifetimeMax = Mathf.Max(fallingDustLifetimeMin, fallingDustLifetimeMax);
        fallingDustSizeMin = Mathf.Max(0.005f, fallingDustSizeMin);
        fallingDustSizeMax = Mathf.Max(fallingDustSizeMin, fallingDustSizeMax);
        fallingDustSpeedMin = Mathf.Max(0f, fallingDustSpeedMin);
        fallingDustSpeedMax = Mathf.Max(fallingDustSpeedMin, fallingDustSpeedMax);
        preBreakStartNormalizedTime = Mathf.Clamp(preBreakStartNormalizedTime, 0.5f, 0.95f);
        preBreakBlinkCount = Mathf.Clamp(preBreakBlinkCount, 1, 6);
        preBreakBlinkSpeed = Mathf.Max(0.1f, preBreakBlinkSpeed);

        if (warningIntensityCurve == null || warningIntensityCurve.length == 0)
        {
            warningIntensityCurve = AnimationCurve.EaseInOut(0f, 0.68f, 1f, 1.2f);
        }
        if (shakeAmplitudeCurve == null || shakeAmplitudeCurve.length == 0)
        {
            shakeAmplitudeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
        if (shakeFrequencyCurve == null || shakeFrequencyCurve.length == 0)
        {
            shakeFrequencyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        ResolveReferences();
    }
}
