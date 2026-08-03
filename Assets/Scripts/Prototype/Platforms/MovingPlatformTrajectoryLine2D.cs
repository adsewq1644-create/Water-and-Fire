using UnityEngine;
using UnityEngine.Rendering;

internal sealed class MovingPlatformTrajectoryLine2D
{
    private const string PreviewObjectName = "__GameTrajectoryPreview";

    private readonly Transform owner;
    private LineRenderer line;
    private Material runtimeMaterial;

    public MovingPlatformTrajectoryLine2D(Transform owner)
    {
        this.owner = owner;
    }

    public void DrawSegment(
        Vector2 start,
        Vector2 end,
        Color color,
        float width,
        int sortingOrder)
    {
        EnsureLine(color, width, sortingOrder);
        line.loop = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.enabled = true;
    }

    public void DrawCircle(
        Vector2 center,
        float radius,
        int segmentCount,
        Color color,
        float width,
        int sortingOrder)
    {
        EnsureLine(color, width, sortingOrder);
        int safeSegmentCount = Mathf.Max(12, segmentCount);
        line.loop = true;
        line.positionCount = safeSegmentCount;

        float angleStep = Mathf.PI * 2f / safeSegmentCount;
        for (int i = 0; i < safeSegmentCount; i++)
        {
            float angle = angleStep * i;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            line.SetPosition(i, point);
        }

        line.enabled = true;
    }

    public void Hide()
    {
        if (line != null)
        {
            line.enabled = false;
        }
    }

    public void Dispose()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(runtimeMaterial);
        }
        else
        {
            Object.DestroyImmediate(runtimeMaterial);
        }

        runtimeMaterial = null;
    }

    private void EnsureLine(Color color, float width, int sortingOrder)
    {
        if (line == null)
        {
            Transform existing = owner.Find(PreviewObjectName);
            GameObject previewObject;
            if (existing != null)
            {
                previewObject = existing.gameObject;
            }
            else
            {
                previewObject = new GameObject(PreviewObjectName);
                previewObject.hideFlags = HideFlags.DontSave;
                previewObject.layer = owner.gameObject.layer;
                previewObject.transform.SetParent(owner, false);
            }

            line = previewObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = previewObject.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                runtimeMaterial = new Material(shader)
                {
                    name = "Moving Platform Trajectory (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                line.sharedMaterial = runtimeMaterial;
            }
        }

        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = Mathf.Max(0.005f, width);
        line.sortingOrder = sortingOrder;
    }
}
