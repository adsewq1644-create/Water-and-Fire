using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShadowRockCluster : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bigSilhouetteVisual;
    [SerializeField] private RevealByFireLight[] revealObjects;
    [SerializeField] private bool autoCollectRevealObjects = true;
    [SerializeField] private bool keepPlatformCollidersEnabled = true;
    [SerializeField, Range(0f, 1f)] private float silhouetteAlpha = 0.24f;
    [SerializeField] private bool debugGizmos = true;

    public IReadOnlyList<RevealByFireLight> RevealObjects => revealObjects;

    private void Reset()
    {
        AutoAssignSilhouette();
        CollectRevealObjects();
    }

    private void Awake()
    {
        RefreshCluster();
    }

    private void OnValidate()
    {
        if (autoCollectRevealObjects)
        {
            CollectRevealObjects();
        }

        RefreshCluster();
    }

    public void RefreshCluster()
    {
        if (autoCollectRevealObjects)
        {
            CollectRevealObjects();
        }

        DarknessLightUtility.SetSpriteAlpha(bigSilhouetteVisual, silhouetteAlpha);

        if (keepPlatformCollidersEnabled)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D collider2d in colliders)
            {
                if (collider2d != null)
                {
                    collider2d.enabled = true;
                }
            }
        }
    }

    public void CollectRevealObjects()
    {
        revealObjects = GetComponentsInChildren<RevealByFireLight>(true);
    }

    private void AutoAssignSilhouette()
    {
        if (bigSilhouetteVisual != null)
        {
            return;
        }

        Transform visual = transform.Find("BigSilhouetteVisual");
        if (visual == null)
        {
            visual = transform.Find("SilhouetteVisual");
        }

        bigSilhouetteVisual = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.18f, 0.12f, 0.28f, 0.35f);
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
            }
        }
    }
}
