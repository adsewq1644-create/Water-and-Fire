using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShockwaveVisual2D : MonoBehaviour
{
    [Header("Air Band")]
    [SerializeField, Min(0.001f)] private float startRadius = 0.035f;
    [SerializeField, Min(0.01f)] private float airBandWidth = 0.55f;
    [SerializeField, Range(0.05f, 1f)] private float bandSoftness = 0.78f;
    [SerializeField, Range(0f, 0.02f)] private float distortionStrength = 0.012f;

    [Header("Organic Motion")]
    [SerializeField, Range(1f, 16f)] private float noiseScale = 5f;
    [SerializeField, Range(0f, 0.08f)] private float noiseStrength = 0.006f;
    [SerializeField, Range(0f, 4f)] private float noiseSpeed = 0.6f;
    [SerializeField, Range(0.5f, 16f)] private float rippleFrequency = 2.4f;
    [SerializeField, Range(0f, 16f)] private float rippleSpeed = 2.8f;
    [SerializeField, Range(0f, 1f)] private float trailStrength = 0.25f;
    [SerializeField, Range(0f, 0.2f)] private float tangentialStrength = 0.14f;

    [Header("Subtle Highlight")]
    [SerializeField, Range(0f, 0.05f)] private float highlightStrength = 0.008f;
    [SerializeField, Range(0.001f, 0.4f)] private float arcEdgeFade = 0.12f;
    [SerializeField, ColorUsage(true, true)] private Color playerTint =
        new Color(0.62f, 0.88f, 1.1f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color coopTint =
        new Color(0.65f, 1.05f, 0.78f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color creatureTint =
        new Color(0.88f, 0.72f, 1.08f, 1f);

    [Header("Envelope")]
    [SerializeField, Range(0f, 0.4f)] private float fadeInFraction = 0.06f;
    [SerializeField, Range(0.2f, 1f)] private float fadeOutStartFraction = 0.72f;

    public void Play(ShockwaveRequest request)
    {
        if (request.Radius <= 0f || request.Duration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        ShockwaveDistortionManager2D.Register(
            request,
            new ShockwaveDistortionManager2D.Style
            {
                startRadius = startRadius,
                airBandWidth = airBandWidth,
                bandSoftness = bandSoftness,
                distortionStrength = distortionStrength,
                noiseScale = noiseScale,
                noiseStrength = noiseStrength,
                noiseSpeed = noiseSpeed,
                rippleFrequency = rippleFrequency,
                rippleSpeed = rippleSpeed,
                trailStrength = trailStrength,
                tangentialStrength = tangentialStrength,
                highlightStrength = highlightStrength,
                arcEdgeFade = arcEdgeFade,
                fadeInFraction = fadeInFraction,
                fadeOutStartFraction = fadeOutStartFraction,
                tint = GetTint(request.SourceType)
            });

        Destroy(gameObject);
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

    private void OnValidate()
    {
        startRadius = Mathf.Max(0.001f, startRadius);
        airBandWidth = Mathf.Max(0.01f, airBandWidth);
        bandSoftness = Mathf.Clamp(bandSoftness, 0.05f, 1f);
        distortionStrength = Mathf.Clamp(distortionStrength, 0f, 0.02f);
        noiseScale = Mathf.Clamp(noiseScale, 1f, 16f);
        noiseStrength = Mathf.Clamp(noiseStrength, 0f, 0.08f);
        noiseSpeed = Mathf.Clamp(noiseSpeed, 0f, 4f);
        rippleFrequency = Mathf.Clamp(rippleFrequency, 0.5f, 16f);
        rippleSpeed = Mathf.Clamp(rippleSpeed, 0f, 16f);
        trailStrength = Mathf.Clamp01(trailStrength);
        tangentialStrength = Mathf.Clamp(tangentialStrength, 0f, 0.2f);
        highlightStrength = Mathf.Clamp(highlightStrength, 0f, 0.05f);
        arcEdgeFade = Mathf.Clamp(arcEdgeFade, 0.001f, 0.4f);
        fadeInFraction = Mathf.Clamp(fadeInFraction, 0f, 0.4f);
        fadeOutStartFraction = Mathf.Clamp(fadeOutStartFraction, 0.2f, 1f);
    }
}
