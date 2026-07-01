using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterFlowSource : MonoBehaviour
{
    private const string GeneratedRootName = "__WaterBlobGenerated";
    private const string LegacyGridRootName = "__WaterGridFlowGenerated";
    private const string LegacyLineRootName = "__WaterFlowGenerated";
    private const string SpriteFallbackShader = "Sprites/Default";

    private static Material defaultSpriteMaterial;
    private static Sprite defaultBlobSprite;

    [Header("Blob Spawn")]
    [SerializeField] private bool spawnBlobsInPlayMode = true;
    [SerializeField] private float spawnInterval = 0.12f;
    [SerializeField] private int blobsPerSpawn = 1;
    [SerializeField] private int maxActiveBlobs = 55;
    [SerializeField] private float spawnWidth = 0.35f;
    [SerializeField] private float spawnJitter = 0.04f;
    [SerializeField] private float blobLifetime = 4.5f;

    [Header("Collision")]
    [SerializeField] private LayerMask solidMask = ~0;
    [SerializeField] private bool ignorePlayerCharacters = true;
    [SerializeField] private bool ignoreTriggerColliders = true;
    [SerializeField] private float groundProbeDistance = 0.1f;
    [SerializeField] private float sideProbeDistance = 0.07f;
    [SerializeField] private float ledgeProbeDistance = 0.22f;
    [SerializeField] private float surfaceClearance = 0.01f;

    [Header("Blob Motion")]
    [SerializeField] private float gravity = 12f;
    [SerializeField] private float maxFallSpeed = 5.5f;
    [SerializeField] private float spreadSpeed = 1.45f;
    [SerializeField] private float fallDriftSpeed = 0.18f;
    [SerializeField] private float horizontalAcceleration = 10f;
    [SerializeField] private float edgeDropVelocity = 1.2f;

    [Header("Blob Visual")]
    [SerializeField] private Sprite blobSprite;
    [SerializeField] private Material blobMaterial;
    [SerializeField] private Color blobColor = new Color(0.36f, 0.92f, 1f, 0.76f);
    [SerializeField] private float blobSize = 0.28f;
    [SerializeField] private float blobSizeRandom = 0.06f;
    [SerializeField] private float visualWobbleAmount = 0.045f;
    [SerializeField] private float visualWobbleSpeed = 5.5f;
    [SerializeField] private int sortingOrder = 20;

    [Header("Hazard")]
    [SerializeField] private bool blobIsHazard = true;
    [SerializeField] private float hazardRadiusScale = 0.46f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private readonly List<WaterFlowBlob> activeBlobs = new List<WaterFlowBlob>();
    private Transform generatedRoot;
    private float nextSpawnTime;

    public float GroundProbeDistance => groundProbeDistance;
    public float LedgeProbeDistance => ledgeProbeDistance;
    public float SurfaceClearance => surfaceClearance;
    public float Gravity => gravity;
    public float MaxFallSpeed => maxFallSpeed;
    public float SpreadSpeed => spreadSpeed;
    public float FallDriftSpeed => fallDriftSpeed;
    public float HorizontalAcceleration => horizontalAcceleration;
    public float EdgeDropVelocity => edgeDropVelocity;
    public float VisualWobbleAmount => visualWobbleAmount;
    public float VisualWobbleSpeed => visualWobbleSpeed;
    public float BlobLifetime => blobLifetime;

    private void OnEnable()
    {
        ClearGeneratedVisuals();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            ClearGeneratedRoot(LegacyGridRootName);
            ClearGeneratedRoot(LegacyLineRootName);
            return;
        }

        if (spawnBlobsInPlayMode)
        {
            SpawnBlobsIfNeeded();
        }

        CleanupDeadBlobReferences();
    }

    private void OnDisable()
    {
        ClearGeneratedVisuals();
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.02f, spawnInterval);
        blobsPerSpawn = Mathf.Max(1, blobsPerSpawn);
        maxActiveBlobs = Mathf.Max(1, maxActiveBlobs);
        spawnWidth = Mathf.Max(0f, spawnWidth);
        spawnJitter = Mathf.Max(0f, spawnJitter);
        blobLifetime = Mathf.Max(0.3f, blobLifetime);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        sideProbeDistance = Mathf.Max(0.01f, sideProbeDistance);
        ledgeProbeDistance = Mathf.Max(0.01f, ledgeProbeDistance);
        surfaceClearance = Mathf.Max(0f, surfaceClearance);
        gravity = Mathf.Max(0.01f, gravity);
        maxFallSpeed = Mathf.Max(0.01f, maxFallSpeed);
        spreadSpeed = Mathf.Max(0f, spreadSpeed);
        fallDriftSpeed = Mathf.Max(0f, fallDriftSpeed);
        horizontalAcceleration = Mathf.Max(0.01f, horizontalAcceleration);
        edgeDropVelocity = Mathf.Max(0f, edgeDropVelocity);
        blobSize = Mathf.Max(0.02f, blobSize);
        blobSizeRandom = Mathf.Max(0f, blobSizeRandom);
        visualWobbleAmount = Mathf.Max(0f, visualWobbleAmount);
        visualWobbleSpeed = Mathf.Max(0f, visualWobbleSpeed);
        hazardRadiusScale = Mathf.Clamp(hazardRadiusScale, 0.05f, 1.5f);
    }

    private void SpawnBlobsIfNeeded()
    {
        if (Time.time < nextSpawnTime)
        {
            return;
        }

        nextSpawnTime = Time.time + spawnInterval;
        CleanupDeadBlobReferences();

        for (int i = 0; i < blobsPerSpawn && activeBlobs.Count < maxActiveBlobs; i++)
        {
            SpawnBlob();
        }
    }

    private void SpawnBlob()
    {
        EnsureGeneratedRoot();

        Vector2 spawnOffset = new Vector2(
            Random.Range(-spawnWidth * 0.5f, spawnWidth * 0.5f),
            Random.Range(-spawnJitter, spawnJitter));

        GameObject blobObject = new GameObject("WaterBlob");
        blobObject.transform.SetParent(generatedRoot, true);
        blobObject.transform.position = (Vector2)transform.position + spawnOffset;

        SpriteRenderer renderer = blobObject.AddComponent<SpriteRenderer>();
        renderer.sprite = blobSprite != null ? blobSprite : GetDefaultBlobSprite();
        renderer.color = blobColor;
        renderer.sortingOrder = sortingOrder;

        Material material = blobMaterial != null ? blobMaterial : GetDefaultSpriteMaterial();
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        float randomSize = blobSize + Random.Range(-blobSizeRandom, blobSizeRandom);
        blobObject.transform.localScale = Vector3.one * Mathf.Max(0.02f, randomSize);

        CircleCollider2D trigger = blobObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = hazardRadiusScale;

        if (blobIsHazard)
        {
            blobObject.AddComponent<WaterHazard>();
        }

        WaterFlowBlob blob = blobObject.AddComponent<WaterFlowBlob>();
        blob.Initialize(this, RandomHorizontalDirection());
        activeBlobs.Add(blob);
    }

    internal int RandomHorizontalDirection()
    {
        return Random.value < 0.5f ? -1 : 1;
    }

    internal bool TryGetGroundBelow(Vector2 origin, float radius, Collider2D selfCollider, float extraDistance, out RaycastHit2D hit)
    {
        float distance = groundProbeDistance + Mathf.Max(0f, extraDistance);
        return TryCastSolid(origin, radius, Vector2.down, distance, selfCollider, out hit);
    }

    internal bool TryCastSolid(
        Vector2 origin,
        float radius,
        Vector2 direction,
        float distance,
        Collider2D selfCollider,
        out RaycastHit2D bestHit)
    {
        bestHit = default;
        if (distance <= 0f || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, direction.normalized, distance, solidMask);
        float bestDistance = float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || !IsSolidCollider(hit.collider, selfCollider))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return bestHit.collider != null;
    }

    internal bool HasGroundAhead(Vector2 origin, float radius, int direction, Collider2D selfCollider)
    {
        Vector2 ahead = origin + Vector2.right * direction * (radius + ledgeProbeDistance);
        return TryGetGroundBelow(ahead, radius * 0.85f, selfCollider, ledgeProbeDistance, out _);
    }

    internal bool CanMoveSide(Vector2 origin, float radius, int direction, Collider2D selfCollider)
    {
        if (direction == 0)
        {
            return false;
        }

        float distance = sideProbeDistance + radius * 0.35f;
        return !TryCastSolid(origin, radius * 0.9f, Vector2.right * direction, distance, selfCollider, out _);
    }

    private bool IsSolidCollider(Collider2D hit, Collider2D selfCollider)
    {
        if (hit == null || hit == selfCollider)
        {
            return false;
        }

        if (ignoreTriggerColliders && hit.isTrigger)
        {
            return false;
        }

        if (hit.GetComponentInParent<WaterFlowSource>() == this ||
            hit.GetComponentInParent<WaterFlowBlob>() != null ||
            hit.GetComponentInParent<WaterHazard>() != null)
        {
            return false;
        }

        if (ignorePlayerCharacters && hit.GetComponentInParent<PlayerCharacter>() != null)
        {
            return false;
        }

        return true;
    }

    private void CleanupDeadBlobReferences()
    {
        for (int i = activeBlobs.Count - 1; i >= 0; i--)
        {
            if (activeBlobs[i] == null)
            {
                activeBlobs.RemoveAt(i);
            }
        }
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, false);
        generatedRoot = root.transform;
    }

    private void ClearGeneratedVisuals()
    {
        ClearGeneratedRoot(GeneratedRootName);
        ClearGeneratedRoot(LegacyGridRootName);
        ClearGeneratedRoot(LegacyLineRootName);
        activeBlobs.Clear();
        generatedRoot = null;
    }

    private void ClearGeneratedRoot(string rootName)
    {
        Transform root = transform.Find(rootName);
        if (root == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(root.gameObject);
        }
        else
        {
            DestroyImmediate(root.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = blobColor;
        Vector3 size = new Vector3(Mathf.Max(0.05f, spawnWidth), 0.08f, 0.08f);
        Gizmos.DrawWireCube(transform.position, size);

        Gizmos.color = new Color(blobColor.r, blobColor.g, blobColor.b, 0.28f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundProbeDistance + ledgeProbeDistance));
    }

    private static Material GetDefaultSpriteMaterial()
    {
        if (defaultSpriteMaterial == null)
        {
            Shader shader = Shader.Find(SpriteFallbackShader);
            if (shader == null)
            {
                return null;
            }

            defaultSpriteMaterial = new Material(shader);
            defaultSpriteMaterial.name = "WaterBlob_SpriteDefaultMaterial";
        }

        return defaultSpriteMaterial;
    }

    private static Sprite GetDefaultBlobSprite()
    {
        if (defaultBlobSprite != null)
        {
            return defaultBlobSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "WaterBlob_DefaultTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float body = 1f - Mathf.SmoothStep(radius * 0.66f, radius, distance);
                float highlight = 1f - Mathf.SmoothStep(0f, radius * 0.52f, distance);
                Color color = Color.Lerp(
                    new Color(0.16f, 0.78f, 0.95f, 0.68f),
                    new Color(0.92f, 1f, 1f, 0.94f),
                    highlight * 0.34f);
                color.a *= body;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        defaultBlobSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect);
        defaultBlobSprite.name = "WaterBlob_DefaultSprite";
        return defaultBlobSprite;
    }
}

