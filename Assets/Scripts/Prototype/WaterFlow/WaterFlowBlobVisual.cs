using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterFlowBlobVisual : MonoBehaviour
{
    [SerializeField] private float pulseAmount = 0.055f;
    [SerializeField] private float pulseSpeed = 1.7f;
    [SerializeField] private float phase;

    private Vector3 baseScale;

    public void Configure(float amount, float speed, float phaseOffset)
    {
        pulseAmount = Mathf.Max(0f, amount);
        pulseSpeed = Mathf.Max(0f, speed);
        phase = phaseOffset;
        baseScale = transform.localScale;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        DisableLegacyGridCell();
    }

    private void OnEnable()
    {
        DisableLegacyGridCell();
    }

    private void Update()
    {
        if (IsLegacyGridCell())
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }

    private void DisableLegacyGridCell()
    {
        if (!IsLegacyGridCell())
        {
            return;
        }

        foreach (Renderer renderer in GetComponents<Renderer>())
        {
            renderer.enabled = false;
        }

        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            collider.enabled = false;
        }
    }

    private bool IsLegacyGridCell()
    {
        return name.StartsWith("WaterCell_", System.StringComparison.Ordinal);
    }
}
