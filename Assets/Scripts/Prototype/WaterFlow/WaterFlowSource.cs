using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterFlowSource : MonoBehaviour
{
    private const string GeneratedRootName = "__WaterBlobGenerated";
    private const string LegacyGridRootName = "__WaterGridFlowGenerated";
    private const string LegacyLineRootName = "__WaterFlowGenerated";
    private const string BlobMaterialShader = "WaterAndFire/WaterBlob";
    private const string SpriteFallbackShader = "Sprites/Default";

    private static Material defaultBlobMaterial;
    private static Sprite defaultBlobSprite;

    [Header("Grid Flow")]
    [SerializeField] private float cellSize = 0.45f;
    [SerializeField, Min(1)] private int sourceWidthInCells = 2;
    [SerializeField, Min(1)] private int maxCells = 240;
    [SerializeField, Min(1)] private int maxDepth = 8;
    [SerializeField, Min(1)] private int maxFallCells = 32;
    [SerializeField, Min(0)] private int maxSpreadCells = 10;
    [SerializeField] private bool spreadWhenBlocked = true;
    [SerializeField] private LayerMask solidMask = ~0;
    [SerializeField] private bool ignorePlayerCharacters = true;
    [SerializeField] private bool ignoreTriggerColliders = true;
    [SerializeField] private float solidProbeScale = 0.86f;
    [SerializeField] private bool rebuildInPlayMode = true;
    [SerializeField] private bool rebuildInEditMode = true;
    [SerializeField] private float rebuildInterval = 0.12f;

    [Header("Blob Spawn")]
    [SerializeField] private bool spawnBlobsInPlayMode = true;
    [SerializeField] private float spawnInterval = 0.08f;
    [SerializeField] private int blobsPerSpawn = 1;
    [SerializeField] private int maxActiveBlobs = 90;
    [SerializeField] private float spawnJitter = 0.08f;
    [SerializeField] private float blobLifetime = 6f;

    [Header("Blob Motion")]
    [SerializeField] private float fallSpeed = 3.7f;
    [SerializeField] private float spreadSpeed = 2.1f;
    [SerializeField] private float settleSpeed = 1.2f;
    [SerializeField] private float visualWobbleAmount = 0.055f;
    [SerializeField] private float visualWobbleSpeed = 5.5f;

    [Header("Blob Visual")]
    [SerializeField] private Sprite blobSprite;
    [SerializeField] private Material blobMaterial;
    [SerializeField] private Color blobColor = new Color(0.36f, 0.92f, 1f, 0.76f);
    [SerializeField] private float blobSize = 0.62f;
    [SerializeField] private float blobSizeRandom = 0.16f;
    [SerializeField] private int sortingOrder = 20;

    [Header("Hazard")]
    [SerializeField] private bool blobIsHazard = true;
    [SerializeField] private float hazardRadiusScale = 0.42f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> orderedCells = new List<Vector2Int>();
    private readonly List<WaterFlowBlob> activeBlobs = new List<WaterFlowBlob>();
    private Transform generatedRoot;
    private float nextRebuildTime;
    private float nextSpawnTime;
    private int lastBuildHash;
    private Vector3 lastPosition;
    private int spawnCursor;

    private float CellSize => Mathf.Max(0.05f, cellSize);

    public float FallSpeed => fallSpeed;
    public float SpreadSpeed => spreadSpeed;
    public float SettleSpeed => settleSpeed;
    public float VisualWobbleAmount => visualWobbleAmount;
    public float VisualWobbleSpeed => visualWobbleSpeed;
    public float BlobLifetime => blobLifetime;

    public void RebuildFlow()
    {
        occupiedCells.Clear();
        orderedCells.Clear();

        int width = Mathf.Max(1, sourceWidthInCells);
        int halfWidth = width / 2;
        for (int i = 0; i < width; i++)
        {
            int x = i - halfWidth;
            if (width % 2 == 0 && i >= halfWidth)
            {
                x += 1;
            }

            TraceDown(new Vector2Int(x, 0), maxDepth);
        }

        RememberBuildState();
    }

    public bool TryGetNextBlobCell(Vector2Int currentCell, ref int horizontalDirection, out Vector2Int nextCell)
    {
        Vector2Int below = currentCell + Vector2Int.down;
        if (occupiedCells.Contains(below))
        {
            horizontalDirection = 0;
            nextCell = below;
            return true;
        }

        Vector2Int preferred = currentCell + new Vector2Int(horizontalDirection, 0);
        if (horizontalDirection != 0 && occupiedCells.Contains(preferred))
        {
            nextCell = preferred;
            return true;
        }

        bool canLeft = occupiedCells.Contains(currentCell + Vector2Int.left);
        bool canRight = occupiedCells.Contains(currentCell + Vector2Int.right);
        if (canLeft && canRight)
        {
            horizontalDirection = Random.value < 0.5f ? -1 : 1;
            nextCell = currentCell + new Vector2Int(horizontalDirection, 0);
            return true;
        }

        if (canLeft)
        {
            horizontalDirection = -1;
            nextCell = currentCell + Vector2Int.left;
            return true;
        }

        if (canRight)
        {
            horizontalDirection = 1;
            nextCell = currentCell + Vector2Int.right;
            return true;
        }

        nextCell = currentCell;
        return false;
    }

    public Vector2 CellToWorldPosition(Vector2Int cell)
    {
        return (Vector2)transform.position + new Vector2(cell.x * CellSize, cell.y * CellSize);
    }

    public bool IsFlowCell(Vector2Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    private void OnEnable()
    {
        ClearGeneratedVisuals();
        RebuildFlow();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            if (rebuildInPlayMode && Time.time >= nextRebuildTime)
            {
                nextRebuildTime = Time.time + Mathf.Max(0.02f, rebuildInterval);
                RebuildFlow();
            }

            if (spawnBlobsInPlayMode)
            {
                SpawnBlobsIfNeeded();
            }

            CleanupDeadBlobReferences();
        }
        else
        {
            if (rebuildInEditMode && IsBuildDirty())
            {
                ClearGeneratedVisuals();
                RebuildFlow();
            }
        }
    }

    private void OnDisable()
    {
        ClearGeneratedVisuals();
    }

    private void OnValidate()
    {
        cellSize = Mathf.Max(0.05f, cellSize);
        sourceWidthInCells = Mathf.Max(1, sourceWidthInCells);
        maxCells = Mathf.Max(1, maxCells);
        maxDepth = Mathf.Max(1, maxDepth);
        maxFallCells = Mathf.Max(1, maxFallCells);
        maxSpreadCells = Mathf.Max(0, maxSpreadCells);
        solidProbeScale = Mathf.Clamp(solidProbeScale, 0.1f, 1.25f);
        rebuildInterval = Mathf.Max(0.02f, rebuildInterval);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        blobsPerSpawn = Mathf.Max(1, blobsPerSpawn);
        maxActiveBlobs = Mathf.Max(1, maxActiveBlobs);
        spawnJitter = Mathf.Max(0f, spawnJitter);
        blobLifetime = Mathf.Max(0.2f, blobLifetime);
        fallSpeed = Mathf.Max(0.01f, fallSpeed);
        spreadSpeed = Mathf.Max(0.01f, spreadSpeed);
        settleSpeed = Mathf.Max(0.01f, settleSpeed);
        visualWobbleAmount = Mathf.Max(0f, visualWobbleAmount);
        visualWobbleSpeed = Mathf.Max(0f, visualWobbleSpeed);
        blobSize = Mathf.Max(0.01f, blobSize);
        blobSizeRandom = Mathf.Max(0f, blobSizeRandom);
        hazardRadiusScale = Mathf.Clamp(hazardRadiusScale, 0.05f, 1.5f);
        lastBuildHash = 0;
    }

    private void TraceDown(Vector2Int startCell, int remainingDepth)
    {
        if (remainingDepth <= 0 || occupiedCells.Count >= maxCells)
        {
            return;
        }

        Vector2Int cell = startCell;
        for (int i = 0; i < maxFallCells && occupiedCells.Count < maxCells; i++)
        {
            if (IsSolidCell(cell))
            {
                return;
            }

            AddWaterCell(cell);

            Vector2Int below = cell + Vector2Int.down;
            if (IsSolidCell(below))
            {
                if (spreadWhenBlocked)
                {
                    SpreadSideways(cell, remainingDepth - 1);
                }

                return;
            }

            cell = below;
        }
    }

    private void SpreadSideways(Vector2Int centerCell, int remainingDepth)
    {
        if (remainingDepth <= 0 || maxSpreadCells <= 0)
        {
            return;
        }

        SpreadDirection(centerCell, Vector2Int.left, remainingDepth);
        SpreadDirection(centerCell, Vector2Int.right, remainingDepth);
    }

    private void SpreadDirection(Vector2Int centerCell, Vector2Int direction, int remainingDepth)
    {
        for (int i = 1; i <= maxSpreadCells && occupiedCells.Count < maxCells; i++)
        {
            Vector2Int spreadCell = centerCell + direction * i;
            if (IsSolidCell(spreadCell))
            {
                return;
            }

            AddWaterCell(spreadCell);

            Vector2Int below = spreadCell + Vector2Int.down;
            if (!IsSolidCell(below))
            {
                TraceDown(below, remainingDepth - 1);
                return;
            }
        }
    }

    private void AddWaterCell(Vector2Int cell)
    {
        if (occupiedCells.Count >= maxCells || !occupiedCells.Add(cell))
        {
            return;
        }

        orderedCells.Add(cell);
    }

    private bool IsSolidCell(Vector2Int cell)
    {
        Vector2 worldCenter = CellToWorldPosition(cell);
        Vector2 probeSize = Vector2.one * (CellSize * solidProbeScale);
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, probeSize, 0f, solidMask);
        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (ignoreTriggerColliders && hit.isTrigger)
            {
                continue;
            }

            if (hit.GetComponentInParent<WaterFlowSource>() == this || hit.GetComponentInParent<WaterHazard>() != null)
            {
                continue;
            }

            if (ignorePlayerCharacters && hit.GetComponentInParent<PlayerCharacter>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void SpawnBlobsIfNeeded()
    {
        if (Time.time < nextSpawnTime || occupiedCells.Count == 0)
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

        Vector2Int spawnCell = GetSpawnCell();
        Vector2 spawnPosition = CellToWorldPosition(spawnCell) + Random.insideUnitCircle * spawnJitter;
        GameObject blobObject = new GameObject("WaterBlob");
        blobObject.transform.SetParent(generatedRoot, true);
        blobObject.transform.position = spawnPosition;

        SpriteRenderer renderer = blobObject.AddComponent<SpriteRenderer>();
        renderer.sprite = blobSprite != null ? blobSprite : GetDefaultBlobSprite();
        renderer.sharedMaterial = blobMaterial != null ? blobMaterial : GetDefaultBlobMaterial();
        renderer.color = blobColor;
        renderer.sortingOrder = sortingOrder;

        float randomScale = 1f + Random.Range(-blobSizeRandom, blobSizeRandom);
        blobObject.transform.localScale = Vector3.one * (blobSize * randomScale);

        CircleCollider2D trigger = blobObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = hazardRadiusScale;

        if (blobIsHazard)
        {
            blobObject.AddComponent<WaterHazard>();
        }

        WaterFlowBlob blob = blobObject.AddComponent<WaterFlowBlob>();
        int initialHorizontalDirection = Random.value < 0.5f ? -1 : 1;
        blob.Initialize(this, spawnCell, initialHorizontalDirection);
        activeBlobs.Add(blob);
    }

    private Vector2Int GetSpawnCell()
    {
        if (orderedCells.Count == 0)
        {
            return Vector2Int.zero;
        }

        int width = Mathf.Max(1, sourceWidthInCells);
        int searchCount = Mathf.Min(width, orderedCells.Count);
        int index = spawnCursor % searchCount;
        spawnCursor++;
        return orderedCells[index];
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

    private bool IsBuildDirty()
    {
        return lastBuildHash != GetBuildHash() || lastPosition != transform.position;
    }

    private void RememberBuildState()
    {
        lastBuildHash = GetBuildHash();
        lastPosition = transform.position;
    }

    private int GetBuildHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + cellSize.GetHashCode();
            hash = hash * 31 + sourceWidthInCells;
            hash = hash * 31 + maxCells;
            hash = hash * 31 + maxDepth;
            hash = hash * 31 + maxFallCells;
            hash = hash * 31 + maxSpreadCells;
            hash = hash * 31 + spreadWhenBlocked.GetHashCode();
            hash = hash * 31 + solidMask.value;
            hash = hash * 31 + ignorePlayerCharacters.GetHashCode();
            hash = hash * 31 + ignoreTriggerColliders.GetHashCode();
            hash = hash * 31 + solidProbeScale.GetHashCode();
            return hash;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = blobColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * CellSize);

        if (orderedCells.Count == 0)
        {
            RebuildFlow();
        }

        Gizmos.color = new Color(blobColor.r, blobColor.g, blobColor.b, 0.16f);
        foreach (Vector2Int cell in orderedCells)
        {
            Gizmos.DrawWireCube(CellToWorldPosition(cell), Vector3.one * CellSize * 0.82f);
        }
    }

    private static Material GetDefaultBlobMaterial()
    {
        if (defaultBlobMaterial == null)
        {
            Shader shader = Shader.Find(BlobMaterialShader);
            if (shader == null)
            {
                shader = Shader.Find(SpriteFallbackShader);
            }

            defaultBlobMaterial = new Material(shader);
            defaultBlobMaterial.name = "WaterBlob_DefaultMaterial";
        }

        return defaultBlobMaterial;
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
                float body = 1f - Mathf.SmoothStep(radius * 0.72f, radius, distance);
                float highlight = 1f - Mathf.SmoothStep(0f, radius * 0.5f, distance);
                Color color = Color.Lerp(new Color(0.15f, 0.78f, 0.95f, 0.7f), new Color(0.9f, 1f, 1f, 0.95f), highlight * 0.35f);
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
    private Vector2Int currentCell;
    private Vector2 targetPosition;
    private Vector3 baseScale;
    private int horizontalDirection;
    private float age;
    private float phase;
    private bool retiring;

    public void Initialize(WaterFlowSource owner, Vector2Int startCell, int initialHorizontalDirection)
    {
        source = owner;
        currentCell = startCell;
        horizontalDirection = initialHorizontalDirection == 0 ? 1 : (int)Mathf.Sign(initialHorizontalDirection);
        targetPosition = source.CellToWorldPosition(currentCell);
        phase = Random.Range(0f, Mathf.PI * 2f);
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
            FadeAndDestroy();
            return;
        }

        MoveAlongFlow();
        AnimateBlob();
    }

    private void MoveAlongFlow()
    {
        float speed = GetCurrentSpeed();
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) > 0.015f)
        {
            return;
        }

        if (!source.IsFlowCell(currentCell) || !source.TryGetNextBlobCell(currentCell, ref horizontalDirection, out Vector2Int nextCell))
        {
            retiring = true;
            FadeAndDestroy();
            return;
        }

        currentCell = nextCell;
        targetPosition = source.CellToWorldPosition(currentCell);
    }

    private float GetCurrentSpeed()
    {
        if (retiring)
        {
            return source.SettleSpeed;
        }

        Vector2 direction = targetPosition - (Vector2)transform.position;
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            return source.FallSpeed;
        }

        return source.SpreadSpeed;
    }

    private void AnimateBlob()
    {
        float wobble = Mathf.Sin(Time.time * source.VisualWobbleSpeed + phase) * source.VisualWobbleAmount;
        transform.localScale = new Vector3(
            baseScale.x * (1f + wobble),
            baseScale.y * (1f - wobble * 0.45f),
            baseScale.z);
    }

    private void FadeAndDestroy()
    {
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.MoveTowards(color.a, 0f, Time.deltaTime * 3f);
            spriteRenderer.color = color;
            if (color.a > 0.02f)
            {
                return;
            }
        }

        Destroy(gameObject);
    }
}
