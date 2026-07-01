using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class VineReleaseBridge2D : MonoBehaviour
{
    [Header("Collider")]
    [SerializeField] private Vector2 bridgeColliderSize = new Vector2(8.6f, 0.28f);
    [SerializeField] private Vector2 bridgeColliderOffset = new Vector2(0f, -1.25f);

    [Header("Bridge Shape")]
    [SerializeField] private float bridgeSpan = 8.4f;
    [SerializeField, Range(2, 24)] private int ropePointCount = 10;
    [SerializeField] private float deckY = -1.25f;
    [SerializeField] private float deckSag = 0.2f;
    [SerializeField] private float handrailHeight = 1.35f;
    [SerializeField] private float supportHeight = 4.18f;
    [SerializeField] private Vector2 deckVisualSize = new Vector2(8.4f, 0.24f);

    [Header("Vine Release State")]
    [SerializeField] private float heldLiftHeight = 2.2f;
    [SerializeField] private bool released;
    [SerializeField] private float dropDuration = 0.35f;
    [SerializeField] private AnimationCurve dropCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private float releaseWobbleAmount = 0.25f;
    [SerializeField] private float releaseWobbleFrequency = 4f;
    [SerializeField] private float releaseWobbleDamping = 4.5f;
    [SerializeField] private float settleTime = 1.1f;

    [Header("Fire Interaction")]
    [SerializeField] private string fireProjectileTag = "FireProjectile";
    [SerializeField] private LayerMask fireProjectileLayer;
    [SerializeField] private bool releaseOnFireHit = true;

    [Header("Simple Rider Reaction")]
    [SerializeField] private bool reactToRiders = true;
    [SerializeField] private LayerMask riderMask = ~0;
    [SerializeField] private Vector2 riderProbeSize = new Vector2(8.6f, 0.35f);
    [SerializeField] private Vector2 riderProbeOffset = new Vector2(0f, 0.315f);
    [SerializeField] private float riderSinkAmount = 0.12f;
    [SerializeField] private float riderWobbleAmount = 0.08f;
    [SerializeField] private float riderWobbleDamping = 8f;
    [SerializeField] private float riderReturnSpeed = 5f;

    [Header("Visual Assets")]
    [SerializeField] private Sprite deckSprite;
    [SerializeField] private Sprite knotSprite;
    [SerializeField] private Sprite anchorSprite;
    [SerializeField] private Material ropeMaterial;

    [Header("Visuals")]
    [SerializeField] private Color deckColor = new Color(0.58f, 0.28f, 0.09f, 1f);
    [SerializeField] private Color ropeColor = new Color(0.86f, 0.46f, 0.14f, 1f);
    [SerializeField] private Color anchorColor = new Color(0.55f, 0.48f, 0.42f, 1f);
    [SerializeField] private float ropeWidth = 0.075f;
    [SerializeField] private float knotScale = 0.18f;
    [SerializeField] private int sortingOrder = 1;

    private const string GeneratedRootName = "__VineReleaseBridgeGenerated";

    private readonly RaycastHit2D[] riderHits = new RaycastHit2D[12];
    private readonly List<Rigidbody2D> carriedBodies = new List<Rigidbody2D>(4);

    private Rigidbody2D body;
    private BoxCollider2D bridgeCollider;
    private Transform generatedRoot;
    private Transform ropeRoot;
    private Transform knotRoot;
    private SpriteRenderer deckRenderer;
    private SpriteRenderer leftAnchorRenderer;
    private SpriteRenderer rightAnchorRenderer;
    private Material defaultRopeMaterial;

    private Vector2 basePosition;
    private float currentGameplayOffset;
    private float releaseStartOffset;
    private float releaseStartedAt;
    private bool releaseDropping;
    private float releaseWobbleStartedAt = -999f;
    private float riderSink;
    private float riderWobbleStartedAt = -999f;
    private bool hadRider;

    public bool Released => released;

    [ContextMenu("Rebuild Bridge Visuals")]
    public void RebuildBridgeVisuals()
    {
        CacheReferences();
        ConfigureBody();
        ApplyColliderSettings();
        if (!Application.isPlaying)
        {
            currentGameplayOffset = released ? 0f : heldLiftHeight;
        }

        RebuildVisuals();
    }

    public void ReleaseVines()
    {
        if (released && !releaseDropping)
        {
            return;
        }

        released = true;
        releaseDropping = true;
        releaseStartOffset = currentGameplayOffset;
        releaseStartedAt = Time.time;
        releaseWobbleStartedAt = Time.time;
    }

    private void Awake()
    {
        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            InitializeRuntime();
        }
        else
        {
            QueueEditorRebuild();
        }
    }

    private void OnValidate()
    {
        bridgeColliderSize = new Vector2(Mathf.Max(0.01f, bridgeColliderSize.x), Mathf.Max(0.01f, bridgeColliderSize.y));
        bridgeSpan = Mathf.Max(0.1f, bridgeSpan);
        deckVisualSize = new Vector2(Mathf.Max(0.01f, deckVisualSize.x), Mathf.Max(0.01f, deckVisualSize.y));
        heldLiftHeight = Mathf.Max(0f, heldLiftHeight);
        dropDuration = Mathf.Max(0.01f, dropDuration);
        releaseWobbleFrequency = Mathf.Max(0f, releaseWobbleFrequency);
        releaseWobbleDamping = Mathf.Max(0f, releaseWobbleDamping);
        settleTime = Mathf.Max(0f, settleTime);
        riderProbeSize = new Vector2(Mathf.Max(0.01f, riderProbeSize.x), Mathf.Max(0.01f, riderProbeSize.y));
        riderSinkAmount = Mathf.Max(0f, riderSinkAmount);
        riderWobbleAmount = Mathf.Max(0f, riderWobbleAmount);
        riderWobbleDamping = Mathf.Max(0f, riderWobbleDamping);
        riderReturnSpeed = Mathf.Max(0.01f, riderReturnSpeed);
        ropeWidth = Mathf.Max(0.001f, ropeWidth);
        knotScale = Mathf.Max(0.001f, knotScale);

        CacheReferences();
        ApplyColliderSettings();
        QueueEditorRebuild();
    }

    private void FixedUpdate()
    {
        CacheReferences();
        ApplyColliderSettings();
        ConfigureBody();

        Vector2 before = body.position;
        UpdateRiderReaction(Time.fixedDeltaTime);
        currentGameplayOffset = CalculateGameplayOffset();

        Vector2 targetPosition = basePosition + Vector2.up * currentGameplayOffset;
        Vector2 delta = targetPosition - body.position;

        CollectRiders();
        body.MovePosition(targetPosition);
        CarryRiders(delta);
        UpdateVisualsRuntime();

        if ((body.position - before).sqrMagnitude > 0.000001f)
        {
            transform.hasChanged = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReleaseFrom(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryReleaseFrom(collision.collider);
    }

    private void InitializeRuntime()
    {
        CacheReferences();
        ConfigureBody();
        ApplyColliderSettings();
        currentGameplayOffset = released ? 0f : heldLiftHeight;
        basePosition = body.position - Vector2.up * currentGameplayOffset;
        RebuildVisuals();
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bridgeCollider == null)
        {
            bridgeCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void ConfigureBody()
    {
        if (body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void ApplyColliderSettings()
    {
        if (bridgeCollider == null)
        {
            return;
        }

        bridgeCollider.size = bridgeColliderSize;
        bridgeCollider.offset = bridgeColliderOffset;
        bridgeCollider.isTrigger = false;
    }

    private float CalculateGameplayOffset()
    {
        float liftOffset = released ? 0f : heldLiftHeight;
        if (releaseDropping)
        {
            float t = Mathf.Clamp01((Time.time - releaseStartedAt) / dropDuration);
            float curveT = dropCurve != null ? dropCurve.Evaluate(t) : t;
            liftOffset = Mathf.Lerp(releaseStartOffset, 0f, curveT);
            if (t >= 1f)
            {
                releaseDropping = false;
            }
        }

        return liftOffset + riderSink + GetReleaseWobble() + GetRiderWobble();
    }

    private float GetReleaseWobble()
    {
        if (releaseWobbleStartedAt < -100f || releaseWobbleAmount <= 0f || releaseWobbleFrequency <= 0f)
        {
            return 0f;
        }

        float elapsed = Time.time - releaseWobbleStartedAt;
        if (elapsed > settleTime)
        {
            return 0f;
        }

        float damping = Mathf.Exp(-releaseWobbleDamping * elapsed);
        return Mathf.Sin(elapsed * releaseWobbleFrequency * Mathf.PI * 2f) * releaseWobbleAmount * damping;
    }

    private float GetRiderWobble()
    {
        if (riderWobbleStartedAt < -100f || riderWobbleAmount <= 0f)
        {
            return 0f;
        }

        float elapsed = Time.time - riderWobbleStartedAt;
        float damping = Mathf.Exp(-riderWobbleDamping * elapsed);
        if (damping < 0.005f)
        {
            return 0f;
        }

        return Mathf.Sin(elapsed * 8f * Mathf.PI * 2f) * riderWobbleAmount * damping;
    }

    private void UpdateRiderReaction(float deltaTime)
    {
        bool hasRider = reactToRiders && HasRiderOnBridge();
        if (hasRider && !hadRider)
        {
            riderWobbleStartedAt = Time.time;
        }

        float targetSink = hasRider ? -riderSinkAmount : 0f;
        riderSink = Mathf.MoveTowards(riderSink, targetSink, riderReturnSpeed * deltaTime);
        hadRider = hasRider;
    }

    private bool HasRiderOnBridge()
    {
        if (bridgeCollider == null)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(riderMask);
        filter.useTriggers = false;

        Vector2 probeCenter = GetRiderProbeCenter();
        Vector2 probeSize = GetRiderProbeSize();
        int hitCount = Physics2D.BoxCast(probeCenter, probeSize, 0f, Vector2.zero, filter, riderHits, 0f);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = riderHits[i].collider;
            if (hit == null || hit.isTrigger || hit == bridgeCollider)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerCharacter>() != null || hit.attachedRigidbody != null)
            {
                return true;
            }
        }

        return false;
    }

    private void CollectRiders()
    {
        carriedBodies.Clear();
        if (!reactToRiders || bridgeCollider == null)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(riderMask);
        filter.useTriggers = false;

        Vector2 probeCenter = GetRiderProbeCenter();
        Vector2 probeSize = GetRiderProbeSize();
        int hitCount = Physics2D.BoxCast(probeCenter, probeSize, 0f, Vector2.zero, filter, riderHits, 0f);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = riderHits[i].collider;
            if (hit == null || hit.isTrigger || hit == bridgeCollider)
            {
                continue;
            }

            Rigidbody2D riderBody = hit.attachedRigidbody;
            if (riderBody == null || riderBody == body || riderBody.bodyType == RigidbodyType2D.Static)
            {
                continue;
            }

            if (!carriedBodies.Contains(riderBody))
            {
                carriedBodies.Add(riderBody);
            }
        }
    }

    private void CarryRiders(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0f)
        {
            return;
        }

        for (int i = 0; i < carriedBodies.Count; i++)
        {
            Rigidbody2D riderBody = carriedBodies[i];
            if (riderBody != null)
            {
                riderBody.position += delta;
            }
        }
    }

    private Vector2 GetRiderProbeCenter()
    {
        Vector2 origin = body != null ? body.position : (Vector2)transform.position;
        return origin + bridgeColliderOffset + riderProbeOffset;
    }

    private Vector2 GetRiderProbeSize()
    {
        return new Vector2(Mathf.Max(0.01f, riderProbeSize.x), Mathf.Max(0.01f, riderProbeSize.y));
    }

    private void TryReleaseFrom(Collider2D other)
    {
        if (!releaseOnFireHit || other == null)
        {
            return;
        }

        ElementProjectile projectile = other.GetComponentInParent<ElementProjectile>();
        if (projectile != null && projectile.Element == ElementType.Fire)
        {
            ReleaseVines();
            return;
        }

        bool tagMatches = !string.IsNullOrWhiteSpace(fireProjectileTag) && other.gameObject.tag == fireProjectileTag;
        bool layerMatches = (fireProjectileLayer.value & (1 << other.gameObject.layer)) != 0;
        if (tagMatches || layerMatches)
        {
            ReleaseVines();
        }
    }

    private void RebuildVisuals()
    {
        EnsureGeneratedRoot();
        ClearGeneratedChildren(ropeRoot);
        ClearGeneratedChildren(knotRoot);
        UpdateDeckVisual();
        UpdateAnchorVisuals();
        BuildRopesAndKnots();
    }

    private void UpdateVisualsRuntime()
    {
        EnsureGeneratedRoot();
        int expectedRopeCount = 3 + Mathf.Max(2, ropePointCount);
        int expectedKnotCount = Mathf.Max(2, ropePointCount) * 2;
        if (ropeRoot.childCount != expectedRopeCount || knotRoot.childCount != expectedKnotCount)
        {
            RebuildVisuals();
            return;
        }

        UpdateDeckVisual();
        UpdateAnchorVisuals();
        UpdateRopesAndKnots();
    }

    private void EnsureGeneratedRoot()
    {
        generatedRoot = transform.Find(GeneratedRootName);
        if (generatedRoot == null)
        {
            generatedRoot = new GameObject(GeneratedRootName).transform;
            generatedRoot.SetParent(transform, false);
        }

        Transform deck = generatedRoot.Find("Deck");
        if (deck == null)
        {
            deck = new GameObject("Deck").transform;
            deck.SetParent(generatedRoot, false);
        }

        deckRenderer = deck.GetComponent<SpriteRenderer>();
        if (deckRenderer == null)
        {
            deckRenderer = deck.gameObject.AddComponent<SpriteRenderer>();
        }

        Transform anchors = generatedRoot.Find("Anchors");
        if (anchors == null)
        {
            anchors = new GameObject("Anchors").transform;
            anchors.SetParent(generatedRoot, false);
        }

        leftAnchorRenderer = EnsureSpriteChild(anchors, "LeftAnchor");
        rightAnchorRenderer = EnsureSpriteChild(anchors, "RightAnchor");

        ropeRoot = generatedRoot.Find("Ropes");
        if (ropeRoot == null)
        {
            ropeRoot = new GameObject("Ropes").transform;
            ropeRoot.SetParent(generatedRoot, false);
        }

        knotRoot = generatedRoot.Find("Knots");
        if (knotRoot == null)
        {
            knotRoot = new GameObject("Knots").transform;
            knotRoot.SetParent(generatedRoot, false);
        }
    }

    private SpriteRenderer EnsureSpriteChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
        }

        SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();
        }

        return spriteRenderer;
    }

    private void ClearGeneratedChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void UpdateDeckVisual()
    {
        deckRenderer.sprite = deckSprite;
        deckRenderer.color = deckColor;
        deckRenderer.sortingOrder = sortingOrder;
        deckRenderer.transform.localPosition = new Vector3(0f, deckY, 0f);
        deckRenderer.transform.localScale = new Vector3(deckVisualSize.x, deckVisualSize.y, 1f);
    }

    private void UpdateAnchorVisuals()
    {
        float fixedOffset = GetFixedVisualOffset();
        SetupAnchor(leftAnchorRenderer, new Vector2(-bridgeSpan * 0.5f, deckY + supportHeight - fixedOffset));
        SetupAnchor(rightAnchorRenderer, new Vector2(bridgeSpan * 0.5f, deckY + supportHeight - fixedOffset));
    }

    private void SetupAnchor(SpriteRenderer renderer, Vector2 localPosition)
    {
        renderer.sprite = anchorSprite != null ? anchorSprite : knotSprite;
        renderer.color = anchorColor;
        renderer.sortingOrder = sortingOrder + 2;
        renderer.transform.localPosition = localPosition;
        renderer.transform.localScale = Vector3.one * Mathf.Max(knotScale * 1.5f, 0.01f);
    }

    private void BuildRopesAndKnots()
    {
        int count = Mathf.Max(2, ropePointCount);
        Vector3[] handrailPoints = GetHandrailPoints(count);

        CreateLine("Handrail", handrailPoints);

        float sideFixedOffset = GetFixedVisualOffset();
        CreateLine("LeftSupport", new[]
        {
            new Vector3(-bridgeSpan * 0.5f, deckY + supportHeight - sideFixedOffset, 0f),
            handrailPoints[0]
        });
        CreateLine("RightSupport", new[]
        {
            new Vector3(bridgeSpan * 0.5f, deckY + supportHeight - sideFixedOffset, 0f),
            handrailPoints[count - 1]
        });

        for (int i = 0; i < count; i++)
        {
            Vector3 deckPoint = new Vector3(handrailPoints[i].x, deckY, 0f);
            CreateLine("VerticalRope_" + i, new[] { handrailPoints[i], deckPoint });
            CreateKnot("TopKnot_" + i, handrailPoints[i]);
            CreateKnot("DeckKnot_" + i, deckPoint);
        }
    }

    private void UpdateRopesAndKnots()
    {
        int count = Mathf.Max(2, ropePointCount);
        Vector3[] handrailPoints = GetHandrailPoints(count);
        SetLine("Handrail", handrailPoints);

        float sideFixedOffset = GetFixedVisualOffset();
        SetLine("LeftSupport", new[]
        {
            new Vector3(-bridgeSpan * 0.5f, deckY + supportHeight - sideFixedOffset, 0f),
            handrailPoints[0]
        });
        SetLine("RightSupport", new[]
        {
            new Vector3(bridgeSpan * 0.5f, deckY + supportHeight - sideFixedOffset, 0f),
            handrailPoints[count - 1]
        });

        for (int i = 0; i < count; i++)
        {
            Vector3 deckPoint = new Vector3(handrailPoints[i].x, deckY, 0f);
            SetLine("VerticalRope_" + i, new[] { handrailPoints[i], deckPoint });
            SetKnot("TopKnot_" + i, handrailPoints[i]);
            SetKnot("DeckKnot_" + i, deckPoint);
        }
    }

    private Vector3[] GetHandrailPoints(int count)
    {
        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : i / (float)(count - 1);
            float x = Mathf.Lerp(-bridgeSpan * 0.5f, bridgeSpan * 0.5f, t);
            float sag = Mathf.Sin(t * Mathf.PI) * deckSag;
            points[i] = new Vector3(x, deckY + handrailHeight - sag, 0f);
        }

        return points;
    }

    private LineRenderer CreateLine(string lineName, Vector3[] points)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(ropeRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = ropeWidth;
        line.endWidth = ropeWidth;
        line.startColor = ropeColor;
        line.endColor = ropeColor;
        line.sortingOrder = sortingOrder + 1;
        line.sharedMaterial = ropeMaterial != null ? ropeMaterial : GetDefaultRopeMaterial();
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        return line;
    }

    private void SetLine(string lineName, Vector3[] points)
    {
        Transform lineTransform = ropeRoot.Find(lineName);
        if (lineTransform == null)
        {
            return;
        }

        LineRenderer line = lineTransform.GetComponent<LineRenderer>();
        if (line == null)
        {
            return;
        }

        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = ropeWidth;
        line.endWidth = ropeWidth;
        line.startColor = ropeColor;
        line.endColor = ropeColor;
        line.sortingOrder = sortingOrder + 1;
        line.sharedMaterial = ropeMaterial != null ? ropeMaterial : GetDefaultRopeMaterial();
    }

    private void CreateKnot(string knotName, Vector3 localPosition)
    {
        GameObject knotObject = new GameObject(knotName);
        knotObject.transform.SetParent(knotRoot, false);
        knotObject.transform.localPosition = localPosition;
        knotObject.transform.localScale = Vector3.one * knotScale;
        SpriteRenderer renderer = knotObject.AddComponent<SpriteRenderer>();
        renderer.sprite = knotSprite != null ? knotSprite : deckSprite;
        renderer.color = ropeColor;
        renderer.sortingOrder = sortingOrder + 2;
    }

    private void SetKnot(string knotName, Vector3 localPosition)
    {
        Transform knotTransform = knotRoot.Find(knotName);
        if (knotTransform == null)
        {
            return;
        }

        knotTransform.localPosition = localPosition;
        knotTransform.localScale = Vector3.one * knotScale;
        SpriteRenderer renderer = knotTransform.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = knotSprite != null ? knotSprite : deckSprite;
            renderer.color = ropeColor;
            renderer.sortingOrder = sortingOrder + 2;
        }
    }

    private Material GetDefaultRopeMaterial()
    {
        if (defaultRopeMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            defaultRopeMaterial = new Material(shader);
            defaultRopeMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        return defaultRopeMaterial;
    }

    private float GetFixedVisualOffset()
    {
        if (Application.isPlaying)
        {
            return currentGameplayOffset;
        }

        return released ? 0f : heldLiftHeight;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 1f, 0.4f, 0.8f);
        Vector3 colliderCenter = transform.position + (Vector3)bridgeColliderOffset;
        Gizmos.DrawWireCube(colliderCenter, bridgeColliderSize);

        if (!reactToRiders)
        {
            return;
        }

        Gizmos.color = new Color(0.3f, 0.75f, 1f, 0.65f);
        Vector2 riderCenter = (Vector2)transform.position + bridgeColliderOffset + riderProbeOffset;
        Vector2 riderSize = GetRiderProbeSize();
        Gizmos.DrawWireCube(riderCenter, new Vector3(riderSize.x, riderSize.y, 0f));
    }

    private void QueueEditorRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(gameObject))
        {
            return;
        }

        UnityEditor.EditorApplication.delayCall -= EditorRebuildIfAlive;
        UnityEditor.EditorApplication.delayCall += EditorRebuildIfAlive;
#endif
    }

#if UNITY_EDITOR
    private void EditorRebuildIfAlive()
    {
        if (this == null || Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(gameObject))
        {
            return;
        }

        CacheReferences();
        ApplyColliderSettings();
        RebuildVisuals();
    }
#endif
}
