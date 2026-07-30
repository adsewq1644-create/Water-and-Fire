using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ShockwaveEmitter2D))]
public sealed class DarkCreatureSnoreShockwave2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShockwaveEmitter2D shockwaveEmitter;
    [SerializeField] private Transform snoreOrigin;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip snoreClip;
    [SerializeField] private ParticleSystem warningVfx;

    [Header("Timing")]
    [SerializeField] private bool autoStart = true;
    [SerializeField, Min(0f)] private float initialDelay = 1.2f;
    [SerializeField, Min(0.1f)] private float snoreInterval = 4.5f;
    [SerializeField, Min(0f)] private float intervalRandomness = 0.35f;
    [SerializeField, Min(0f)] private float warningDuration = 0.55f;

    [Header("Shockwave")]
    [SerializeField, Min(0.1f)] private float radius = 7f;
    [SerializeField, Min(0.05f)] private float duration = 1.1f;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private bool delayByDistance = true;
    [SerializeField, Min(0.1f)] private float visualWidthMultiplier = 1.35f;

    [Header("Warning")]
    [SerializeField] private bool showWarningWave = true;
    [SerializeField, Min(0.05f)] private float warningRadius = 0.55f;

    [Header("Animator Triggers")]
    [SerializeField] private string warningTrigger = "SnoreWarning";
    [SerializeField] private string snoreTrigger = "Snore";

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private Coroutine loopRoutine;
    private Coroutine activeSnoreRoutine;

    private Vector2 Origin => snoreOrigin != null
        ? (Vector2)snoreOrigin.position
        : (Vector2)transform.position;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (autoStart)
        {
            loopRoutine = StartCoroutine(SnoreLoop());
        }
    }

    private void OnDisable()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        if (activeSnoreRoutine != null)
        {
            StopCoroutine(activeSnoreRoutine);
            activeSnoreRoutine = null;
        }

        if (warningVfx != null)
        {
            warningVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    [ContextMenu("Trigger Snore Now")]
    public void TriggerSnoreNow()
    {
        if (!isActiveAndEnabled || activeSnoreRoutine != null)
        {
            return;
        }

        activeSnoreRoutine = StartCoroutine(PlaySnore());
    }

    private IEnumerator SnoreLoop()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (isActiveAndEnabled)
        {
            if (activeSnoreRoutine == null)
            {
                activeSnoreRoutine = StartCoroutine(PlaySnore());
                yield return activeSnoreRoutine;
            }

            float wait = snoreInterval + Random.Range(-intervalRandomness, intervalRandomness);
            yield return new WaitForSeconds(Mathf.Max(0.1f, wait));
        }
    }

    private IEnumerator PlaySnore()
    {
        TriggerAnimator(warningTrigger);
        if (warningVfx != null)
        {
            warningVfx.Play();
        }

        if (showWarningWave && warningDuration > 0f)
        {
            ShockwaveRequest warningRequest = BuildRequest(
                warningRadius,
                warningDuration,
                delayReceivers: false);
            shockwaveEmitter.PlayVisualOnly(warningRequest);
        }

        if (warningDuration > 0f)
        {
            yield return new WaitForSeconds(warningDuration);
        }

        if (warningVfx != null)
        {
            warningVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        TriggerAnimator(snoreTrigger);
        if (audioSource != null && snoreClip != null)
        {
            audioSource.PlayOneShot(snoreClip);
        }

        shockwaveEmitter.Emit(BuildRequest(radius, duration, delayByDistance));
        activeSnoreRoutine = null;
    }

    private ShockwaveRequest BuildRequest(
        float requestRadius,
        float requestDuration,
        bool delayReceivers)
    {
        return new ShockwaveRequest
        {
            Origin = Origin,
            SourceObject = gameObject,
            Instigator = null,
            SourceType = ShockwaveSourceType.CreatureSnore,
            Shape = ShockwaveShape.UpperSemicircle,
            Radius = requestRadius,
            Duration = requestDuration,
            TargetMask = targetMask,
            DelayByDistance = delayReceivers,
            ShowVisual = true,
            VisualWidthMultiplier = visualWidthMultiplier,
            ArcDirection = Vector2.up,
            ArcAngle = 180f
        };
    }

    private void ResolveReferences()
    {
        if (shockwaveEmitter == null)
        {
            shockwaveEmitter = GetComponent<ShockwaveEmitter2D>();
        }
    }

    private void TriggerAnimator(string triggerName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        initialDelay = Mathf.Max(0f, initialDelay);
        snoreInterval = Mathf.Max(0.1f, snoreInterval);
        intervalRandomness = Mathf.Max(0f, intervalRandomness);
        warningDuration = Mathf.Max(0f, warningDuration);
        warningRadius = Mathf.Max(0.05f, warningRadius);
        radius = Mathf.Max(0.1f, radius);
        duration = Mathf.Max(0.05f, duration);
        visualWidthMultiplier = Mathf.Max(0.1f, visualWidthMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector2 origin = snoreOrigin != null
            ? (Vector2)snoreOrigin.position
            : (Vector2)transform.position;
        Gizmos.color = new Color(0.85f, 0.55f, 1f, 0.75f);
        const int segments = 32;
        Vector3 previous = origin + Vector2.right * radius;
        for (int index = 1; index <= segments; index++)
        {
            float angle = index / (float)segments * Mathf.PI;
            Vector3 next = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        Gizmos.DrawLine(origin, origin + Vector2.right * radius);
        Gizmos.DrawLine(origin, origin + Vector2.left * radius);
        Gizmos.DrawWireSphere(origin, 0.08f);
    }
}
