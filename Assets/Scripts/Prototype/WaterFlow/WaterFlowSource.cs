using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterFlowSource : MonoBehaviour
{
    private const string GeneratedRootName = "__WaterBlobGenerated";
    private const string SpriteFallbackShader = "Sprites/Default";

    private static Material defaultSpriteMaterial;
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
    [SerializeField] private Material blobMaterial;
    [SerializeField] private Color blobColor = new Color(0.36f, 0.92f, 1f, 0.9f);
    [SerializeField] private Color blobEdgeColor = new Color(0.75f, 1f, 1f, 0.32f);
    [SerializeField] private float blobSize = 0.28f;
    [SerializeField] private float blobSizeRandom = 0.06f;
    [SerializeField] private float visualWobbleAmount = 0.045f;
    [SerializeField] private float visualWobbleSpeed = 5.5f;
    [SerializeField] private float fallingStretch = 0.32f;
    [SerializeField] private float surfaceStretch = 0.24f;
    [SerializeField] private bool useMetaballVisual = true;
    [SerializeField] private float metaballCellSize = 0.07f;
    [SerializeField] private float metaballInfluenceScale = 1.9f;
    [SerializeField] private float metaballCoreThreshold = 0.34f;
    [SerializeField] private float metaballEdgeThreshold = 0.16f;
    [SerializeField] private int metaballMaxCells = 14000;
    [SerializeField] private float metaballUpdateInterval = 0.02f;
    [SerializeField] private int sortingOrder = 80;

    [Header("Hazard")]
    [SerializeField] private bool blobIsHazard = true;
    [SerializeField] private float hazardRadiusScale = 0.46f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private readonly List<WaterFlowBlob> activeBlobs = new List<WaterFlowBlob>();
    private Transform generatedRoot;
    private WaterFlowMetaballVisual metaballVisual;
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
    public float FallingStretch => fallingStretch;
    public float SurfaceStretch => surfaceStretch;
    public float BlobLifetime => blobLifetime;
    public bool BlobIsHazard => blobIsHazard;
    public IReadOnlyList<WaterFlowBlob> ActiveBlobs => activeBlobs;
    public Color BlobCoreColor => blobColor;
    public Color BlobEdgeColor => blobEdgeColor;
    public Material BlobVisualMaterial => GetVisibleBlobMaterial(blobMaterial);
    public bool UseMetaballVisual => useMetaballVisual;
    public float MetaballCellSize => metaballCellSize;
    public float MetaballInfluenceScale => metaballInfluenceScale;
    public float MetaballCoreThreshold => metaballCoreThreshold;
    public float MetaballEdgeThreshold => metaballEdgeThreshold;
    public int MetaballMaxCells => metaballMaxCells;
    public float MetaballUpdateInterval => metaballUpdateInterval;
    public int SortingOrder => sortingOrder;

    private void OnEnable()
    {
        ClearGeneratedVisuals();
        ClearLegacyGeneratedChildren();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (spawnBlobsInPlayMode)
        {
            SpawnBlobsIfNeeded();
        }

        CleanupDeadBlobReferences();
        UpdateMetaballVisual();
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
        fallingStretch = Mathf.Max(0f, fallingStretch);
        surfaceStretch = Mathf.Max(0f, surfaceStretch);
        metaballCellSize = Mathf.Max(0.03f, metaballCellSize);
        metaballInfluenceScale = Mathf.Max(0.5f, metaballInfluenceScale);
        metaballCoreThreshold = Mathf.Max(0.01f, metaballCoreThreshold);
        metaballEdgeThreshold = Mathf.Clamp(metaballEdgeThreshold, 0.01f, metaballCoreThreshold);
        metaballMaxCells = Mathf.Max(100, metaballMaxCells);
        metaballUpdateInterval = Mathf.Max(0f, metaballUpdateInterval);
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

        float randomSize = blobSize + Random.Range(-blobSizeRandom, blobSizeRandom);
        randomSize = Mathf.Max(0.02f, randomSize);

        float hazardRadius = Mathf.Max(0.01f, randomSize * hazardRadiusScale);
        float visualRadius = Mathf.Max(0.01f, randomSize * 0.56f);

        WaterFlowBlob blob = blobObject.AddComponent<WaterFlowBlob>();
        blob.Initialize(this, RandomHorizontalDirection(), hazardRadius, visualRadius);

        if (blobIsHazard)
        {
            CircleCollider2D trigger = blobObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = hazardRadius;
            blobObject.AddComponent<WaterHazard>();
        }

        activeBlobs.Add(blob);
    }

    private void UpdateMetaballVisual()
    {
        if (!useMetaballVisual)
        {
            if (metaballVisual != null)
            {
                metaballVisual.Clear();
            }
            return;
        }

        EnsureMetaballVisual();
        metaballVisual.Render(this);
    }

    private void EnsureMetaballVisual()
    {
        EnsureGeneratedRoot();

        if (metaballVisual != null)
        {
            return;
        }

        Transform existing = generatedRoot.Find("WaterMetaballVisual");
        GameObject visualObject = existing != null ? existing.gameObject : new GameObject("WaterMetaballVisual");
        visualObject.transform.SetParent(generatedRoot, false);
        metaballVisual = visualObject.GetComponent<WaterFlowMetaballVisual>();
        if (metaballVisual == null)
        {
            metaballVisual = visualObject.AddComponent<WaterFlowMetaballVisual>();
        }
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
        activeBlobs.Clear();
        generatedRoot = null;
        metaballVisual = null;
    }

    private void ClearLegacyGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!IsLegacyGeneratedChild(child.name))
            {
                continue;
            }

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

    private static bool IsLegacyGeneratedChild(string childName)
    {
        return childName == "WaterVisual_Body" ||
            childName.StartsWith("WaterEdge_", System.StringComparison.Ordinal) ||
            childName.StartsWith("WaterHazard_", System.StringComparison.Ordinal) ||
            childName.StartsWith("WaterCell_", System.StringComparison.Ordinal);
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
            defaultSpriteMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        return defaultSpriteMaterial;
    }

    private static Material GetVisibleBlobMaterial(Material requestedMaterial)
    {
        if (IsUsableSpriteMaterial(requestedMaterial))
        {
            return requestedMaterial;
        }

        return GetDefaultSpriteMaterial();
    }

    private static bool IsUsableSpriteMaterial(Material material)
    {
        if (material == null || material.shader == null)
        {
            return false;
        }

        string shaderName = material.shader.name;
        return !shaderName.Contains("Error") &&
            !shaderName.Contains("Hidden/InternalErrorShader") &&
            !shaderName.Contains("WaterAndFire/WaterBlob");
    }

}

