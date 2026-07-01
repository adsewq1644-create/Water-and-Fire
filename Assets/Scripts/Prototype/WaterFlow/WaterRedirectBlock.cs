using UnityEngine;

[DisallowMultipleComponent]
public class WaterRedirectBlock : MonoBehaviour
{
    [SerializeField] private Vector2 outputDirection = new Vector2(1f, -1f);

    public Vector2 OutputDirection
    {
        get
        {
            if (outputDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.down;
            }

            return outputDirection.normalized;
        }
    }

    private void OnValidate()
    {
        if (outputDirection.sqrMagnitude <= 0.0001f)
        {
            outputDirection = Vector2.down;
        }
    }
}
