using System.Collections;
using UnityEngine;

public class FallingEmberSpawner2D : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject emberPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnInterval = 1.2f;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private bool autoStart = true;

    [Header("Spread")]
    [SerializeField] private float horizontalRandomRange = 0.2f;
    [SerializeField] private Vector2 initialVelocity = new Vector2(0f, -3f);

    [Header("Water Shutdown")]
    [SerializeField] private bool stopOnWaterProjectile = true;
    [SerializeField] private bool destroyWaterProjectileOnStop = true;
    [SerializeField] private Collider2D waterHitTrigger;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private Coroutine spawnRoutine;
    private bool stoppedByWater;

    public bool IsSpawning => spawnRoutine != null;
    public bool IsStoppedByWater => stoppedByWater;

    public void StartSpawner()
    {
        if (spawnRoutine != null || stoppedByWater)
        {
            return;
        }

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawner()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    public void RestartSpawner()
    {
        stoppedByWater = false;
        StartSpawner();
    }

    public void StopByWater()
    {
        stoppedByWater = true;
        StopSpawner();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureTrigger();
    }

    private void OnEnable()
    {
        if (autoStart)
        {
            StartSpawner();
        }
    }

    private void OnDisable()
    {
        StopSpawner();
    }

    private IEnumerator SpawnLoop()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        while (!stoppedByWater)
        {
            SpawnEmber();
            yield return new WaitForSeconds(spawnInterval);
        }

        spawnRoutine = null;
    }

    private void SpawnEmber()
    {
        if (emberPrefab == null)
        {
            return;
        }

        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        origin.x += Random.Range(-horizontalRandomRange, horizontalRandomRange);

        GameObject ember = Instantiate(emberPrefab, origin, Quaternion.identity);
        FallingEmber2D emberComponent = ember.GetComponent<FallingEmber2D>();
        if (emberComponent != null)
        {
            emberComponent.Initialize(initialVelocity);
            return;
        }

        Rigidbody2D emberBody = ember.GetComponent<Rigidbody2D>();
        if (emberBody != null)
        {
            emberBody.linearVelocity = initialVelocity;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!stopOnWaterProjectile || stoppedByWater || other == null)
        {
            return;
        }

        ElementProjectile projectile = other.GetComponentInParent<ElementProjectile>();
        if (projectile == null || projectile.Element != ElementType.Water)
        {
            return;
        }

        StopByWater();

        if (destroyWaterProjectileOnStop)
        {
            Destroy(projectile.gameObject);
        }
    }

    private void ResolveReferences()
    {
        if (spawnPoint == null)
        {
            Transform found = transform.Find("SpawnPoint");
            if (found != null)
            {
                spawnPoint = found;
            }
        }

        if (waterHitTrigger == null)
        {
            waterHitTrigger = GetComponent<Collider2D>();
        }
    }

    private void ConfigureTrigger()
    {
        if (waterHitTrigger != null)
        {
            waterHitTrigger.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.02f, spawnInterval);
        startDelay = Mathf.Max(0f, startDelay);
        horizontalRandomRange = Mathf.Max(0f, horizontalRandomRange);

        ResolveReferences();
        ConfigureTrigger();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.85f);
        Gizmos.DrawLine(origin + Vector3.left * horizontalRandomRange, origin + Vector3.right * horizontalRandomRange);
        Gizmos.DrawWireSphere(origin, 0.08f);

        Gizmos.color = new Color(1f, 0.75f, 0.05f, 0.5f);
        Gizmos.DrawLine(origin, origin + (Vector3)initialVelocity.normalized * 0.65f);
    }
}
