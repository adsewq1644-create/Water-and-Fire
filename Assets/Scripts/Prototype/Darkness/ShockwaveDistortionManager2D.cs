using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShockwaveDistortionManager2D
{
    public const int MaximumWaveCount = 4;
    public const int MaximumDarkZoneRectCount = 8;

    [Serializable]
    public struct Style
    {
        [Min(0.001f)] public float startRadius;
        [Min(0.01f)] public float airBandWidth;
        [Range(0.05f, 1f)] public float bandSoftness;
        [Range(0f, 0.02f)] public float distortionStrength;
        [Range(1f, 16f)] public float noiseScale;
        [Range(0f, 0.08f)] public float noiseStrength;
        [Range(0f, 4f)] public float noiseSpeed;
        [Range(0.5f, 16f)] public float rippleFrequency;
        [Range(0f, 16f)] public float rippleSpeed;
        [Range(0f, 1f)] public float trailStrength;
        [Range(0f, 0.3f)] public float highlightStrength;
        [Range(0.001f, 0.4f)] public float arcEdgeFade;
        [Range(0f, 0.4f)] public float fadeInFraction;
        [Range(0.2f, 1f)] public float fadeOutStartFraction;
        public Color tint;
    }

    private sealed class ActiveWave
    {
        public ShockwaveRequest Request;
        public Style Style;
        public float StartTime;
        public ulong Sequence;
    }

    private static readonly List<ActiveWave> activeWaves =
        new List<ActiveWave>(MaximumWaveCount);

    private static readonly Vector4[] centerRadiusWidth =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] waveParameters0 =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] waveParameters1 =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] waveParameters2 =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] waveParameters3 =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] arcDirections =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] waveTints =
        new Vector4[MaximumWaveCount];
    private static readonly Vector4[] darkZoneRects =
        new Vector4[MaximumDarkZoneRectCount];

    private static readonly int WaveCountId = Shader.PropertyToID("_ShockwaveCount");
    private static readonly int CenterRadiusWidthId =
        Shader.PropertyToID("_ShockwaveCenterRadiusWidth");
    private static readonly int WaveParameters0Id =
        Shader.PropertyToID("_ShockwaveParameters0");
    private static readonly int WaveParameters1Id =
        Shader.PropertyToID("_ShockwaveParameters1");
    private static readonly int WaveParameters2Id =
        Shader.PropertyToID("_ShockwaveParameters2");
    private static readonly int WaveParameters3Id =
        Shader.PropertyToID("_ShockwaveParameters3");
    private static readonly int ArcDirectionsId =
        Shader.PropertyToID("_ShockwaveArcDirections");
    private static readonly int WaveTintsId = Shader.PropertyToID("_ShockwaveTints");
    private static readonly int DarkZoneRectCountId =
        Shader.PropertyToID("_DarkZoneRectCount");
    private static readonly int DarkZoneRectsId =
        Shader.PropertyToID("_DarkZoneRects");
    private static readonly int DarkZoneEdgeSoftnessId =
        Shader.PropertyToID("_DarkZoneEdgeSoftness");
    private static readonly int GlobalStrengthId =
        Shader.PropertyToID("_ShockwaveGlobalStrength");

    private static ulong nextSequence;

    public static int ActiveWaveCount => activeWaves.Count;

    public static void Register(ShockwaveRequest request, Style style)
    {
        if (request.Radius <= 0f ||
            request.Duration <= 0f ||
            !DarkZone.ContainsWorldPoint(request.Origin))
        {
            return;
        }

        PruneExpiredWaves(Time.time);
        if (activeWaves.Count >= MaximumWaveCount)
        {
            RemoveOldestWave();
        }

        activeWaves.Add(new ActiveWave
        {
            Request = request,
            Style = Sanitize(style),
            StartTime = Time.time,
            Sequence = nextSequence++
        });
    }

    public static bool ApplyToMaterial(
        Material material,
        Camera camera,
        float globalStrength,
        float darkZoneEdgeSoftness)
    {
        if (material == null || camera == null)
        {
            return false;
        }

        float now = Time.time;
        PruneExpiredWaves(now);
        if (activeWaves.Count == 0)
        {
            return false;
        }

        int darkZoneRectCount = DarkZone.FillViewportRects(
            camera,
            darkZoneRects,
            MaximumDarkZoneRectCount);
        if (darkZoneRectCount == 0)
        {
            return false;
        }

        int visibleWaveCount = 0;
        float aspect = Mathf.Max(0.0001f, camera.aspect);
        for (int index = 0;
             index < activeWaves.Count && visibleWaveCount < MaximumWaveCount;
             index++)
        {
            ActiveWave wave = activeWaves[index];
            float progress = Mathf.Clamp01(
                (now - wave.StartTime) / Mathf.Max(0.0001f, wave.Request.Duration));
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2.1f);
            float radiusWorld = Mathf.Lerp(
                wave.Style.startRadius,
                wave.Request.Radius,
                easedProgress);

            Vector3 originWorld = new Vector3(
                wave.Request.Origin.x,
                wave.Request.Origin.y,
                0f);
            Vector3 centerViewport = camera.WorldToViewportPoint(originWorld);
            if (centerViewport.z <= 0f)
            {
                continue;
            }

            float radiusViewport = ProjectWorldLengthToViewport(
                camera,
                originWorld,
                radiusWorld);
            float widthViewport = ProjectWorldLengthToViewport(
                camera,
                originWorld,
                wave.Style.airBandWidth * wave.Request.VisualWidthMultiplier);
            if (radiusViewport <= 0f || widthViewport <= 0f)
            {
                continue;
            }

            Vector2 arcDirection = ProjectDirectionToViewport(
                camera,
                originWorld,
                wave.Request.ArcDirection,
                aspect);
            float envelope = EvaluateEnvelope(progress, wave.Style);
            float arcCosine = Mathf.Cos(
                Mathf.Clamp(wave.Request.ArcAngle, 1f, 360f) *
                0.5f *
                Mathf.Deg2Rad);

            centerRadiusWidth[visibleWaveCount] = new Vector4(
                centerViewport.x,
                centerViewport.y,
                radiusViewport,
                widthViewport);
            waveParameters0[visibleWaveCount] = new Vector4(
                wave.Style.distortionStrength,
                wave.Request.Shape == ShockwaveShape.FullCircle ? 0f : 1f,
                arcCosine,
                envelope);
            waveParameters1[visibleWaveCount] = new Vector4(
                wave.Style.noiseScale,
                wave.Style.noiseStrength,
                wave.Style.noiseSpeed,
                wave.Style.arcEdgeFade);
            waveParameters2[visibleWaveCount] = new Vector4(
                wave.Style.rippleFrequency,
                wave.Style.rippleSpeed,
                wave.Style.trailStrength,
                wave.Style.highlightStrength);
            waveParameters3[visibleWaveCount] = new Vector4(
                wave.Style.bandSoftness,
                progress,
                0f,
                0f);
            arcDirections[visibleWaveCount] = new Vector4(
                arcDirection.x,
                arcDirection.y,
                0f,
                0f);
            waveTints[visibleWaveCount] = wave.Style.tint;
            visibleWaveCount++;
        }

        if (visibleWaveCount == 0)
        {
            return false;
        }

        ClearUnusedWaveSlots(visibleWaveCount);
        material.SetInt(WaveCountId, visibleWaveCount);
        material.SetVectorArray(CenterRadiusWidthId, centerRadiusWidth);
        material.SetVectorArray(WaveParameters0Id, waveParameters0);
        material.SetVectorArray(WaveParameters1Id, waveParameters1);
        material.SetVectorArray(WaveParameters2Id, waveParameters2);
        material.SetVectorArray(WaveParameters3Id, waveParameters3);
        material.SetVectorArray(ArcDirectionsId, arcDirections);
        material.SetVectorArray(WaveTintsId, waveTints);
        material.SetInt(DarkZoneRectCountId, darkZoneRectCount);
        material.SetVectorArray(DarkZoneRectsId, darkZoneRects);
        material.SetFloat(
            DarkZoneEdgeSoftnessId,
            Mathf.Max(0.00001f, darkZoneEdgeSoftness));
        material.SetFloat(GlobalStrengthId, Mathf.Max(0f, globalStrength));
        return true;
    }

    public static void Clear()
    {
        activeWaves.Clear();
    }

    private static Style Sanitize(Style style)
    {
        style.startRadius = Mathf.Max(0.001f, style.startRadius);
        style.airBandWidth = Mathf.Max(0.01f, style.airBandWidth);
        style.bandSoftness = Mathf.Clamp(style.bandSoftness, 0.05f, 1f);
        style.distortionStrength = Mathf.Clamp(style.distortionStrength, 0f, 0.02f);
        style.noiseScale = Mathf.Clamp(style.noiseScale, 1f, 16f);
        style.noiseStrength = Mathf.Clamp(style.noiseStrength, 0f, 0.08f);
        style.noiseSpeed = Mathf.Clamp(style.noiseSpeed, 0f, 4f);
        style.rippleFrequency = Mathf.Clamp(style.rippleFrequency, 0.5f, 16f);
        style.rippleSpeed = Mathf.Clamp(style.rippleSpeed, 0f, 16f);
        style.trailStrength = Mathf.Clamp01(style.trailStrength);
        style.highlightStrength = Mathf.Clamp(style.highlightStrength, 0f, 0.3f);
        style.arcEdgeFade = Mathf.Clamp(style.arcEdgeFade, 0.001f, 0.4f);
        style.fadeInFraction = Mathf.Clamp(style.fadeInFraction, 0f, 0.4f);
        style.fadeOutStartFraction = Mathf.Clamp(
            style.fadeOutStartFraction,
            0.2f,
            1f);
        return style;
    }

    private static float EvaluateEnvelope(float progress, Style style)
    {
        float fadeIn = style.fadeInFraction <= 0f
            ? 1f
            : Mathf.Clamp01(progress / style.fadeInFraction);
        float fadeOut = progress <= style.fadeOutStartFraction
            ? 1f
            : 1f - Mathf.InverseLerp(style.fadeOutStartFraction, 1f, progress);
        return Mathf.SmoothStep(0f, 1f, fadeIn) *
               Mathf.SmoothStep(0f, 1f, fadeOut);
    }

    private static float ProjectWorldLengthToViewport(
        Camera camera,
        Vector3 origin,
        float worldLength)
    {
        Vector3 endpoint = origin + camera.transform.up * worldLength;
        Vector3 originViewport = camera.WorldToViewportPoint(origin);
        Vector3 endpointViewport = camera.WorldToViewportPoint(endpoint);
        return Mathf.Abs(endpointViewport.y - originViewport.y);
    }

    private static Vector2 ProjectDirectionToViewport(
        Camera camera,
        Vector3 origin,
        Vector2 worldDirection,
        float aspect)
    {
        Vector2 direction = worldDirection.sqrMagnitude > 0.0001f
            ? worldDirection.normalized
            : Vector2.up;
        Vector3 endpoint = origin + new Vector3(direction.x, direction.y, 0f);
        Vector3 originViewport = camera.WorldToViewportPoint(origin);
        Vector3 endpointViewport = camera.WorldToViewportPoint(endpoint);
        Vector2 viewportDirection = new Vector2(
            (endpointViewport.x - originViewport.x) * aspect,
            endpointViewport.y - originViewport.y);
        return viewportDirection.sqrMagnitude > 0.0001f
            ? viewportDirection.normalized
            : Vector2.up;
    }

    private static void PruneExpiredWaves(float now)
    {
        for (int index = activeWaves.Count - 1; index >= 0; index--)
        {
            ActiveWave wave = activeWaves[index];
            if (wave == null ||
                now - wave.StartTime >= Mathf.Max(0.0001f, wave.Request.Duration))
            {
                activeWaves.RemoveAt(index);
            }
        }
    }

    private static void RemoveOldestWave()
    {
        int oldestIndex = 0;
        ulong oldestSequence = activeWaves[0].Sequence;
        for (int index = 1; index < activeWaves.Count; index++)
        {
            if (activeWaves[index].Sequence < oldestSequence)
            {
                oldestSequence = activeWaves[index].Sequence;
                oldestIndex = index;
            }
        }

        activeWaves.RemoveAt(oldestIndex);
    }

    private static void ClearUnusedWaveSlots(int usedCount)
    {
        for (int index = usedCount; index < MaximumWaveCount; index++)
        {
            centerRadiusWidth[index] = Vector4.zero;
            waveParameters0[index] = Vector4.zero;
            waveParameters1[index] = Vector4.zero;
            waveParameters2[index] = Vector4.zero;
            waveParameters3[index] = Vector4.zero;
            arcDirections[index] = Vector4.zero;
            waveTints[index] = Vector4.zero;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        activeWaves.Clear();
        nextSequence = 0;
    }
}
