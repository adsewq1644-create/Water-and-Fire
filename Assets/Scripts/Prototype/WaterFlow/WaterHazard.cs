using UnityEngine;

[DisallowMultipleComponent]
public class WaterHazard : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider2D other)
    {
        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        if (player == null || player.Element != ElementType.Fire || player.IsDeadLike)
        {
            return;
        }

        player.Kill("Water hazard");
    }
}