[DisallowMultipleComponent]
public class WaterFlowBlob : MonoBehaviour
{
    private WaterFlowSource source;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;
    private Vector2 velocity;
    private Vector3 baseScale;
    private int horizontalDirection;
    private float age;
    private float phase;
    private bool retiring;

    public void Initialize(WaterFlowSource owner, int initialHorizontalDirection)
    {
        source = owner;
        horizontalDirection = initialHorizontalDirection == 0 ? 1 : (int)Mathf.Sign(initialHorizontalDirection);
        velocity = new Vector2(horizontalDirection * owner.FallDriftSpeed, -owner.EdgeDropVelocity);
        phase = Random.Range(0f, Mathf.PI * 2f);
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        baseScale = transform.localScale;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (source == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age >= source.BlobLifetime)
        {
            StartRetiring();
        }

        if (retiring)
        {
            FadeAndDestroy();
            return;
        }

        SimulateMovement(Time.deltaTime);
        AnimateBlob();
    }

    private void SimulateMovement(float deltaTime)
    {
        Vector2 position = transform.position;
        float radius = GetWorldRadius();
        float fallDistance = velocity.y < 0f ? -velocity.y * deltaTime : 0f;

        if (velocity.y <= 0.05f && source.TryGetGroundBelow(position, radius, circleCollider, fallDistance, out RaycastHit2D groundHit))
        {
            FlowOnSurface(ref position, radius, deltaTime, groundHit);
        }
        else
        {
            Fall(ref position, radius, deltaTime);
        }

        transform.position = position;
    }

