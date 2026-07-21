using UnityEngine;

[DisallowMultipleComponent]
public sealed class DarkFogDemoCameraPan2D : MonoBehaviour
{
    [SerializeField] private bool animate = true;
    [SerializeField] private Vector2 panDistance = new Vector2(4f, 1.2f);
    [SerializeField, Min(0.01f)] private float panSpeed = 0.22f;

    private Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (!animate)
        {
            return;
        }

        float phase = Time.time * panSpeed;
        transform.position = startPosition + new Vector3(
            Mathf.Sin(phase) * panDistance.x,
            Mathf.Sin(phase * 0.63f) * panDistance.y,
            0f);
    }
}
