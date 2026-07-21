using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class AmbientDustRegion2D : MonoBehaviour
{
    public enum DustDepth
    {
        Far,
        Mid
    }

    [Header("References")]
    [SerializeField] private ParticleSystem dustParticles;

    [Header("Region")]
    [SerializeField] private DustDepth depth = DustDepth.Far;
    [SerializeField] private bool regionEnabled = true;
    [SerializeField] private Vector2 regionSize = new Vector2(34f, 18f);
    [SerializeField] private Vector2 regionOffset = Vector2.zero;

    [Header("Dust")]
    [SerializeField, Min(0)] private int baseParticleCount = 70;
    [SerializeField, Range(0f, 3f)] private float density = 1f;
    [SerializeField] private Color color = new Color(0.62f, 0.84f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float alpha = 0.16f;
    [SerializeField, Min(0.001f)] private float sizeMin = 0.018f;
    [SerializeField, Min(0.001f)] private float sizeMax = 0.055f;
    [SerializeField, Min(0.1f)] private float lifetimeMin = 10f;
    [SerializeField, Min(0.1f)] private float lifetimeMax = 18f;

    [Header("Motion")]
    [SerializeField] private Vector2 driftDirection = new Vector2(0.35f, 0.12f);
    [SerializeField, Min(0f)] private float driftSpeed = 0.025f;
    [SerializeField, Min(0f)] private float randomSpeedMin = 0.005f;
    [SerializeField, Min(0f)] private float randomSpeedMax = 0.035f;
    [SerializeField, Min(0f)] private float noiseStrength = 0.055f;
    [SerializeField, Range(0f, 0.8f)] private float flickerAmount = 0.18f;

    [Header("Visual")]
    [SerializeField] private int sortingOrder = -80;

    [Header("Scene Gizmo")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool gizmosOnlyWhenSelected = true;

    private ParticleSystem.Particle[] particleBuffer;

    public Bounds LocalBounds => new Bounds(regionOffset, regionSize);

    private void OnEnable()
    {
        ApplyRegion();
    }

    private void OnValidate()
    {
        sizeMax = Mathf.Max(sizeMin, sizeMax);
        lifetimeMax = Mathf.Max(lifetimeMin, lifetimeMax);
        randomSpeedMax = Mathf.Max(randomSpeedMin, randomSpeedMax);
        regionSize.x = Mathf.Max(0.1f, regionSize.x);
        regionSize.y = Mathf.Max(0.1f, regionSize.y);

        if (Application.isPlaying)
        {
            return;
        }

        ApplyRegion();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !regionEnabled || dustParticles == null)
        {
            return;
        }

        KeepParticlesInsideRegion();
    }

    [ContextMenu("Apply Ambient Dust Region")]
    public void ApplyRegion()
    {
        if (dustParticles == null)
        {
            return;
        }

        bool shouldPlay = regionEnabled && density > 0f && baseParticleCount > 0;
        dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dustParticles.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(randomSpeedMin, randomSpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        Color particleColor = color;
        particleColor.a = alpha;
        main.startColor = particleColor;
        main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(baseParticleCount * density * 1.25f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        float averageLifetime = (lifetimeMin + lifetimeMax) * 0.5f;
        var emission = dustParticles.emission;
        emission.enabled = shouldPlay;
        emission.rateOverTime = baseParticleCount * density / Mathf.Max(0.1f, averageLifetime);

        var shape = dustParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(regionOffset.x, regionOffset.y, 0f);
        shape.scale = new Vector3(regionSize.x, regionSize.y, 0.1f);
        shape.randomDirectionAmount = 1f;

        Vector2 direction = driftDirection.sqrMagnitude > 0.0001f ? driftDirection.normalized : Vector2.zero;
        var velocity = dustParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = direction.x * driftSpeed;
        velocity.y = direction.y * driftSpeed;

        var noise = dustParticles.noise;
        noise.enabled = noiseStrength > 0f;
        noise.separateAxes = true;
        noise.strengthX = noiseStrength;
        noise.strengthY = noiseStrength;
        noise.strengthZ = 0f;
        noise.frequency = 0.16f;
        noise.scrollSpeed = 0.04f;
        noise.damping = true;
        noise.octaveCount = 1;

        var colorOverLifetime = dustParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f - flickerAmount, 0.52f),
                new GradientAlphaKey(1f, 0.84f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.enabled = regionEnabled;
            renderer.sortingOrder = sortingOrder;
        }

        if (Application.isPlaying && shouldPlay)
        {
            dustParticles.Play();
        }
    }

    public void SetRegionEnabled(bool value)
    {
        regionEnabled = value;
        ApplyRegion();
    }

    private void KeepParticlesInsideRegion()
    {
        int capacity = Mathf.Max(1, dustParticles.main.maxParticles);
        if (particleBuffer == null || particleBuffer.Length < capacity)
        {
            particleBuffer = new ParticleSystem.Particle[capacity];
        }

        int count = dustParticles.GetParticles(particleBuffer);
        Vector2 halfSize = regionSize * 0.5f;
        Vector2 minimum = regionOffset - halfSize;
        Vector2 maximum = regionOffset + halfSize;
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 localPosition = transform.InverseTransformPoint(particleBuffer[i].position);
            Vector3 wrappedPosition = localPosition;

            if (wrappedPosition.x < minimum.x)
            {
                wrappedPosition.x = maximum.x;
            }
            else if (wrappedPosition.x > maximum.x)
            {
                wrappedPosition.x = minimum.x;
            }

            if (wrappedPosition.y < minimum.y)
            {
                wrappedPosition.y = maximum.y;
            }
            else if (wrappedPosition.y > maximum.y)
            {
                wrappedPosition.y = minimum.y;
            }

            if (wrappedPosition != localPosition)
            {
                particleBuffer[i].position = transform.TransformPoint(wrappedPosition);
                changed = true;
            }
        }

        if (changed)
        {
            dustParticles.SetParticles(particleBuffer, count);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || gizmosOnlyWhenSelected)
        {
            return;
        }

        DrawRegionGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        DrawRegionGizmo();
    }

    private void DrawRegionGizmo()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Color activeColor = depth == DustDepth.Far
            ? new Color(0.42f, 0.72f, 1f, 0.8f)
            : new Color(0.72f, 0.48f, 1f, 0.8f);
        Gizmos.color = regionEnabled ? activeColor : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        Gizmos.DrawWireCube(regionOffset, regionSize);

        if (driftDirection.sqrMagnitude > 0.0001f)
        {
            Vector2 direction = driftDirection.normalized;
            Vector3 start = regionOffset;
            Vector3 end = start + (Vector3)(direction * Mathf.Min(regionSize.x, regionSize.y) * 0.18f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.09f);
        }

        Gizmos.matrix = previous;
    }
}
