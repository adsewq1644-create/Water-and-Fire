using UnityEngine;

public class ReviveGaugeUI : MonoBehaviour
{
    [SerializeField] private PlayerCharacter target;
    [SerializeField] private Transform fill;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.25f, 0f);

    public void Configure(PlayerCharacter reviveTarget, Transform fillTransform)
    {
        target = reviveTarget;
        fill = fillTransform;
    }

    private void LateUpdate()
    {
        if (target == null || fill == null)
        {
            return;
        }

        transform.position = target.transform.position + worldOffset;
        bool visible = target.IsDeadLike && target.GetReviveNormalized() > 0f;
        if (fill.parent != null)
        {
            fill.parent.gameObject.SetActive(visible);
        }

        float normalized = target.GetReviveNormalized();
        fill.localScale = new Vector3(normalized, 1f, 1f);
        fill.localPosition = new Vector3((normalized - 1f) * 0.5f, 0f, 0f);
    }
}
