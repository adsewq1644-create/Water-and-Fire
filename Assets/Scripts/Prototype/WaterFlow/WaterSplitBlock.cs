using UnityEngine;

[DisallowMultipleComponent]
public class WaterSplitBlock : MonoBehaviour
{
    [Header("Split Shape")]
    [SerializeField] private bool requireTopHit = true;
    [SerializeField] private float topNormalThreshold = 0.55f;
    [SerializeField] private float edgeInset = 0.02f;
    [SerializeField] private float exitYOffset = 0.03f;
    [SerializeField] private bool drawTopSurfaceSegments = true;

    public bool RequireTopHit => requireTopHit;
    public float TopNormalThreshold => topNormalThreshold;
    public float EdgeInset => Mathf.Max(0f, edgeInset);
    public float ExitYOffset => Mathf.Max(0f, exitYOffset);
    public bool DrawTopSurfaceSegments => drawTopSurfaceSegments;

    public bool CanSplit(RaycastHit2D hit)
    {
        return !requireTopHit || hit.normal.y >= topNormalThreshold;
    }

    public void GetTopExitPoints(Collider2D hitCollider, out Vector2 leftPoint, out Vector2 rightPoint)
    {
        Bounds bounds = hitCollider.bounds;
        float inset = Mathf.Min(EdgeInset, bounds.extents.x);
        float topY = bounds.max.y + ExitYOffset;

        leftPoint = new Vector2(bounds.min.x + inset, topY);
        rightPoint = new Vector2(bounds.max.x - inset, topY);
    }
}