[DisallowMultipleComponent]
public class WaterFlowMetaballVisual : MonoBehaviour
{
    private const string CoreObjectName = "WaterMetaball_Core";
    private const string EdgeObjectName = "WaterMetaball_Edge";
    private const string FallbackShaderName = "Sprites/Default";

    private readonly List<Vector3> vertices = new List<Vector3>(4096);
    private readonly List<int> triangles = new List<int>(8192);
    private readonly List<Color> colors = new List<Color>(4096);
    private readonly List<Vector2> uvs = new List<Vector2>(4096);

    private Mesh coreMesh;
    private Mesh edgeMesh;
    private MeshFilter coreFilter;
    private MeshFilter edgeFilter;
    private MeshRenderer coreRenderer;
    private MeshRenderer edgeRenderer;
    private Material coreMaterial;
    private Material edgeMaterial;
    private readonly Vector2[] cachedCorners = new Vector2[4];
    private readonly float[] cachedValues = new float[4];
    private readonly bool[] cachedInside = new bool[4];
    private readonly Vector3[] cachedPolygon = new Vector3[8];
    private float[] fieldValues = System.Array.Empty<float>();
    private Rect fieldBounds;
    private float fieldCellSize;
    private int fieldColumns;
    private int fieldRows;
    private int fieldVertexColumns;
    private bool fieldReady;
    private float nextRenderTime;

