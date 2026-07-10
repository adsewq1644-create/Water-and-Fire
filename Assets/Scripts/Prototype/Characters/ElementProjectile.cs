using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class ElementProjectile : MonoBehaviour
{
    [SerializeField] private ElementType element;
    [SerializeField] private PlayerCharacter owner;
    [SerializeField] private float lifetime = 6f;

    private bool initialized;

    public ElementType Element => element;
    public PlayerCharacter Owner => owner;

    public void Initialize(ElementType projectileElement, PlayerCharacter projectileOwner, Vector2 velocity, float projectileLifetime, float gravityScale = 1f)
    {
        element = projectileElement;
        owner = projectileOwner;
        lifetime = projectileLifetime;
        initialized = true;

        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(0f, gravityScale);
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.linearVelocity = velocity;

        var collider2d = GetComponent<Collider2D>();
        collider2d.isTrigger = true;

        Destroy(gameObject, lifetime);
    }

    private void Awake()
    {
        var collider2d = GetComponent<Collider2D>();
        collider2d.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized)
        {
            return;
        }

        var plant = other.GetComponentInParent<GrowablePlant>();
        if (plant != null)
        {
            bool changed = plant.ApplyElement(element);
            if (changed || element != ElementType.Fire || !plant.IsSeedStage)
            {
                Destroy(gameObject);
            }

            return;
        }

        var player = other.GetComponentInParent<PlayerCharacter>();
        if (player != null)
        {
            if (player == owner || player.Element == element)
            {
                return;
            }

            player.Kill("Opposite element projectile");
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