    private void FlowOnSurface(ref Vector2 position, float radius, float deltaTime, RaycastHit2D groundHit)
    {
        position = groundHit.centroid + Vector2.up * source.SurfaceClearance;
        velocity.y = 0f;

        int direction = ChooseFlowDirection(position, radius);
        if (direction == 0)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, source.HorizontalAcceleration * deltaTime);
            return;
        }

        horizontalDirection = direction;
        velocity.x = Mathf.MoveTowards(
            velocity.x,
            direction * source.SpreadSpeed,
            source.HorizontalAcceleration * deltaTime);

        Vector2 nextPosition = position + Vector2.right * velocity.x * deltaTime;
        if (source.TryCastSolid(position, radius, Vector2.right * direction, Mathf.Abs(velocity.x) * deltaTime + 0.01f, circleCollider, out _))
        {
            horizontalDirection = -direction;
            velocity.x = 0f;
            return;
        }

        if (source.TryGetGroundBelow(nextPosition, radius, circleCollider, source.LedgeProbeDistance, out RaycastHit2D nextGround))
        {
            position = nextGround.centroid + Vector2.up * source.SurfaceClearance;
            return;
        }

        position = nextPosition;
        velocity.y = -source.EdgeDropVelocity;
    }

    private int ChooseFlowDirection(Vector2 position, float radius)
    {
        int preferred = horizontalDirection == 0 ? source.RandomHorizontalDirection() : horizontalDirection;
        bool canPreferred = source.CanMoveSide(position, radius, preferred, circleCollider);
        bool canOther = source.CanMoveSide(position, radius, -preferred, circleCollider);

        if (canPreferred && canOther && !source.HasGroundAhead(position, radius, preferred, circleCollider))
        {
            return preferred;
        }

        if (canPreferred)
        {
            return preferred;
        }

        if (canOther)
        {
            return -preferred;
        }

        return 0;
    }

    private void Fall(ref Vector2 position, float radius, float deltaTime)
    {
        velocity.y = Mathf.Max(velocity.y - source.Gravity * deltaTime, -source.MaxFallSpeed);
        velocity.x = Mathf.MoveTowards(
            velocity.x,
            horizontalDirection * source.FallDriftSpeed,
            source.HorizontalAcceleration * deltaTime);

        Vector2 horizontalDelta = new Vector2(velocity.x * deltaTime, 0f);
        if (Mathf.Abs(horizontalDelta.x) > 0f &&
            source.TryCastSolid(position, radius, Vector2.right * Mathf.Sign(horizontalDelta.x), Mathf.Abs(horizontalDelta.x), circleCollider, out _))
        {
            horizontalDirection *= -1;
            velocity.x = 0f;
            horizontalDelta = Vector2.zero;
        }

        position += horizontalDelta;

        float downwardDistance = Mathf.Max(0f, -velocity.y * deltaTime);
        if (downwardDistance > 0f &&
            source.TryCastSolid(position, radius, Vector2.down, downwardDistance + source.GroundProbeDistance, circleCollider, out RaycastHit2D groundHit))
        {
            position = groundHit.centroid + Vector2.up * source.SurfaceClearance;
            velocity.y = 0f;
            horizontalDirection = source.RandomHorizontalDirection();
            return;
        }

        position += Vector2.up * velocity.y * deltaTime;
    }

    private float GetWorldRadius()
    {
        if (circleCollider == null)
        {
            return Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.5f;
        }

        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        return circleCollider.radius * scale;
    }

    private void AnimateBlob()
    {
        float wobble = Mathf.Sin(Time.time * source.VisualWobbleSpeed + phase) * source.VisualWobbleAmount;
        transform.localScale = new Vector3(
            baseScale.x * (1f + wobble),
            baseScale.y * (1f - wobble * 0.45f),
            baseScale.z);
    }

    private void StartRetiring()
    {
        retiring = true;
        if (circleCollider != null)
        {
            circleCollider.enabled = false;
        }
    }

    private void FadeAndDestroy()
    {
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.MoveTowards(color.a, 0f, Time.deltaTime * 3.5f);
            spriteRenderer.color = color;
            if (color.a > 0.02f)
            {
                return;
            }
        }

        Destroy(gameObject);
    }
}
