using UnityEngine;

[DisallowMultipleComponent]
public class LeafBouncePlatformChildCollider2D : MonoBehaviour
{
    private LeafBouncePlatform2D platform;

    private void Awake()
    {
        platform = GetComponentInParent<LeafBouncePlatform2D>();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (platform == null)
        {
            platform = GetComponentInParent<LeafBouncePlatform2D>();
        }

        platform?.NotifySlideZoneStay(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (platform == null)
        {
            platform = GetComponentInParent<LeafBouncePlatform2D>();
        }

        platform?.NotifySlideZoneExit(other);
    }
}
