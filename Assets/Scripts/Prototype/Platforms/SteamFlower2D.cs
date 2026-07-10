using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SteamFlower2D : MonoBehaviour
{
    private enum SteamFlowerState
    {
        Empty,
        Charged,
        Active
    }

    [Header("State")]
    [SerializeField] private float chargedScaleMultiplier = 1.2f;
    [SerializeField] private float stateTransitionTime = 0.12f;

    [Header("Steam")]
    [SerializeField] private float steamDuration = 2f;
    [SerializeField] private float steamLiftSpeed = 8f;
    [SerializeField] private float steamZoneHeight = 3f;
    [SerializeField] private float steamZoneWidth = 1.2f;

    [Header("References")]
    [SerializeField] private Collider2D steamZoneTrigger;
    [SerializeField] private Collider2D elementHitTrigger;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject emptyVisual;
    [SerializeField] private GameObject chargedVisual;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private ParticleSystem waterChargeVfx;
    [SerializeField] private ParticleSystem steamBurstVfx;

    private SteamFlowerState state = SteamFlowerState.Empty;
    private Vector3 visualBaseScale = Vector3.one;
    private Coroutine stateRoutine;
    private Coroutine activeRoutine;

    public bool IsActive => state == SteamFlowerState.Active;

    private void Awake()
    {
        ResolveReferences();
        visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        SetState(SteamFlowerState.Empty, true);
    }

    private void OnEnable()
    {
        ConfigureSteamZone();
        ApplyVisualState(true);
    }

    public void NotifyElementHit(Collider2D other)
    {
        ElementProjectile projectile = other != null ? other.GetComponentInParent<ElementProjectile>() : null;
        if (projectile == null)
        {
            return;
        }

        ElementType element = projectile.Element;
        if (state == SteamFlowerState.Empty && element == ElementType.Water)
        {
            ChargeWithWater();
        }
        else if (state == SteamFlowerState.Charged && element == ElementType.Fire)
        {
            ActivateSteam();
        }

        Destroy(projectile.gameObject);
    }

    public void ApplySteamLift(Collider2D other)
    {
        if (state != SteamFlowerState.Active)
        {
            return;
        }

        PlayerCharacter player = other != null ? other.GetComponentInParent<PlayerCharacter>() : null;
        if (player == null || !player.IsAliveLike)
        {
            return;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, steamLiftSpeed));
    }

    private void ChargeWithWater()
    {
        if (waterChargeVfx != null)
        {
            waterChargeVfx.Play();
        }

        SetState(SteamFlowerState.Charged, false);
    }

    private void ActivateSteam()
    {
        if (activeRoutine != null)
        {
            return;
        }

        if (steamBurstVfx != null)
        {
            steamBurstVfx.Play();
        }

        SetState(SteamFlowerState.Active, false);
        activeRoutine = StartCoroutine(ActiveTimer());
    }

    private IEnumerator ActiveTimer()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, steamDuration));
        activeRoutine = null;
        SetState(SteamFlowerState.Empty, false);
    }

    private void SetState(SteamFlowerState nextState, bool instant)
    {
        state = nextState;
        if (steamZoneTrigger != null)
        {
            steamZoneTrigger.enabled = state == SteamFlowerState.Active;
            steamZoneTrigger.isTrigger = true;
        }

        if (elementHitTrigger != null)
        {
            elementHitTrigger.enabled = true;
            elementHitTrigger.isTrigger = true;
        }

        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
            stateRoutine = null;
        }

        if (instant || !Application.isPlaying)
        {
            ApplyVisualState(true);
            return;
        }

        stateRoutine = StartCoroutine(AnimateVisualState());
    }

    private IEnumerator AnimateVisualState()
    {
        if (visualRoot == null)
        {
            ApplyVisualState(true);
            yield break;
        }

        Vector3 fromScale = visualRoot.localScale;
        Vector3 targetScale = GetTargetVisualScale();

        ApplyVisualObjects();

        float duration = Mathf.Max(0f, stateTransitionTime);
        if (duration <= 0f)
        {
            ApplyVisualState(true);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualRoot.localScale = Vector3.LerpUnclamped(fromScale, targetScale, t);
            yield return null;
        }

        ApplyVisualState(true);
        stateRoutine = null;
    }

    private void ApplyVisualState(bool snapScale)
    {
        ApplyVisualObjects();

        if (visualRoot != null && snapScale)
        {
            visualRoot.localScale = GetTargetVisualScale();
        }
    }

    private void ApplyVisualObjects()
    {
        if (emptyVisual != null)
        {
            emptyVisual.SetActive(state == SteamFlowerState.Empty);
        }

        if (chargedVisual != null)
        {
            chargedVisual.SetActive(state == SteamFlowerState.Charged);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(state == SteamFlowerState.Active);
        }
    }

    private Vector3 GetTargetVisualScale()
    {
        return state == SteamFlowerState.Charged
            ? visualBaseScale * Mathf.Max(0.01f, chargedScaleMultiplier)
            : visualBaseScale;
    }

    private void ConfigureSteamZone()
    {
        if (steamZoneTrigger is BoxCollider2D box)
        {
            box.isTrigger = true;
            box.size = new Vector2(Mathf.Max(0.01f, steamZoneWidth), Mathf.Max(0.01f, steamZoneHeight));
            box.offset = Vector2.up * (box.size.y * 0.5f);
        }

        if (elementHitTrigger != null)
        {
            elementHitTrigger.isTrigger = true;
        }
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
        {
            Transform foundVisualRoot = transform.Find("VisualRoot");
            visualRoot = foundVisualRoot != null ? foundVisualRoot : transform;
        }

        if (emptyVisual == null)
        {
            Transform found = transform.Find("VisualRoot/BudVisual_Empty");
            emptyVisual = found != null ? found.gameObject : null;
        }

        if (chargedVisual == null)
        {
            Transform found = transform.Find("VisualRoot/BudVisual_Charged");
            chargedVisual = found != null ? found.gameObject : null;
        }

        if (activeVisual == null)
        {
            Transform found = transform.Find("VisualRoot/BloomVisual_Active");
            activeVisual = found != null ? found.gameObject : null;
        }

        if (steamZoneTrigger == null)
        {
            Transform found = transform.Find("SteamZoneTrigger");
            steamZoneTrigger = found != null ? found.GetComponent<Collider2D>() : null;
        }

        if (elementHitTrigger == null)
        {
            Transform found = transform.Find("ElementHitTrigger");
            elementHitTrigger = found != null ? found.GetComponent<Collider2D>() : null;
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        chargedScaleMultiplier = Mathf.Max(0.01f, chargedScaleMultiplier);
        stateTransitionTime = Mathf.Max(0f, stateTransitionTime);
        steamDuration = Mathf.Max(0f, steamDuration);
        steamLiftSpeed = Mathf.Max(0f, steamLiftSpeed);
        steamZoneHeight = Mathf.Max(0.01f, steamZoneHeight);
        steamZoneWidth = Mathf.Max(0.01f, steamZoneWidth);
        ConfigureSteamZone();
    }
}
