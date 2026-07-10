using System;
using System.Collections;
using UnityEngine;

public enum PlantGrowthStage
{
    Sprout,
    SmallTree,
    LargeTree
}

public enum PlantCollisionRole
{
    PassThrough,
    PlatformOrShortWall,
    WallOrBridge
}

[Serializable]
public class PlantStageSettings
{
    public string displayName;
    public Sprite sprite;
    public Color color = Color.white;
    public Vector2 visualScale = Vector2.one;
    public Vector2 visualOffset;
    public PlantCollisionRole collisionRole;
    public Vector2 colliderSize = Vector2.one;
    public Vector2 colliderOffset;
    public Vector2 hitboxSize = Vector2.one;
    public Vector2 hitboxOffset;
}

public class GrowablePlant : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private BoxCollider2D solidCollider;
    [SerializeField] private BoxCollider2D elementHitbox;
    [SerializeField] private PlantEdgeBurnEffect burnEffect;
    [SerializeField] private PlantGrowthEdgeEffect growthEffect;

    [Header("Growth")]
    [SerializeField] private PlantGrowthStage initialStage = PlantGrowthStage.Sprout;
    [SerializeField] private PlantStageSettings[] stages;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.35f;
    [SerializeField, Min(0f)] private float reactionCooldown = 0.08f;
    [SerializeField, Range(0f, 0.3f)] private float transitionPulse = 0.1f;
    [SerializeField] private AnimationCurve transitionCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

    private PlantGrowthStage currentStage;
    private Coroutine transitionRoutine;
    private float nextReactionTime;

#if UNITY_EDITOR
    private bool editorApplyQueued;
#endif

    public PlantGrowthStage CurrentStage => currentStage;
    public PlantCollisionRole CurrentCollisionRole => GetStageSettings(currentStage).collisionRole;
    public bool IsSeedStage => currentStage == PlantGrowthStage.Sprout || CurrentCollisionRole == PlantCollisionRole.PassThrough;
    public bool CanBurnToPreviousStage => !IsSeedStage;

    public bool ApplyElement(ElementType sourceElement)
    {
        if (Time.time < nextReactionTime)
        {
            return false;
        }

        bool changed = sourceElement switch
        {
            ElementType.Water => TryChangeStage(1, sourceElement),
            ElementType.Fire => TryStartFireShrink(),
            _ => false
        };

        if (changed)
        {
            nextReactionTime = Time.time + reactionCooldown;
        }

        return changed;
    }

    public bool Grow()
    {
        return TryChangeStage(1, ElementType.Water);
    }

    public bool Shrink()
    {
        return TryStartFireShrink();
    }

    public void ConfigurePrototype(Sprite placeholderSprite, PlantGrowthStage startingStage)
    {
        CacheReferences();
        stages = CreateDefaultStages(placeholderSprite);
        initialStage = startingStage;
        currentStage = startingStage;
        ApplyStageImmediate(currentStage);
    }

    private void Awake()
    {
        CacheReferences();
        EnsureBurnEffect();
        EnsureGrowthEffect();
        EnsureStageSettings();
        currentStage = ClampStage(initialStage);
        ApplyStageImmediate(currentStage);
    }

    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0f, transitionDuration);
        reactionCooldown = Mathf.Max(0f, reactionCooldown);
        CacheReferences();
        EnsureStageSettings();

        if (!Application.isPlaying)
        {
            currentStage = ClampStage(initialStage);
            QueueEditorStageApply();
        }
    }

    private void QueueEditorStageApply()
    {
#if UNITY_EDITOR
        if (editorApplyQueued)
        {
            return;
        }

        editorApplyQueued = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null)
            {
                return;
            }

            editorApplyQueued = false;
            if (Application.isPlaying)
            {
                return;
            }

            CacheReferences();
            EnsureStageSettings();
            currentStage = ClampStage(initialStage);
            ApplyStageImmediate(currentStage);
        };