    public void Render(WaterFlowSource source)
    {
        if (source == null || source.ActiveBlobs == null || source.ActiveBlobs.Count == 0)
        {
            Clear();
            return;
        }

        if (ShouldSkipFrame(source))
        {
            return;
        }

        EnsureRenderObjects(source);
        if (!BuildField(source))
        {
            Clear();
            return;
        }

        BuildMeshFromField(source.MetaballEdgeThreshold, edgeMesh, source.BlobEdgeColor);
        BuildMeshFromField(source.MetaballCoreThreshold, coreMesh, source.BlobCoreColor);
        ConfigureRenderers(source);
    }

    public void Clear()
    {
        if (coreMesh != null)
        {
            coreMesh.Clear();
        }

        if (edgeMesh != null)
        {
            edgeMesh.Clear();
        }

        fieldReady = false;
        nextRenderTime = 0f;
    }

    private void EnsureRenderObjects(WaterFlowSource source)
    {
        EnsureMeshObject(CoreObjectName, ref coreFilter, ref coreRenderer, ref coreMesh);
        EnsureMeshObject(EdgeObjectName, ref edgeFilter, ref edgeRenderer, ref edgeMesh);

        if (coreMaterial == null)
        {
            coreMaterial = CreateRuntimeMaterial(source, source.BlobCoreColor, "WaterMetaball_CoreMaterial");
        }

        if (edgeMaterial == null)
        {
            edgeMaterial = CreateRuntimeMaterial(source, source.BlobEdgeColor, "WaterMetaball_EdgeMaterial");
        }
    }

    private void EnsureMeshObject(string objectName, ref MeshFilter filter, ref MeshRenderer meshRenderer, ref Mesh mesh)
    {
        Transform child = transform.Find(objectName);
        GameObject meshObject = child != null ? child.gameObject : new GameObject(objectName);
        meshObject.transform.SetParent(transform, false);

        filter = meshObject.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = meshObject.AddComponent<MeshFilter>();
        }

