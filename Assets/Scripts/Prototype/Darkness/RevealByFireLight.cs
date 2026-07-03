using UnityEngine;

[DisallowMultipleComponent]
public class RevealByFireLight : MonoBehaviour
{
    [SerializeField] private SpriteRenderer silhouetteRenderer;
    [SerializeField] private SpriteRenderer revealedRenderer;
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha;
    [SerializeField, Range(0f, 1f)] private float revealedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float silhouetteAlpha = 0.22f;
    [SerializeField] private float revealSpeed = 6f;
    [SerializeField] private float hideSpeed = 3.5f;
    [SerializeField] private bool onlyRevealInDarkZone;
    [SerializeField] private bool debugGizmos = true;

    private float currentRevealAlpha;
    private bool revealed;
    private float nearestRevealDistance = float.PositiveInfinity;

    public bool IsRevealed => revealed;
    public float NearestRevealDistance => nearestRevealDistance;

    private void Reset()
    {
        AutoAssignRenderers();
    }

    private void Awake()
    {
        ApplyImmediateAlpha(false);
    }

    private void Update()
    {
        revealed = ShouldReveal();
        float targetAlpha = revealed ? revealedAlpha : hiddenAlpha;
        float speed = revealed ? revealSpeed : hideSpeed;
        currentRevealAlpha = Mathf.MoveTowards(currentRevealAlpha, targetAlpha, speed * Time.deltaTime);

        DarknessLightUtility.SetSpriteAlpha(silhouetteRenderer, silhouetteAlpha);
        DarknessLightUtility.SetSpriteAlpha(revealedRenderer, currentRevealAlpha);
    }

    public void ApplyImmediateAlpha(bool forceRevealed)
    {
        currentRevealAlpha = forceRevealed ? revealedAlpha : hiddenAlpha;
        DarknessLightUtility.SetSpriteAlpha(silhouetteRenderer, silhouetteAlpha);
        DarknessLightUtility.SetSpriteAlpha(revealedRenderer, currentRevealAlpha);
    }

    private bool ShouldReveal()
    {
        nearestRevealDistance = float.PositiveInfinity;
        if (onlyRevealInDarkZone && !DarkZoneManager.IsLocalViewDark)
        {
            return false;
        }

        Vector2 position = transform.position;
        foreach (FireLightSource source in FireLightSource.ActiveSources)
        {
            if (source == null || !source.CanRevealObjects || !source.IsLightActive)
            {
                continue;
            }

            float distance = Vector2.Distance(position, source.transform.position);
            if (distance < nearestRevealDistance)
            {
                nearestRevealDistance = distance;
            }

            if (distance <= source.RevealRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void AutoAssignRenderers()
    {
        if (silhouetteRenderer == null)
        {
            Transform silhouette = transform.Find("SilhouetteVisual");
            if (silhouette == null)
            {
                silhouette = transform.Find("Silhouette");
            }

            silhouetteRenderer = silhouette != null ? silhouette.GetComponent<SpriteRenderer>() : null;
        }

        if (revealedRenderer == null)
        {
            Transform revealedVisual = transform.Find("RevealedVisual");
            if (revealedVisual == null)
            {
                revealedVisual = transform.Find("Revealed");
            }

            revealedRenderer = revealedVisual != null ? revealedVisual.GetComponent<SpriteRenderer>() : null;
        }
    }

    private void OnValidate()
    {
        revealSpeed = Mathf.Max(0f, revealSpeed);
        hideSpeed = Mathf.Max(0f, hideSpeed);
        AutoAssignRenderers();
        if (!Application.isPlaying)
        {
            ApplyImmediateAlpha(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = revealed
            ? new Color(1f, 0.65f, 0.1f, 0.9f)
            : new Color(0.6f, 0.6f, 0.65f, 0.45f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.35f);
    }
}
