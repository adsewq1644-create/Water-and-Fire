using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShockwaveHiddenPlatform2D : MonoBehaviour, IShockwaveContextReceiver
{
    private readonly struct EdgeSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly Vector2 Normal;
        public readonly bool IsTop;

        public EdgeSegment(Vector2 start, Vector2 end, Vector2 normal, bool isTop)
        {
            Start = start;
            End = end;
            Normal = normal;
            IsTop = isTop;
        }

        public float Length => Vector2.Distance(Start, End);
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer[] silhouetteRenderers;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Collider2D[] platformColliders;
    [SerializeField] private Material particleMaterial;

    [Header("Reveal Timing")]
    [SerializeField, Min(0.05f)] private float revealDuration = 0.8f;
    [SerializeField, Min(0f)] private float fadeInTime = 0.08f;
    [SerializeField, Min(0f)] private float fadeOutTime = 0.32f;
    [SerializeField, Min(0f)] private float minimumRevealInterval = 0.08f;

    [Header("Hidden Surface")]
    [SerializeField, Range(0f, 0.08f)] private float idleSilhouetteAlpha;

    [Header("Edge Glow")]
    [SerializeField, ColorUsage(true, true)] private Color edgeGlowColor = new Color(0.42f, 1.15f, 2.1f, 1f);
    [SerializeField, Range(0f, 1f)] private float edgeGlowAlpha = 0.72f;
    [SerializeField, Range(0.01f, 0.25f)] private float edgeGlowWidth = 0.075f;
    [SerializeField, Range(0f, 0.2f)] private float edgeGlowJitter = 0.035f;
    [SerializeField] private bool edgeGlowTopOnly = true;
    [SerializeField, Range(1f, 4f)] private float edgeGlowCornerBoost = 1.65f;

    [Header("Particles")]
    [SerializeField, ColorUsage(true, true)] private Color particleColor = new Color(0.62f, 0.88f, 2.2f, 1f);
    [SerializeField, Range(0f, 1f)] private float particleAlpha = 0.82f;
    [SerializeField, Range(1, 300)] private int particleCount = 38;
    [SerializeField, Min(0.05f)] private float particleLifetimeMin = 0.38f;
    [SerializeField, Min(0.05f)] private float particleLifetimeMax = 0.85f;
    [SerializeField, Min(0.005f)] private float particleSizeMin = 0.025f;
    [SerializeField, Min(0.005f)] private float particleSizeMax = 0.085f;
    [SerializeField, Min(0f)] private float particleSpeedMin = 0.08f;
    [SerializeField, Min(0f)] private float particleSpeedMax = 0.38f;
    [SerializeField, Range(0f, 1f)] private float particleSpread = 0.48f;
    [SerializeField, Min(0f)] private float particleNoiseStrength = 0.18f;
    [SerializeField, Range(0.1f, 4f)] private float edgeParticleMultiplier = 1f;
    [SerializeField, Range(0f, 6f)] private float cornerParticleMultiplier = 2.2f;

    [Header("Distance Response")]
    [SerializeField] private AnimationCurve distanceFalloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float minIntensity = 0.22f;
    [SerializeField, Range(0f, 2f)] private float maxIntensity = 1f;

    [Header("Rendering")]
    [SerializeField] private int particleSortingOrder = 4;

    [Header("Debug")]
    [SerializeField] private bool showEdgeGizmos = true;

    private readonly List<EdgeSegment> edgeSegments = new List<EdgeSegment>(32);
    private readonly List<EdgeSegment> activeSegments = new List<EdgeSegment>(32);
    private readonly List<Vector2> cornerPoints = new List<Vector2>(32);
    private ParticleSystem edgeGlowParticles;
    private ParticleSystem sporeParticles;
    private Material runtimeParticleMaterial;
    private float lastRevealTime = float.NegativeInfinity;

    private void Awake()
    {
        ResolveReferences();
        SetWholeSurfaceAlpha(idleSilhouetteAlpha);
        EnsureParticleSystems();
    }

    private void OnDisable()
    {
        StopAndClear(edgeGlowParticles);
        StopAndClear(sporeParticles);
        SetWholeSurfaceAlpha(idleSilhouetteAlpha);
    }

    private void OnDestroy()
    {
        if (runtimeParticleMaterial != null)
        {
            Destroy(runtimeParticleMaterial);
        }
    }

    public void OnShockwaveReceived(ShockwaveContext context)
    {
        if (!isActiveAndEnabled || Time.time < lastRevealTime + minimumRevealInterval)
        {
            return;
        }

        lastRevealTime = Time.time;
        ResolveReferences();
        EnsureParticleSystems();
        BuildEdgeSegments();
        SelectRevealSegments();
        if (activeSegments.Count == 0)
        {
            return;
        }

        float closeness = context.Radius > 0.0001f
            ? 1f - Mathf.Clamp01(context.Distance / context.Radius)
            : 1f;
        float response = distanceFalloffCurve != null
            ? Mathf.Clamp01(distanceFalloffCurve.Evaluate(closeness))
            : closeness;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, response);

        SetWholeSurfaceAlpha(idleSilhouetteAlpha);
        ConfigureParticleSystems();
        EmitSparseEdgeGlow(intensity);
        EmitSurfaceSpores(intensity);
        EmitCornerSpores(intensity);
    }

    [ContextMenu("Refresh Reveal References")]
    public void RefreshRevealReferences()
    {
        silhouetteRenderers = visualRoot != null
            ? visualRoot.GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponentsInChildren<SpriteRenderer>(true);
        platformColliders = GetComponentsInChildren<Collider2D>(true);
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (silhouetteRenderers == null || silhouetteRenderers.Length == 0)
        {
            silhouetteRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (platformColliders == null || platformColliders.Length == 0)
        {
            platformColliders = GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void SetWholeSurfaceAlpha(float alpha)
    {
        if (silhouetteRenderers == null)
        {
            return;
        }

        float clampedAlpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < silhouetteRenderers.Length; i++)
        {
            SpriteRenderer renderer = silhouetteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = clampedAlpha;
            renderer.color = color;
        }
    }

    private void EnsureParticleSystems()
    {
        if (edgeGlowParticles != null && sporeParticles != null)
        {
            return;
        }

        Transform effectRoot = transform.Find("ShockwaveEdgeRevealFx");
        if (effectRoot == null)
        {
            var effectObject = new GameObject("ShockwaveEdgeRevealFx");
            effectObject.layer = gameObject.layer;
            effectObject.transform.SetParent(transform, false);
            effectRoot = effectObject.transform;
        }

        edgeGlowParticles = FindOrCreateParticleSystem(effectRoot, "SparseEdgeGlow");
        sporeParticles = FindOrCreateParticleSystem(effectRoot, "SurfaceSpores");
        ApplyParticleMaterial(edgeGlowParticles);
        ApplyParticleMaterial(sporeParticles);
    }

    private ParticleSystem FindOrCreateParticleSystem(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        ParticleSystem particles;
        if (existing != null && existing.TryGetComponent(out particles))
        {
            return particles;
        }

        var particleObject = new GameObject(objectName);
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(parent, false);
        particles = particleObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1200;

        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = particleSortingOrder;
        return particles;
    }

    private void ApplyParticleMaterial(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial != null ? particleMaterial : GetRuntimeParticleMaterial();
        renderer.sortingOrder = particleSortingOrder;
        if (silhouetteRenderers != null && silhouetteRenderers.Length > 0 && silhouetteRenderers[0] != null)
        {
            renderer.sortingLayerID = silhouetteRenderers[0].sortingLayerID;
        }
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
            runtimeParticleMaterial.name = "ShockwaveRevealSpore_Runtime";
        }
        return runtimeParticleMaterial;
    }

    private void ConfigureParticleSystems()
    {
        ConfigureParticleSystem(edgeGlowParticles, particleNoiseStrength * 0.25f);
        ConfigureParticleSystem(sporeParticles, particleNoiseStrength);
    }

    private void ConfigureParticleSystem(ParticleSystem particles, float noiseStrength)
    {
        if (particles == null)
        {
            return;
        }

        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 1200;

        var noise = particles.noise;
        noise.enabled = noiseStrength > 0f;
        noise.separateAxes = true;
        noise.strengthX = noiseStrength;
        noise.strengthY = noiseStrength;
        noise.strengthZ = 0f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.18f;
        noise.damping = true;
        noise.octaveCount = 1;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient();

        ApplyParticleMaterial(particles);
    }

    private Gradient BuildFadeGradient()
    {
        float duration = Mathf.Max(0.05f, revealDuration);
        float fadeInEnd = Mathf.Clamp01(fadeInTime / duration);
        float fadeOutStart = Mathf.Clamp01(1f - fadeOutTime / duration);
        if (fadeOutStart < fadeInEnd)
        {
            fadeOutStart = fadeInEnd;
        }

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, fadeInEnd),
                new GradientAlphaKey(1f, fadeOutStart),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void BuildEdgeSegments()
    {
        edgeSegments.Clear();
        if (platformColliders == null)
        {
            return;
        }

        for (int i = 0; i < platformColliders.Length; i++)
        {
            Collider2D collider = platformColliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            switch (collider)
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
                    AddBoundsTopEdge(collider.bounds);
                    break;
            }
        }
    }

    private void AddBoxEdges(BoxCollider2D box)
    {
        Vector2 half = box.size * 0.5f;
        Vector2 offset = box.offset;
        Vector2 bottomLeft = ToWorldPoint(box.transform, offset + new Vector2(-half.x, -half.y));
        Vector2 bottomRight = ToWorldPoint(box.transform, offset + new Vector2(half.x, -half.y));
        Vector2 topRight = ToWorldPoint(box.transform, offset + new Vector2(half.x, half.y));
        Vector2 topLeft = ToWorldPoint(box.transform, offset + new Vector2(-half.x, half.y));

        AddSegment(bottomLeft, bottomRight, -box.transform.up, false);
        AddSegment(bottomRight, topRight, box.transform.right, false);
        AddSegment(topRight, topLeft, box.transform.up, true);
        AddSegment(topLeft, bottomLeft, -box.transform.right, false);
    }

    private void AddPolygonEdges(PolygonCollider2D polygon)
    {
        for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
        {
            Vector2[] path = polygon.GetPath(pathIndex);
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 start = ToWorldPoint(polygon.transform, path[i] + polygon.offset);
                Vector2 end = ToWorldPoint(polygon.transform, path[(i + 1) % path.Length] + polygon.offset);
                AddClassifiedSegment(start, end, polygon.bounds);
            }
        }
    }

    private void AddEdgeColliderEdges(EdgeCollider2D edge)
    {
        Vector2[] points = edge.points;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 start = ToWorldPoint(edge.transform, points[i] + edge.offset);
            Vector2 end = ToWorldPoint(edge.transform, points[i + 1] + edge.offset);
            AddSegment(start, end, UpwardNormal(start, end), true);
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
                Vector2 start = ToWorldPoint(composite.transform, points[i] + composite.offset);
                Vector2 end = ToWorldPoint(composite.transform, points[i + 1] + composite.offset);
                AddClassifiedSegment(start, end, composite.bounds);
            }
        }
    }

    private void AddClassifiedSegment(Vector2 start, Vector2 end, Bounds bounds)
    {
        Vector2 normal = UpwardNormal(start, end);
        Vector2 midpoint = (start + end) * 0.5f;
        Vector2 direction = (end - start).normalized;
        bool facesUp = normal.y >= 0.35f;
        bool liesInUpperHalf = midpoint.y >= bounds.center.y;
        bool isSurfaceLike = Mathf.Abs(direction.x) >= 0.3f;
        AddSegment(start, end, normal, facesUp && liesInUpperHalf && isSurfaceLike);
    }

    private void AddBoundsTopEdge(Bounds bounds)
    {
        Vector2 start = new Vector2(bounds.min.x, bounds.max.y);
        Vector2 end = new Vector2(bounds.max.x, bounds.max.y);
        AddSegment(start, end, Vector2.up, true);
    }

    private void AddSegment(Vector2 start, Vector2 end, Vector2 normal, bool isTop)
    {
        if ((end - start).sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 normalizedNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : UpwardNormal(start, end);
        edgeSegments.Add(new EdgeSegment(start, end, normalizedNormal, isTop));
    }

    private void SelectRevealSegments()
    {
        activeSegments.Clear();
        cornerPoints.Clear();

        for (int i = 0; i < edgeSegments.Count; i++)
        {
            EdgeSegment segment = edgeSegments[i];
            if (edgeGlowTopOnly && !segment.IsTop)
            {
                continue;
            }

            activeSegments.Add(segment);
            AddUniqueCorner(segment.Start);
            AddUniqueCorner(segment.End);
        }

        if (activeSegments.Count == 0 && edgeSegments.Count > 0)
        {
            float highestMidpoint = float.NegativeInfinity;
            for (int i = 0; i < edgeSegments.Count; i++)
            {
                highestMidpoint = Mathf.Max(highestMidpoint, (edgeSegments[i].Start.y + edgeSegments[i].End.y) * 0.5f);
            }

            for (int i = 0; i < edgeSegments.Count; i++)
            {
                EdgeSegment segment = edgeSegments[i];
                float midpointY = (segment.Start.y + segment.End.y) * 0.5f;
                if (midpointY < highestMidpoint - 0.08f)
                {
                    continue;
                }

                activeSegments.Add(segment);
                AddUniqueCorner(segment.Start);
                AddUniqueCorner(segment.End);
            }
        }
    }

    private void AddUniqueCorner(Vector2 point)
    {
        for (int i = 0; i < cornerPoints.Count; i++)
        {
            if ((cornerPoints[i] - point).sqrMagnitude < 0.0025f)
            {
                return;
            }
        }
        cornerPoints.Add(point);
    }

    private void EmitSparseEdgeGlow(float intensity)
    {
        float spacing = Mathf.Max(0.14f, edgeGlowWidth * 3.8f);
        for (int segmentIndex = 0; segmentIndex < activeSegments.Count; segmentIndex++)
        {
            EdgeSegment segment = activeSegments[segmentIndex];
            int samples = Mathf.Clamp(
                Mathf.RoundToInt(segment.Length / spacing * edgeParticleMultiplier * intensity),
                1,
                180);
            Vector2 tangent = (segment.End - segment.Start).normalized;

            for (int i = 0; i < samples; i++)
            {
                float t = (i + Random.Range(0.18f, 0.82f)) / samples;
                if (Random.value < 0.22f)
                {
                    continue;
                }

                Vector2 position = Vector2.Lerp(segment.Start, segment.End, t);
                position += tangent * Random.Range(-edgeGlowJitter, edgeGlowJitter);
                position += segment.Normal * Random.Range(-edgeGlowJitter * 0.25f, edgeGlowJitter);
                EmitParticle(
                    edgeGlowParticles,
                    position,
                    segment.Normal * Random.Range(0f, particleSpeedMin * 0.35f),
                    Random.Range(edgeGlowWidth * 0.65f, edgeGlowWidth * 1.2f),
                    Mathf.Min(revealDuration, Random.Range(particleLifetimeMin, particleLifetimeMax)),
                    edgeGlowColor,
                    edgeGlowAlpha * intensity);
            }
        }
    }

    private void EmitSurfaceSpores(float intensity)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(particleCount * edgeParticleMultiplier * intensity), 1, 600);
        float totalLength = TotalActiveLength();
        if (totalLength <= 0.0001f)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            EdgeSegment segment = ChooseSegmentByLength(Random.value * totalLength);
            Vector2 tangent = (segment.End - segment.Start).normalized;
            Vector2 position = Vector2.Lerp(segment.Start, segment.End, Random.value);
            position += tangent * Random.Range(-edgeGlowJitter, edgeGlowJitter);
            position += segment.Normal * Random.Range(0f, edgeGlowJitter * 1.8f);
            EmitSpore(position, segment.Normal, intensity, 1f);
        }
    }

    private void EmitCornerSpores(float intensity)
    {
        int perCorner = Mathf.Clamp(Mathf.RoundToInt(cornerParticleMultiplier * intensity), 1, 18);
        for (int cornerIndex = 0; cornerIndex < cornerPoints.Count; cornerIndex++)
        {
            for (int i = 0; i < perCorner; i++)
            {
                Vector2 position = cornerPoints[cornerIndex] + Random.insideUnitCircle * edgeGlowJitter;
                EmitSpore(position, Vector2.up, intensity, edgeGlowCornerBoost);
            }
        }
    }

    private void EmitSpore(Vector2 position, Vector2 surfaceNormal, float intensity, float sizeBoost)
    {
        Vector2 randomDirection = Random.insideUnitCircle;
        if (randomDirection.sqrMagnitude < 0.0001f)
        {
            randomDirection = Vector2.up;
        }
        randomDirection.Normalize();

        Vector2 direction = Vector2.Lerp(surfaceNormal.normalized, randomDirection, particleSpread).normalized;
        float speed = Random.Range(particleSpeedMin, particleSpeedMax);
        EmitParticle(
            sporeParticles,
            position,
            direction * speed,
            Random.Range(particleSizeMin, particleSizeMax) * sizeBoost,
            Mathf.Min(revealDuration, Random.Range(particleLifetimeMin, particleLifetimeMax)),
            particleColor,
            particleAlpha * intensity);
    }

    private static void EmitParticle(
        ParticleSystem particles,
        Vector2 position,
        Vector2 velocity,
        float size,
        float lifetime,
        Color color,
        float alpha)
    {
        if (particles == null)
        {
            return;
        }

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

    private float TotalActiveLength()
    {
        float length = 0f;
        for (int i = 0; i < activeSegments.Count; i++)
        {
            length += activeSegments[i].Length;
        }
        return length;
    }

    private EdgeSegment ChooseSegmentByLength(float distance)
    {
        for (int i = 0; i < activeSegments.Count; i++)
        {
            EdgeSegment segment = activeSegments[i];
            if (distance <= segment.Length)
            {
                return segment;
            }
            distance -= segment.Length;
        }
        return activeSegments[activeSegments.Count - 1];
    }

    private static Vector2 UpwardNormal(Vector2 start, Vector2 end)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        return normal.y < 0f ? -normal : normal;
    }

    private static Vector2 ToWorldPoint(Transform source, Vector2 localPoint)
    {
        return source.TransformPoint(localPoint);
    }

    private static void StopAndClear(ParticleSystem particles)
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnValidate()
    {
        revealDuration = Mathf.Max(0.05f, revealDuration);
        fadeInTime = Mathf.Clamp(fadeInTime, 0f, revealDuration);
        fadeOutTime = Mathf.Clamp(fadeOutTime, 0f, revealDuration);
        minimumRevealInterval = Mathf.Max(0f, minimumRevealInterval);
        edgeGlowWidth = Mathf.Max(0.01f, edgeGlowWidth);
        particleCount = Mathf.Max(1, particleCount);
        particleLifetimeMin = Mathf.Max(0.05f, particleLifetimeMin);
        particleLifetimeMax = Mathf.Max(particleLifetimeMin, particleLifetimeMax);
        particleSizeMin = Mathf.Max(0.005f, particleSizeMin);
        particleSizeMax = Mathf.Max(particleSizeMin, particleSizeMax);
        particleSpeedMin = Mathf.Max(0f, particleSpeedMin);
        particleSpeedMax = Mathf.Max(particleSpeedMin, particleSpeedMax);
        minIntensity = Mathf.Clamp01(minIntensity);
        maxIntensity = Mathf.Max(minIntensity, maxIntensity);
        ResolveReferences();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showEdgeGizmos)
        {
            return;
        }

        ResolveReferences();
        BuildEdgeSegments();
        for (int i = 0; i < edgeSegments.Count; i++)
        {
            EdgeSegment segment = edgeSegments[i];
            Gizmos.color = segment.IsTop
                ? new Color(0.35f, 0.95f, 1f, 0.9f)
                : new Color(0.65f, 0.35f, 1f, 0.35f);
            Gizmos.DrawLine(segment.Start, segment.End);
        }
    }
}
