using System.Collections;
using NavMeshPlus.Components;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BatNavigationZone2D : MonoBehaviour
{
    [SerializeField] private NavMeshSurface surface;
    [SerializeField] private bool allowSettledObstacleRebuilds;
    [SerializeField] private float settledRebuildDelay = 0.15f;

    private Coroutine pendingRebuild;

    public NavMeshSurface Surface => surface;

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
        settledRebuildDelay = Mathf.Max(0f, settledRebuildDelay);
        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
        }
    }
}