#else
        ApplyStageImmediate(currentStage);
#endif
    }

    private bool TryChangeStage(int direction, ElementType sourceElement)
    {
        if (transitionRoutine != null)
        {
            return false;
        }

        int targetIndex = Mathf.Clamp((int)currentStage + direction, 0, stages.Length - 1);
        var targetStage = (PlantGrowthStage)targetIndex;
        if (targetStage == currentStage)
        {
            return false;
        }

        if (!isActiveAndEnabled || GetTransitionDuration(sourceElement) <= 0f)
        {
            currentStage = targetStage;
            ApplyStageImmediate(currentStage);
            transitionRoutine = null;
            return true;
        }

        transitionRoutine = StartCoroutine(AnimateStageChange(targetStage, sourceElement));
        return true;
    }

    private bool TryStartFireShrink()
    {
        if (transitionRoutine != null)
        {
            return false;
        }

        if (!CanBurnToPreviousStage)
        {
            return false;
        }

        int targetIndex = Mathf.Max(0, (int)currentStage - 1);
        var targetStage = (PlantGrowthStage)targetIndex;
        if (targetStage == currentStage)
        {
            return false;
        }

        if (!isActiveAndEnabled || burnEffect == null || burnEffect.Duration <= 0f)
        {
            return TryChangeStage(-1, ElementType.Fire);
        }

        transitionRoutine = StartCoroutine(AnimateBurnShrinkTogether(targetStage));
        return true;
    }

    private IEnumerator AnimateBurnShrinkTogether(PlantGrowthStage targetStage)
    {
        PlantStageSettings target = GetStageSettings(targetStage);
        Vector3 startScale = visualRenderer.transform.localScale;
        Vector3 targetScale = new Vector3(target.visualScale.x, target.visualScale.y, 1f);
        Vector3 startPosition = visualRenderer.transform.localPosition;
        Vector3 targetPosition = new Vector3(target.visualOffset.x, target.visualOffset.y, 0f);
        Color startColor = visualRenderer.color;
        Vector2 startColliderSize = solidCollider.size;
        Vector2 startColliderOffset = solidCollider.offset;
        Vector2 startHitboxSize = elementHitbox.size;
        Vector2 startHitboxOffset = elementHitbox.offset;
        bool targetHasCollision = target.collisionRole != PlantCollisionRole.PassThrough;

        bool burnStarted = burnEffect.BeginBurn();
        if (target.sprite != null)
        {
            visualRenderer.sprite = target.sprite;
        }

        solidCollider.enabled = solidCollider.enabled || targetHasCollision;
        elementHitbox.enabled = true;

        float duration = Mathf.Max(0.01f, burnEffect.Duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float curved = transitionCurve.Evaluate(normalized);
            float melt = Mathf.Sin(normalized * Mathf.PI) * transitionPulse;

            if (burnStarted)
            {
                burnEffect.SetProgress(curved);
            }

            Vector3 scale = Vector3.LerpUnclamped(startScale, targetScale, curved);
            scale.x *= 1f + melt * 0.35f;
            scale.y *= 1f - melt;
            visualRenderer.transform.localScale = scale;
            visualRenderer.transform.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, curved);

            Color baseColor = Color.LerpUnclamped(startColor, target.color, curved);
            Color heatColor = Color.Lerp(baseColor, new Color(1f, 0.28f, 0.04f, 1f), melt * 0.45f);
            visualRenderer.color = heatColor;

            solidCollider.size = Vector2.LerpUnclamped(startColliderSize, target.colliderSize, curved);
            solidCollider.offset = Vector2.LerpUnclamped(startColliderOffset, target.colliderOffset, curved);
            elementHitbox.size = Vector2.LerpUnclamped(startHitboxSize, target.hitboxSize, curved);
            elementHitbox.offset = Vector2.LerpUnclamped(startHitboxOffset, target.hitboxOffset, curved);
            yield return null;
        }

        burnEffect.EndBurn();
        currentStage = targetStage;
        ApplyStageImmediate(targetStage);
        transitionRoutine = null;
    }

    private IEnumerator AnimateStageChange(PlantGrowthStage targetStage, ElementType sourceElement)
    {
        yield return AnimateStageTransition(targetStage, sourceElement);
        transitionRoutine = null;
    }

    private IEnumerator AnimateStageTransition(PlantGrowthStage targetStage, ElementType sourceElement)
    {
        PlantStageSettings target = GetStageSettings(targetStage);
        Vector3 startScale = visualRenderer.transform.localScale;
        Vector3 targetScale = new Vector3(target.visualScale.x, target.visualScale.y, 1f);
        Vector3 startPosition = visualRenderer.transform.localPosition;
        Vector3 targetPosition = new Vector3(target.visualOffset.x, target.visualOffset.y, 0f);
        Color startColor = visualRenderer.color;
        Color reactionColor = sourceElement == ElementType.Water
            ? new Color(0.35f, 0.85f, 1f, 1f)
            : new Color(1f, 0.38f, 0.08f, 1f);

        if (target.sprite != null)
        {
            visualRenderer.sprite = target.sprite;
        }

        bool growthStarted = sourceElement == ElementType.Water
            && growthEffect != null
            && growthEffect.BeginGrowth();
        float duration = Mathf.Max(0.01f, GetTransitionDuration(sourceElement));

        solidCollider.enabled = false;
        ApplyHitbox(target);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float curved = transitionCurve.Evaluate(normalized);
            float pulse = 1f + Mathf.Sin(normalized * Mathf.PI) * transitionPulse;

            if (growthStarted)
            {
                growthEffect.SetProgress(curved);
            }

            visualRenderer.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, curved) * pulse;
            visualRenderer.transform.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, curved);
            Color baseColor = Color.LerpUnclamped(startColor, target.color, curved);
            visualRenderer.color = Color.Lerp(baseColor, reactionColor, Mathf.Sin(normalized * Mathf.PI) * 0.35f);
            yield return null;
        }

        if (growthStarted)
        {
            growthEffect.EndGrowth();
        }

        currentStage = targetStage;
        ApplyStageImmediate(targetStage);
    }

    private void ApplyStageImmediate(PlantGrowthStage stage)
    {
        if (visualRenderer == null || solidCollider == null || elementHitbox == null || stages == null || stages.Length == 0)
        {
            return;
        }

        PlantStageSettings settings = GetStageSettings(stage);
        if (settings.sprite != null)
        {
            visualRenderer.sprite = settings.sprite;
        }

        visualRenderer.color = settings.color;
        visualRenderer.transform.localScale = new Vector3(settings.visualScale.x, settings.visualScale.y, 1f);
        visualRenderer.transform.localPosition = new Vector3(settings.visualOffset.x, settings.visualOffset.y, 0f);

        solidCollider.size = settings.colliderSize;
        solidCollider.offset = settings.colliderOffset;
        solidCollider.isTrigger = false;
        solidCollider.enabled = settings.collisionRole != PlantCollisionRole.PassThrough;
        ApplyHitbox(settings);
    }

    private void ApplyHitbox(PlantStageSettings settings)
    {
        elementHitbox.size = settings.hitboxSize;
        elementHitbox.offset = settings.hitboxOffset;
        elementHitbox.isTrigger = true;
        elementHitbox.enabled = true;
    }

    private PlantStageSettings GetStageSettings(PlantGrowthStage stage)
    {
        int index = Mathf.Clamp((int)stage, 0, stages.Length - 1);
        return stages[index];
    }

    private PlantGrowthStage ClampStage(PlantGrowthStage stage)
    {
        int maxStage = stages == null || stages.Length == 0 ? 0 : stages.Length - 1;
        return (PlantGrowthStage)Mathf.Clamp((int)stage, 0, maxStage);
    }

    private void CacheReferences()
    {
        if (solidCollider == null)
        {
            solidCollider = GetComponent<BoxCollider2D>();
        }

        if (visualRenderer == null)
        {
            Transform visual = transform.Find("Visual");
            visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>(true);
        }

        if (elementHitbox == null)
        {
            Transform hitbox = transform.Find("ElementHitbox");
            if (hitbox != null)
            {
                elementHitbox = hitbox.GetComponent<BoxCollider2D>();
            }
        }

        burnEffect?.Configure(visualRenderer);
        growthEffect?.Configure(visualRenderer);
    }

    private void EnsureBurnEffect()
    {
        if (burnEffect == null)
        {
            burnEffect = GetComponent<PlantEdgeBurnEffect>();
        }

        if (burnEffect == null && Application.isPlaying)
        {
            burnEffect = gameObject.AddComponent<PlantEdgeBurnEffect>();
        }

        burnEffect?.Configure(visualRenderer);
    }

    private void EnsureGrowthEffect()
    {
        if (growthEffect == null)
        {
            growthEffect = GetComponent<PlantGrowthEdgeEffect>();
        }

        if (growthEffect == null && Application.isPlaying)
        {
            growthEffect = gameObject.AddComponent<PlantGrowthEdgeEffect>();
        }

        growthEffect?.Configure(visualRenderer);
    }

    private float GetTransitionDuration(ElementType sourceElement)
    {
        return sourceElement == ElementType.Water && growthEffect != null
            ? growthEffect.Duration
            : transitionDuration;
    }

    private void EnsureStageSettings()
    {
        if (stages != null && stages.Length == 3)
        {
            return;
        }

        Sprite placeholder = visualRenderer != null ? visualRenderer.sprite : null;
        stages = CreateDefaultStages(placeholder);
    }

    private static PlantStageSettings[] CreateDefaultStages(Sprite placeholder)
    {
        return new[]
        {
            new PlantStageSettings
            {
                displayName = "Sprout",
                sprite = placeholder,
                color = new Color(0.48f, 0.9f, 0.32f, 1f),
                visualScale = new Vector2(0.55f, 0.65f),
                visualOffset = new Vector2(0f, 0.325f),
                collisionRole = PlantCollisionRole.PassThrough,
                colliderSize = new Vector2(0.55f, 0.65f),
                colliderOffset = new Vector2(0f, 0.325f),
                hitboxSize = new Vector2(0.7f, 0.8f),
                hitboxOffset = new Vector2(0f, 0.4f)
            },
            new PlantStageSettings
            {
                displayName = "Small Tree",
                sprite = placeholder,
                color = new Color(0.24f, 0.72f, 0.25f, 1f),
                visualScale = new Vector2(1.2f, 1.8f),
                visualOffset = new Vector2(0f, 0.9f),
                collisionRole = PlantCollisionRole.PlatformOrShortWall,
                colliderSize = new Vector2(1.1f, 1.7f),
                colliderOffset = new Vector2(0f, 0.85f),
                hitboxSize = new Vector2(1.35f, 1.95f),
                hitboxOffset = new Vector2(0f, 0.975f)
            },
            new PlantStageSettings
            {
                displayName = "Large Tree",
                sprite = placeholder,
                color = new Color(0.12f, 0.52f, 0.18f, 1f),
                visualScale = new Vector2(1.8f, 3.4f),
                visualOffset = new Vector2(0f, 1.7f),
                collisionRole = PlantCollisionRole.WallOrBridge,
                colliderSize = new Vector2(1.65f, 3.25f),
                colliderOffset = new Vector2(0f, 1.625f),
                hitboxSize = new Vector2(1.95f, 3.55f),
                hitboxOffset = new Vector2(0f, 1.775f)
            }
        };
    }
}
