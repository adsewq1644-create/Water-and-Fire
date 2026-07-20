using System.Collections;
using NavMeshPlus.Components;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BatNavigationZone2D : MonoBehaviour
{
    [Header("Zone Bounds")]
    [SerializeField] private BoxCollider2D flightBounds;
    [SerializeField] private Vector2 zoneCenter = new Vector2(35.5f, 103f);
    [SerializeField] private Vector2 zoneSize = new Vector2(43f, 28f);
    [SerializeField] private bool showZoneBounds = true;

    [Header("NavMesh")]
    [SerializeField] private NavMeshSurface surface;
    [SerializeField] private bool allowSettledObstacleRebuilds;
    [SerializeField] private float settledRebuildDelay = 0.15f;

    private Coroutine pendingRebuild;

    public NavMeshSurface Surface => surface;
    public Vector2 ZoneCenter => zoneCenter;
    public Vector2 ZoneSize => zoneSize;

    [ContextMenu("Apply Zone Bounds")]
    public void ApplyZoneBounds()
    {
        if (flightBounds == null)
        {
            Transform child = transform.Find("BatFlightBounds");
            flightBounds = child != null ? child.GetComponent<BoxCollider2D>() : null;
        }

        if (flightBounds == null)
        {
            return;
        }

        flightBounds.transform.position = new Vector3(zoneCenter.x, zoneCenter.y, flightBounds.transform.position.z);
        flightBounds.transform.rotation = Quaternion.identity;
        flightBounds.transform.localScale = Vector3.one;
        flightBounds.offset = Vector2.zero;
        flightBounds.size = zoneSize;
        flightBounds.isTrigger = true;
    }

    private void OnEnable()
    {
        MovingNavObstacle2D.SettledForOptionalRebuild += HandleObstacleSettled;
    }

    private void OnDisable()
    {
        MovingNavObstacle2D.SettledForOptionalRebuild -= HandleObstacleSettled;
        if (pendingRebuild != null)
        {
            StopCoroutine(pendingRebuild);
            pendingRebuild = null;
        }
    }

    [ContextMenu("Rebuild Bat NavMesh")]
    public void RebuildNavMesh()
    {
        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
        }

        if (surface != null)
        {
            surface.BuildNavMesh();
        }
    }

    private void HandleObstacleSettled(MovingNavObstacle2D obstacle)
    {
        if (!allowSettledObstacleRebuilds || obstacle == null ||
            !obstacle.UpdateNavMeshWhenSettled || pendingRebuild != null)
        {
            return;
        }

        pendingRebuild = StartCoroutine(RebuildAfterDelay());
    }

    private IEnumerator RebuildAfterDelay()
    {
        yield return new WaitForSecondsRealtime(settledRebuildDelay);
        pendingRebuild = null;
        RebuildNavMesh();
    }

    private void OnValidate()
    {
        zoneSize.x = Mathf.Max(0.5f, zoneSize.x);
        zoneSize.y = Mathf.Max(0.5f, zoneSize.y);
        settledRebuildDelay = Mathf.Max(0f, settledRebuildDelay);
        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
        }

        ApplyZoneBounds();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showZoneBounds)
        {
            return;
        }

        Gizmos.color = new Color(0.1f, 0.9f, 0.85f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(zoneCenter.x, zoneCenter.y, transform.position.z), zoneSize);
    }
}
