using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class AmbientDust2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ParticleSystem farDust;
    [SerializeField] private ParticleSystem midDust;

    [Header("General")]
    [SerializeField] private bool enableAmbientDust = true;
    [SerializeField] private Color dustTintColor = new Color(0.62f, 0.84f, 1f, 1f);
    [SerializeField, Range(0f, 3f)] private float overallDensity = 1f;
    [SerializeField, Range(0f, 1f)] private float overallAlpha = 0.42f;

    [Header("Far Dust")]
    [SerializeField, Min(0)] private int farCount = 70;
    [SerializeField, Min(0.001f)] private float farSizeMin = 0.018f;
    [SerializeField, Min(0.001f)] private float farSizeMax = 0.055f;
    [SerializeField, Min(0f)] private float farSpeedMin = 0.005f;
    [SerializeField, Min(0f)] private float farSpeedMax = 0.035f;
    [SerializeField, Range(0f, 1f)] private float farAlpha = 0.34f;
    [SerializeField] private Vector2 farSpawnAreaSize = new Vector2(34f, 18f);

    [Header("Mid Dust")]
    [SerializeField, Min(0)] private int midCount = 38;
    [SerializeField, Min(0.001f)] private float midSizeMin = 0.035f;
    [SerializeField, Min(0.001f)] private float midSizeMax = 0.095f;
    [SerializeField, Min(0f)] private float midSpeedMin = 0.015f;
    [SerializeField, Min(0f)] private float midSpeedMax = 0.075f;
    [SerializeField, Range(0f, 1f)] private float midAlpha = 0.50f;
    [SerializeField] private Vector2 midSpawnAreaSize = new Vector2(30f, 16f);

    [Header("Motion")]
    [SerializeField] private Vector2 driftDirection = new Vector2(0.35f, 0.12f);
    [SerializeField, Min(0f)] private float driftStrength = 0.025f;
    [SerializeField, Min(0f)] private float randomMotionStrength = 0.055f;
    [SerializeField, Range(0f, 0.8f)] private float flickerAmount = 0.18f;
    [SerializeField] private bool followCamera = true;

    private Camera cachedCamera;

    private void OnEnable()
    {
        ResolveCamera();
        ApplySettings();
    }

    private void LateUpdate()
    {
        ResolveCamera();
        if (Application.isPlaying && followCamera && cachedCamera != null)
        {
            Vector3 cameraPosition = cachedCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);
        }
    }

    private void OnValidate()
    {
        farSizeMax = Mathf.Max(farSizeMin, farSizeMax);
        midSizeMax = Mathf.Max(midSizeMin, midSizeMax);
        farSpeedMax = Mathf.Max(farSpeedMin, farSpeedMax);
        midSpeedMax = Mathf.Max(midSpeedMin, midSpeedMax);
        farSpawnAreaSize = MaxSize(farSpawnAreaSize);
        midSpawnAreaSize = MaxSize(midSpawnAreaSize);

        // Entering Play Mode can invoke OnValidate while Application.isPlaying is
        // already true. Starting a ParticleSystem from that callback is forbidden.
        // OnEnable applies the same settings safely immediately afterward.
        if (Application.isPlaying)
        {
            return;
        }

        ApplySettings();
    }

    [ContextMenu("Apply Ambient Dust Settings")]
    public void ApplySettings()
    {
        ConfigureLayer(farDust, farCount, farSizeMin, farSizeMax, farSpeedMin, farSpeedMax, farAlpha, farSpawnAreaSize, 14f, 0.65f);
        ConfigureLayer(midDust, midCount, midSizeMin, midSizeMax, midSpeedMin, midSpeedMax, midAlpha, midSpawnAreaSize, 10f, 1f);
    }

    private void ConfigureLayer(
        ParticleSystem particles,
        int count,
        float sizeMin,
        float sizeMax,
        float speedMin,
        float speedMax,
        float layerAlpha,
        Vector2 spawnArea,
        float lifetime,
        float motionScale)
    {
        if (particles == null)
        {
            return;
        }

        bool shouldPlay = enableAmbientDust && overallDensity > 0f && count > 0;
        // Unity does not allow duration/prewarm changes while a system is playing.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.72f, lifetime * 1.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        Color color = dustTintColor;
        color.a = Mathf.Clamp01(overallAlpha * layerAlpha);
        main.startColor = color;
        main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(count * overallDensity * 1.25f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;

        var emission = particles.emission;
        emission.enabled = shouldPlay;
        float targetCount = count * overallDensity;
        emission.rateOverTime = targetCount / lifetime;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(spawnArea.x, spawnArea.y, 0.1f);
        shape.randomDirectionAmount = 1f;

        Vector2 direction = driftDirection.sqrMagnitude > 0.0001f ? driftDirection.normalized : Vector2.zero;
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = direction.x * driftStrength * motionScale;
        velocity.y = direction.y * driftStrength * motionScale;

        var noise = particles.noise;
        noise.enabled = randomMotionStrength > 0f;
        noise.separateAxes = true;
        noise.strengthX = randomMotionStrength * motionScale;
        noise.strengthY = randomMotionStrength * motionScale;
        noise.strengthZ = 0f;
        noise.frequency = 0.16f;
        noise.scrollSpeed = 0.04f;
        noise.damping = true;
        noise.octaveCount = 1;

        var colorOverLifetime = particles.colorOverLifetime;
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

        if (!Application.isPlaying)
        {
            return;
        }

        if (shouldPlay)
        {
            particles.Play();
        }
    }

    private void ResolveCamera()
    {
        cachedCamera = targetCamera != null ? targetCamera : Camera.main;
    }

    private static Vector2 MaxSize(Vector2 value)
    {
        return new Vector2(Mathf.Max(0.1f, value.x), Mathf.Max(0.1f, value.y));
    }
}
