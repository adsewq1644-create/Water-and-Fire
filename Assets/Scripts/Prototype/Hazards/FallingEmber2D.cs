using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class FallingEmber2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float fallSpeed = 5f;
    [SerializeField] private bool useGravity = true;
    [SerializeField] private float lifeTime = 5f;

    [Header("Impact")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private GameObject impactVfx;
    [SerializeField] private float lingerFireDuration = 0.4f;

    [Header("Damage")]
    [SerializeField] private bool killWaterPlayer = true;
    [SerializeField] private bool ignoreFirePlayer = true;

    private Rigidbody2D body;
    private Collider2D emberCollider;
    private SpriteRenderer spriteRenderer;
    private Vector2 directVelocity;
    private bool consumed;

    public void Initialize(Vector2 initialVelocity)
    {
        CacheReferences();
        ConfigureBody();

        if (initialVelocity.sqrMagnitude <= 0.0001f)
        {
            initialVelocity = Vector2.down * fallSpeed;
        }

        if (useGravity)
        {
            body.linearVelocity = initialVelocity;
        }
        else
        {
            directVelocity = initialVelocity.normalized * Mathf.Max(0.01f, fallSpeed);
        }
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureBody();

        if (lifeTime > 0f)
        {
            Destroy(gameObject, lifeTime);
        }
    }

    private void FixedUpdate()
    {
        if (consumed || body == null)
        {
            return;
        }

        if (useGravity)
        {
            if (fallSpeed > 0f && body.linearVelocity.y < -fallSpeed)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, -fallSpeed);
            }

            return;
        }

        Vector2 velocity = directVelocity.sqrMagnitude > 0.0001f
            ? directVelocity
            : Vector2.down * Mathf.Max(0.01f, fallSpeed);
        body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        if (TryHandlePlantContact(other))
        {
            return;
        }

        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        if (player != null)
        {
            HandlePlayerContact(player);
            return;
        }

        if (!other.isTrigger && IsInLayerMask(other.gameObject.layer, groundLayer))
        {
            ImpactAndDestroy();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (consumed || collision == null || collision.collider == null)
        {
            return;
        }

        if (TryHandlePlantContact(collision.collider))
        {
            return;
        }

        PlayerCharacter player = collision.collider.GetComponentInParent<PlayerCharacter>();
        if (player != null)
        {
            HandlePlayerContact(player);
            return;
        }

        if (IsInLayerMask(collision.collider.gameObject.layer, groundLayer))
        {
            ImpactAndDestroy();
        }
    }

    private bool TryHandlePlantContact(Collider2D other)
    {
        GrowablePlant plant = other.GetComponentInParent<GrowablePlant>();
        if (plant == null)
        {
            return false;
        }

        plant.ApplyElement(ElementType.Fire);
        if (!plant.IsSeedStage)
        {
            ImpactAndDestroy();
        }

        return true;
    }

    private void HandlePlayerContact(PlayerCharacter player)
    {
        if (player.Element == ElementType.Fire && ignoreFirePlayer)
        {
            return;
        }

        if (player.Element == ElementType.Water && killWaterPlayer)
        {
            player.Kill("Falling ember");
            ImpactAndDestroy();
            return;
        }

        ImpactAndDestroy();
    }

    private void ImpactAndDestroy()
    {
        if (consumed)
        {
            return;
        }

        consumed = true;

        if (emberCollider != null)
        {
            emberCollider.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        if (impactVfx != null)
        {
            Instantiate(impactVfx, transform.position, Quaternion.identity);
        }
        else
        {
            SpawnFallbackLingerVisual();
        }

        Destroy(gameObject);
    }

    private void SpawnFallbackLingerVisual()
    {
        if (lingerFireDuration <= 0f || spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        GameObject linger = new GameObject("FallingEmber_LingerFire");
        linger.transform.position = transform.position;
        linger.transform.localScale = new Vector3(0.55f, 0.22f, 1f);

        SpriteRenderer lingerRenderer = linger.AddComponent<SpriteRenderer>();
        lingerRenderer.sprite = spriteRenderer.sprite;
        lingerRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        lingerRenderer.color = new Color(1f, 0.36f, 0.05f, 0.75f);
        lingerRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        lingerRenderer.sortingOrder = spriteRenderer.sortingOrder;

        Destroy(linger, lingerFireDuration);
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (emberCollider == null)
        {
            emberCollider = GetComponent<Collider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void ConfigureBody()
    {
        if (emberCollider != null)
        {
            emberCollider.isTrigger = true;
        }

        if (body == null)
        {
            return;
        }

        body.bodyType = useGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        body.gravityScale = useGravity ? 1f : 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.freezeRotation = true;
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnValidate()
    {
        fallSpeed = Mathf.Max(0.01f, fallSpeed);
        lifeTime = Mathf.Max(0f, lifeTime);
        lingerFireDuration = Mathf.Max(0f, lingerFireDuration);

        CacheReferences();
        ConfigureBody();
    }
}
