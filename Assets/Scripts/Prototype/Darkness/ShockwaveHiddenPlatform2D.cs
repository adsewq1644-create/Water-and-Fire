using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShockwaveHiddenPlatform2D : MonoBehaviour, IShockwaveContextReceiver
{
    [Header("References")]
    [SerializeField] private SpriteRenderer[] silhouetteRenderers;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private ParticleSystem dustParticles;

    [Header("Reveal")]
    [SerializeField] private float revealDuration = 2.5f;
    [SerializeField] private float fadeInDuration = 0.1f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float hiddenAlpha;
    [SerializeField] private float revealedAlpha = 0.65f;
    [SerializeField] private Color silhouetteColor = new Color(0.16f, 0.18f, 0.22f, 1f);
    [SerializeField] private bool startHidden = true;

    [Header("Shake")]
    [SerializeField] private float shakeDuration = 0.35f;
    [SerializeField] private float shakeAmplitude = 0.06f;
    [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private Vector3 visualStartLocalPosition;
    private Coroutine revealRoutine;
    private float currentAlpha;

    private void Awake()
    {
        ResolveReferences();
        visualStartLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        SetVisualAlpha(startHidden ? hiddenAlpha : revealedAlpha);
    }

    private void OnDisable()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = visualStartLocalPosition;
        }
    }

    public void OnShockwaveReceived(ShockwaveContext context)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = visualStartLocalPosition;
        }

        if (dustParticles != null)
        {
            dustParticles.Play();
        }

        revealRoutine = StartCoroutine(RevealSequence());
    }

    private IEnumerator RevealSequence()
    {
        float fadeStartAlpha = currentAlpha;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            SetVisualAlpha(Mathf.Lerp(fadeStartAlpha, revealedAlpha, Normalized(elapsed, fadeInDuration)));
            ApplyShake(elapsed);
            yield return null;
        }

        SetVisualAlpha(revealedAlpha);
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            ApplyShake(elapsed);
            yield return null;
        }

        RestoreVisualPosition();
        if (revealDuration > 0f)
        {
            yield return new WaitForSeconds(revealDuration);
        }

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            SetVisualAlpha(Mathf.Lerp(revealedAlpha, hiddenAlpha, Normalized(elapsed, fadeOutDuration)));
            yield return null;
        }

        SetVisualAlpha(hiddenAlpha);
        revealRoutine = null;
    }

    private void ApplyShake(float elapsed)
    {
        if (visualRoot == null || shakeDuration <= 0f)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / shakeDuration);
        float strength = shakeCurve != null ? shakeCurve.Evaluate(t) : 1f - t;
        float horizontal = Mathf.Sin(elapsed * 82f) * shakeAmplitude * strength;
        float vertical = Mathf.Sin(elapsed * 113f) * shakeAmplitude * 0.35f * strength;
        visualRoot.localPosition = visualStartLocalPosition + new Vector3(horizontal, vertical, 0f);
    }

    private void RestoreVisualPosition()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualStartLocalPosition;
        }
    }

    private void SetVisualAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);
        if (silhouetteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < silhouetteRenderers.Length; i++)
        {
            SpriteRenderer renderer = silhouetteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = silhouetteColor;
            color.a = currentAlpha;
            renderer.color = color;
        }
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
    }

    private static float Normalized(float elapsed, float duration)
    {
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }

    private void OnValidate()
    {
        revealDuration = Mathf.Max(0f, revealDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        shakeDuration = Mathf.Max(0f, shakeDuration);
        shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
        hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
        revealedAlpha = Mathf.Clamp01(revealedAlpha);
        ResolveReferences();
    }
}
