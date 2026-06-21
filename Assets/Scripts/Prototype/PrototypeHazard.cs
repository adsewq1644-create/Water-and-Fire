using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PrototypeHazard : MonoBehaviour
{
    [SerializeField] private ElementType hazardType = ElementType.Common;

    private void Awake()
    {
        var collider2d = GetComponent<Collider2D>();
        collider2d.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerCharacter>();
        if (player == null || player.IsDeadLike)
        {
            return;
        }

        if (hazardType == ElementType.Common || player.Element != hazardType)
        {
            player.Kill("Hazard");
        }
    }
}