        meshRenderer = meshObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
        }

        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = objectName + "_Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.MarkDynamic();
        }

        filter.sharedMesh = mesh;
    }

    private void ConfigureRenderers(WaterFlowSource source)
    {
        if (edgeRenderer != null)
        {
            UpdateMaterial(edgeMaterial, source.BlobEdgeColor);
            edgeRenderer.sharedMaterial = edgeMaterial;
            edgeRenderer.sortingOrder = source.SortingOrder - 1;
        }

        if (coreRenderer != null)
        {
            UpdateMaterial(coreMaterial, source.BlobCoreColor);
            coreRenderer.sharedMaterial = coreMaterial;
            coreRenderer.sortingOrder = source.SortingOrder;
        }
    }

    private Material CreateRuntimeMaterial(WaterFlowSource source, Color color, string materialName)
    {
        Shader shader = null;
        Material sourceMaterial = source.BlobVisualMaterial;
        if (sourceMaterial != null && sourceMaterial.shader != null)
        {
            shader = sourceMaterial.shader;
        }

        if (shader == null)
        {
            shader = Shader.Find(FallbackShaderName);
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = Texture2D.whiteTexture,
            color = color
        };
        UpdateMaterial(material, color);
        return material;
    }

    private static void UpdateMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
        }
    }

    private bool ShouldSkipFrame(WaterFlowSource source)
    {
        if (!Application.isPlaying || source.MetaballUpdateInterval <= 0f)
        {
            return false;
        }

        float now = Time.unscaledTime;
        if (now < nextRenderTime)
        {
            return true;
        }

        nextRenderTime = now + source.MetaballUpdateInterval;
        return false;
    }

    private bool BuildField(WaterFlowSource source)
    {
        if (!TryGetBounds(source, out fieldBounds))
        {
            fieldReady = false;
            return false;
        }

        fieldCellSize = Mathf.Max(0.03f, source.MetaballCellSize);
        fieldColumns = Mathf.Max(1, Mathf.CeilToInt(fieldBounds.width / fieldCellSize));
        fieldRows = Mathf.Max(1, Mathf.CeilToInt(fieldBounds.height / fieldCellSize));

        int cellCount = fieldColumns * fieldRows;
        if (cellCount > source.MetaballMaxCells)
        {
            float scale = Mathf.Sqrt(cellCount / (float)source.MetaballMaxCells);
            fieldCellSize *= scale;
            fieldColumns = Mathf.Max(1, Mathf.CeilToInt(fieldBounds.width / fieldCellSize));
            fieldRows = Mathf.Max(1, Mathf.CeilToInt(fieldBounds.height / fieldCellSize));
        }

        fieldVertexColumns = fieldColumns + 1;
        int fieldVertexRows = fieldRows + 1;
        int sampleCount = fieldVertexColumns * fieldVertexRows;
        EnsureFieldCapacity(sampleCount);
        System.Array.Clear(fieldValues, 0, sampleCount);

        float influence = Mathf.Max(0.5f, source.MetaballInfluenceScale);
        bool wroteAnyValue = false;

        foreach (WaterFlowBlob blob in source.ActiveBlobs)
        {
            if (blob == null || blob.VisualAlpha <= 0.01f)
            {
                continue;
            }

            blob.GetMetaballRadii(out float radiusX, out float radiusY);
            radiusX = Mathf.Max(0.01f, radiusX * influence);
            radiusY = Mathf.Max(0.01f, radiusY * influence);

            Vector2 position = blob.Position;
            int minX = Mathf.Clamp(Mathf.FloorToInt((position.x - radiusX - fieldBounds.xMin) / fieldCellSize), 0, fieldVertexColumns - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((position.x + radiusX - fieldBounds.xMin) / fieldCellSize), 0, fieldVertexColumns - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((position.y - radiusY - fieldBounds.yMin) / fieldCellSize), 0, fieldVertexRows - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((position.y + radiusY - fieldBounds.yMin) / fieldCellSize), 0, fieldVertexRows - 1);

            float invRadiusXSqr = 1f / (radiusX * radiusX);
            float invRadiusYSqr = 1f / (radiusY * radiusY);

            for (int y = minY; y <= maxY; y++)
            {
                float sampleY = fieldBounds.yMin + y * fieldCellSize;
                float deltaY = sampleY - position.y;
                float yPart = deltaY * deltaY * invRadiusYSqr;
                int rowStart = y * fieldVertexColumns;

                for (int x = minX; x <= maxX; x++)
                {
                    float sampleX = fieldBounds.xMin + x * fieldCellSize;
                    float deltaX = sampleX - position.x;
                    float q = deltaX * deltaX * invRadiusXSqr + yPart;
                    if (q >= 1f)
                    {
                        continue;
                    }

                    float contribution = 1f - q;
                    fieldValues[rowStart + x] += contribution * contribution * blob.VisualAlpha;
                    wroteAnyValue = true;
                }
            }
        }

        fieldReady = wroteAnyValue;
        return fieldReady;
    }

    private void EnsureFieldCapacity(int sampleCount)
    {
        if (fieldValues.Length >= sampleCount)
        {
            return;
        }

        fieldValues = new float[sampleCount];
    }

    private void BuildMeshFromField(float threshold, Mesh mesh, Color color)
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();
        uvs.Clear();

        if (!fieldReady)
        {
            mesh.Clear();
            return;
        }

        for (int y = 0; y < fieldRows; y++)
        {
            float bottom = fieldBounds.yMin + y * fieldCellSize;
            float top = bottom + fieldCellSize;
            int row = y * fieldVertexColumns;
            int nextRow = (y + 1) * fieldVertexColumns;

            for (int x = 0; x < fieldColumns; x++)
            {
                float left = fieldBounds.xMin + x * fieldCellSize;
                float right = left + fieldCellSize;

                cachedCorners[0] = new Vector2(left, bottom);
                cachedCorners[1] = new Vector2(right, bottom);
                cachedCorners[2] = new Vector2(right, top);
                cachedCorners[3] = new Vector2(left, top);

                cachedValues[0] = fieldValues[row + x];
                cachedValues[1] = fieldValues[row + x + 1];
                cachedValues[2] = fieldValues[nextRow + x + 1];
                cachedValues[3] = fieldValues[nextRow + x];

                int insideCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    cachedInside[i] = cachedValues[i] >= threshold;
                    if (cachedInside[i])
                    {
                        insideCount++;
                    }
                }

                if (insideCount == 0)
                {
                    continue;
                }

                int polygonCount = BuildCellPolygon(cachedCorners, cachedValues, cachedInside, threshold, cachedPolygon);
                AddPolygon(cachedPolygon, polygonCount, color);
            }
        }

        mesh.Clear();
        if (vertices.Count == 0)
        {
            return;
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private bool TryGetBounds(WaterFlowSource source, out Rect bounds)
    {
        bool hasBlob = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        float influence = Mathf.Max(0.5f, source.MetaballInfluenceScale);

        foreach (WaterFlowBlob blob in source.ActiveBlobs)
        {
            if (blob == null || blob.VisualAlpha <= 0.01f)
            {
                continue;
            }

            blob.GetMetaballRadii(out float radiusX, out float radiusY);
            Vector2 position = blob.Position;
            Vector2 extent = new Vector2(radiusX, radiusY) * (influence + 0.25f);

            if (!hasBlob)
            {
                min = position - extent;
                max = position + extent;
                hasBlob = true;
            }
            else
            {
                min = Vector2.Min(min, position - extent);
                max = Vector2.Max(max, position + extent);
            }
        }

        if (!hasBlob)
        {
            bounds = default;
            return false;
        }

        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static int BuildCellPolygon(
        Vector2[] corners,
        float[] values,
        bool[] inside,
        float threshold,
        Vector3[] polygon)
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            if (inside[i])
            {
                polygon[count++] = corners[i];
            }

            if (inside[i] != inside[next])
            {
                polygon[count++] = Interpolate(corners[i], corners[next], values[i], values[next], threshold);
            }
        }

        return count;
    }

    private static Vector3 Interpolate(Vector2 a, Vector2 b, float valueA, float valueB, float threshold)
    {
        float denominator = valueB - valueA;
        float t = Mathf.Abs(denominator) < 0.0001f ? 0.5f : Mathf.Clamp01((threshold - valueA) / denominator);
        return Vector2.Lerp(a, b, t);
    }

    private void AddPolygon(Vector3[] polygon, int count, Color color)
    {
        if (count < 3)
        {
            return;
        }

        int start = vertices.Count;
        for (int i = 0; i < count; i++)
        {
            vertices.Add(transform.InverseTransformPoint(polygon[i]));
            colors.Add(color);
            uvs.Add(Vector2.zero);
        }

        for (int i = 1; i < count - 1; i++)
        {
            triangles.Add(start);
            triangles.Add(start + i);
            triangles.Add(start + i + 1);
        }
    }
}

