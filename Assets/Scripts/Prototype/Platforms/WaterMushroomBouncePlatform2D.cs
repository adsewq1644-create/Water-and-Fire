using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class WaterMushroomBouncePlatform2D : MonoBehaviour, IDiveImpactReceiver
{
    [Header("State")]
    [SerializeField] private bool startActive;

    [Header("Colliders")]
    [SerializeField] private Collider2D platformCollider;
    [SerializeField] private Collider2D elementHitTrigger;

    [Header("Bounce")]
    [SerializeField] private float bounceVerticalVelocity = 14f;
    [SerializeField] private float bounceHorizontalVelocity = 5f;
    [SerializeField] private float releaseDelay = 0.08f;
    [SerializeField] private float cooldown = 0.15f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float inactiveScale = 1f;
    [SerializeField] private float activeScale = 1.35f;
    [SerializeField] private float growTime = 0.12f;
    [SerializeField] private float shrinkTime = 0.12f;
    [SerializeField] private float compressDepth = 0.18f;
    [SerializeField] private float compressTime = 0.06f;
    [SerializeField] private float recoverTime = 0.12f;
    [SerializeField] private Color inactiveColor = new Color(0.75f, 0.38f, 0.9f, 1f);
    [SerializeField] private Color activeColor = new Color(0.48f, 0.9f, 1f, 1f);
    [SerializeField] private Color steamColor = new Color(0.85f, 0.95f, 1f, 0.75f);

    private Vector3 visualRestLocalPosition;
    private Vector3 visualBaseLocalScale = Vector3.one;
    private SpriteRenderer visualRenderer;
    private Coroutine bounceRoutine;
    private Coroutine scaleRoutine;
    private float lastBounceTime = -999f;
    private bool activeStage;

    private void Awake()
    {
        ResolveReferences();
        visualRestLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        visualBaseLocalScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        SetActiveStage(startActive, true);
    }

    private void OnEnable()
    {
        ApplyColliderState();
        ApplyVisualState(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyProjectile(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplyProjectile(collision.collider);
    }

    public void OnDiveImpact(Vector2 impactPoint, GameObject instigator)
    {
        if (!activeStage || Time.time < lastBounceTime + cooldown || bounceRoutine != null)
        {
            return;
        }

        Rigidbody2D instigatorBody = instigator != null ? instigator.GetComponent<Rigidbody2D>() : null;
        PlayerCharacter player = instigator != null ? instigator.GetComponent<PlayerCharacter>() : null;
        if (player != null)
        {
            player.SuppressDiveLandingStunThisImpact();
        }

        bounceRoutine = StartCoroutine(BounceAfterCompression(instigatorBody, player));
    }

    private void TryApplyProjectile(Collider2D other)
    {
        ElementProjectile projectile = other != null ? other.GetComponentInParent<ElementProjectile>() : null;
        if (projectile == null)
        {
            return;
        }

        if (projectile.Element == ElementType.Water)
        {
            if (!activeStage)
            {
                SetActiveStage(true, false);
            }

            Destroy(projectile.gameObject);
            return;
        }

        if (projectile.Element == ElementType.Fire)
        {
            if (activeStage)
            {
                SpawnSteamPuff();
                SetActiveStage(false, false);
            }

            Destroy(projectile.gameObject);
        }
    }

    private IEnumerator BounceAfterCompression(Rigidbody2D instigatorBody, PlayerCharacter player)
    {
        lastBounceTime = Time.time;

        Vector3 compressedPosition = visualRestLocalPosition + Vector3.down * compressDepth;
        yield return MoveVisual(visualRestLocalPosition, compressedPosition, compressTime);

        if (releaseDelay > 0f)
        {
            yield return new WaitForSeconds(releaseDelay);
        }

        if (instigatorBody != null)
        {
            float horizontalInput = player != null ? player.CurrentMoveInput : 0f;
            Vector2 velocity = Vector2.up * bounceVerticalVelocity;
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                velocity.x = Mathf.Sign(horizontalInput) * bounceHorizontalVelocity;
            }

            if (player != null)
            {
                player.ApplyDiveBounce(velocity);
            }
            else
            {
                instigatorBody.linearVelocity = velocity;
            }
        }

        yield return MoveVisual(compressedPosition, visualRestLocalPosition, recoverTime);
        bounceRoutine = null;
    }

    private IEnumerator MoveVisual(Vector3 from, Vector3 to, float duration)
    {
        if (visualRoot == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            visualRoot.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            visualRoot.localPosition = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        visualRoot.localPosition = to;
    }

    private void SetActiveStage(bool active, bool instant)
    {
        activeStage = active;
        ApplyColliderState();

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        if (instant || !Application.isPlaying)
        {
            ApplyVisualState(true);
            return;
        }

        float duration = active ? growTime : shrinkTime;
        scaleRoutine = StartCoroutine(AnimateVisualScale(duration));
    }

    private IEnumerator AnimateVisualScale(float duration)
    {
        if (visualRoot == null)
        {
            yield break;
        }

        Vector3 fromScale = visualRoot.localScale;
        Color fromColor = visualRenderer != null ? visualRenderer.color : Color.white;
        Vector3 targetScale = GetStageScale();
        Color targetColor = GetStageColor();

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
            if (visualRenderer != null)
            {
                visualRenderer.color = Color.LerpUnclamped(fromColor, targetColor, t);
            }

            yield return null;
        }

        ApplyVisualState(true);
        scaleRoutine = null;
    }

    private void ApplyColliderState()
    {
        if (platformCollider != null)
        {
            platformCollider.enabled = activeStage;
            platformCollider.isTrigger = false;
        }

        if (elementHitTrigger != null)
        {
            elementHitTrigger.enabled = true;
            elementHitTrigger.isTrigger = true;
        }
    }

    private void ApplyVisualState(bool resetPosition)
    {
        if (visualRoot != null)
        {
            if (resetPosition)
            {
                visualRoot.localPosition = visualRestLocalPosition;
            }

            visualRoot.localScale = GetStageScale();
        }

        if (visualRenderer != null)
        {
            visualRenderer.color = GetStageColor();
        }
    }

    private Vector3 GetStageScale()
    {
        return visualBaseLocalScale * Mathf.Max(0.01f, activeStage ? activeScale : inactiveScale);
    }

    private Color GetStageColor()
    {
        return activeStage ? activeColor : inactiveColor;
    }

    private void SpawnSteamPuff()
    {
        Sprite sprite = visualRenderer != null ? visualRenderer.sprite : GamePrototypeManager.Instance != null ? GamePrototypeManager.Instance.BoxSprite : null;
        if (sprite == null)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            var puff = new GameObject("WaterMushroomSteam");
            puff.transform.position = transform.position + new Vector3((i - 1) * 0.18f, 0.35f + i * 0.08f, 0f);
            var renderer = puff.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = steamColor;
            renderer.sortingOrder = visualRenderer != null ? visualRenderer.sortingOrder + 1 : 2;
            StartCoroutine(AnimateSteamPuff(puff.transform, renderer, i * 0.04f));
        }
    }

    private IEnumerator AnimateSteamPuff(Transform puffTransform, SpriteRenderer renderer, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 startPosition = puffTransform.position;
        Vector3 startScale = Vector3.one * 0.18f;
        Vector3 endScale = Vector3.one * 0.42f;

        while (elapsed < duration && puffTransform != null && renderer != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            puffTransform.position = startPosition + Vector3.up * (0.35f * t);
            puffTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            Color color = steamColor;
            color.a *= 1f - t;
            renderer.color = color;
            yield return null;
        }

        if (puffTransform != null)
        {
            Destroy(puffTransform.gameObject);
        }
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
        {
            Transform visual = transform.Find("Visual");
            visualRoot = visual != null ? visual : transform;
        }

        visualRenderer = visualRoot != null ? visualRoot.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();

        if (platformCollider == null)
        {
            Transform platform = transform.Find("PlatformCollider");
            platformCollider = platform != null ? platform.GetComponent<Collider2D>() : GetComponent<Collider2D>();
        }

        if (elementHitTrigger == null)
        {
            Transform hitbox = transform.Find("ElementHitTrigger");
            elementHitTrigger = hitbox != null ? hitbox.GetComponent<Collider2D>() : null;
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        bounceVerticalVelocity = Mathf.Max(0f, bounceVerticalVelocity);
        bounceHorizontalVelocity = Mathf.Max(0f, bounceHorizontalVelocity);
        releaseDelay = Mathf.Max(0f, releaseDelay);
        cooldown = Mathf.Max(0f, cooldown);
        inactiveScale = Mathf.Max(0.01f, inactiveScale);
        activeScale = Mathf.Max(0.01f, activeScale);
        growTime = Mathf.Max(0f, growTime);
        shrinkTime = Mathf.Max(0f, shrinkTime);
        compressDepth = Mathf.Max(0f, compressDepth);
        compressTime = Mathf.Max(0f, compressTime);
        recoverTime = Mathf.Max(0f, recoverTime);
        ApplyColliderState();
    }
}
