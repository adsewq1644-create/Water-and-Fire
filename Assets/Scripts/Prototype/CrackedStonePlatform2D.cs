using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CrackedStonePlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    [Header("Break")]
    [SerializeField] private bool startBroken;
    [SerializeField] private bool breakOnlyOnce = true;
    [SerializeField] private float breakDelay = 0.03f;

    [Header("Visual")]
    [SerializeField] private Collider2D platformCollider;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private ParticleSystem breakVfx;
    [SerializeField] private AudioClip breakSfx;

    [Header("Physics")]
    [SerializeField] private float continueFallSpeed = 18f;

    private bool broken;
    private bool hasBrokenOnce;
    private Coroutine breakRoutine;

    private void Awake()
    {
        ResolveReferences();
        broken = startBroken;
        hasBrokenOnce = startBroken;
        ApplyBrokenState();
    }

    private void OnEnable()
    {
        ApplyBrokenState();
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        if (!CanBreak())
        {
            return;
        }

        PlayerCharacter player = instigator != null ? instigator.GetComponent<PlayerCharacter>() : null;
        player?.SuppressDiveLandingStunThisImpact();

        if (breakRoutine != null)
        {
            return;
        }

        breakRoutine = StartCoroutine(BreakAfterDelay(instigator));
    }

    public void BreakNow(GameObject instigator = null)
    {
        if (!CanBreak())
        {
            return;
        }

        BreakPlatform(instigator);
    }

    public void ResetPlatform()
    {
        if (breakRoutine != null)
        {
            StopCoroutine(breakRoutine);
            breakRoutine = null;
        }

        broken = false;
        hasBrokenOnce = false;
        ApplyBrokenState();
    }

    private bool CanBreak()
    {
        if (broken)
        {
            return false;
        }

        return !breakOnlyOnce || !hasBrokenOnce;
    }

    private IEnumerator BreakAfterDelay(GameObject instigator)
    {
        if (breakDelay > 0f)
        {
            yield return new WaitForSeconds(breakDelay);
        }

        BreakPlatform(instigator);
        breakRoutine = null;
    }

    private void BreakPlatform(GameObject instigator)
    {
        if (!CanBreak())
        {
            return;
        }

        broken = true;
        hasBrokenOnce = true;
        ApplyBrokenState();
        PlayBreakEffects();
        ContinueInstigatorFall(instigator);
    }

    private void ApplyBrokenState()
    {
        if (platformCollider != null)
        {
            platformCollider.enabled = !broken;
            platformCollider.isTrigger = false;
        }

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(!broken);
        }

        if (brokenVisual != null)
        {
            brokenVisual.SetActive(broken);
        }
    }

    private void PlayBreakEffects()
    {
        if (breakVfx != null)
        {
            breakVfx.Play();
        }

        if (breakSfx != null)
        {
            AudioSource.PlayClipAtPoint(breakSfx, transform.position);
        }
    }

    private void ContinueInstigatorFall(GameObject instigator)
    {
        Rigidbody2D body = instigator != null ? instigator.GetComponent<Rigidbody2D>() : null;
        if (body == null)
        {
            return;
        }

        float fallSpeed = Mathf.Max(0f, continueFallSpeed);
        body.linearVelocity = new Vector2(body.linearVelocity.x, -fallSpeed);
    }

    private void ResolveReferences()
    {
        if (platformCollider == null)
        {
            platformCollider = GetComponent<Collider2D>();
        }

        if (visualRoot == null)
        {
            Transform visual = transform.Find("Visual");
            visualRoot = visual != null ? visual : null;
        }

        if (brokenVisual == null)
        {
            Transform brokenTransform = transform.Find("BrokenVisual");
            brokenVisual = brokenTransform != null ? brokenTransform.gameObject : null;
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        breakDelay = Mathf.Max(0f, breakDelay);
        continueFallSpeed = Mathf.Max(0f, continueFallSpeed);
        broken = startBroken;
        hasBrokenOnce = startBroken;
        ApplyBrokenState();
    }
}