[DisallowMultipleComponent]
public class WaterFlowBlob : MonoBehaviour
{
    private WaterFlowSource source;
    private Vector2 velocity;
    private float blobRadius = 0.12f;
    private float visualRadius = 0.16f;
    private float visualAlpha = 1f;
    private int horizontalDirection;
    private float age;
    private float phase;
    private bool retiring;
    private bool flowingOnSurface;

    public Vector2 Position => transform.position;
    public Vector2 Velocity => velocity;
    public bool IsFlowingOnSurface => flowingOnSurface;
    public bool IsRetiring => retiring;
    public float VisualAlpha => visualAlpha;

    public void Initialize(WaterFlowSource owner, int initialHorizontalDirection, float radius, float visibleRadius)
    {
        source = owner;
        blobRadius = Mathf.Max(0.01f, radius);
        visualRadius = Mathf.Max(0.01f, visibleRadius);
        horizontalDirection = initialHorizontalDirection == 0 ? 1 : (int)Mathf.Sign(initialHorizontalDirection);
        velocity = new Vector2(horizontalDirection * owner.FallDriftSpeed, -owner.EdgeDropVelocity);
        phase = Random.Range(0f, Mathf.PI * 2f);
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
    }

    private void SimulateMovement(float deltaTime)
    {
        Vector2 position = transform.position;
        float radius = GetWorldRadius();
        float fallDistance = velocity.y < 0f ? -velocity.y * deltaTime : 0f;

        if (velocity.y <= 0.05f && source.TryGetGroundBelow(position, radius, null, fallDistance, out RaycastHit2D groundHit))
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
        flowingOnSurface = true;
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
        if (source.TryCastSolid(position, radius, Vector2.right * direction, Mathf.Abs(velocity.x) * deltaTime + 0.01f, null, out _))
        {
            horizontalDirection = -direction;
            velocity.x = 0f;
            return;
        }

        if (source.TryGetGroundBelow(nextPosition, radius, null, source.LedgeProbeDistance, out RaycastHit2D nextGround))
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
        bool canPreferred = source.CanMoveSide(position, radius, preferred, null);
        bool canOther = source.CanMoveSide(position, radius, -preferred, null);

        if (canPreferred && canOther && !source.HasGroundAhead(position, radius, preferred, null))
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
        flowingOnSurface = false;
        velocity.y = Mathf.Max(velocity.y - source.Gravity * deltaTime, -source.MaxFallSpeed);
        velocity.x = Mathf.MoveTowards(
            velocity.x,
            horizontalDirection * source.FallDriftSpeed,
            source.HorizontalAcceleration * deltaTime);

        Vector2 horizontalDelta = new Vector2(velocity.x * deltaTime, 0f);
        if (Mathf.Abs(horizontalDelta.x) > 0f &&
            source.TryCastSolid(position, radius, Vector2.right * Mathf.Sign(horizontalDelta.x), Mathf.Abs(horizontalDelta.x), null, out _))
        {
            horizontalDirection *= -1;
            velocity.x = 0f;
            horizontalDelta = Vector2.zero;
        }

        position += horizontalDelta;

        float downwardDistance = Mathf.Max(0f, -velocity.y * deltaTime);
        if (downwardDistance > 0f &&
            source.TryCastSolid(position, radius, Vector2.down, downwardDistance + source.GroundProbeDistance, null, out RaycastHit2D groundHit))
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
        return blobRadius;
    }

    public void GetMetaballRadii(out float radiusX, out float radiusY)
    {
        float wobble = Mathf.Sin(Time.time * source.VisualWobbleSpeed + phase) * source.VisualWobbleAmount;
        float stretch;
        float xStretch = wobble;
        float yStretch = -wobble * 0.45f;

        if (flowingOnSurface)
        {
            stretch = source.SpreadSpeed <= 0f ? 0f : Mathf.Clamp01(Mathf.Abs(velocity.x) / source.SpreadSpeed) * source.SurfaceStretch;
            xStretch += stretch;
            yStretch -= stretch * 0.35f;
        }
        else
        {
            stretch = source.MaxFallSpeed <= 0f ? 0f : Mathf.Clamp01(Mathf.Abs(velocity.y) / source.MaxFallSpeed) * source.FallingStretch;
            xStretch -= stretch * 0.28f;
            yStretch += stretch;
        }

        radiusX = visualRadius * Mathf.Max(0.2f, 1f + xStretch);
        radiusY = visualRadius * Mathf.Max(0.2f, 1f + yStretch);
    }

    private void StartRetiring()
    {
        retiring = true;
    }

    private void FadeAndDestroy()
    {
        visualAlpha = Mathf.MoveTowards(visualAlpha, 0f, Time.deltaTime * 3.5f);
        if (visualAlpha <= 0.02f)
        {
            Destroy(gameObject);
        }
    }
}
