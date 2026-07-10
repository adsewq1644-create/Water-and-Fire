using UnityEngine;

[DisallowMultipleComponent]
public class SteamFlowerTriggerRelay2D : MonoBehaviour
{
    private enum TriggerRole
    {
        ElementHit,
        SteamZone
    }

    [SerializeField] private SteamFlower2D owner;
    [SerializeField] private TriggerRole role;

    private void Awake()
    {
        ResolveOwner();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        ResolveOwner();
        ConfigureCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ResolveOwner();
        if (owner == null || role != TriggerRole.ElementHit)
        {
            return;
        }

        owner.NotifyElementHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ResolveOwner();
        if (owner == null || role != TriggerRole.SteamZone)
        {
            return;
        }

        owner.ApplySteamLift(other);
    }

    private void ResolveOwner()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<SteamFlower2D>();
        }
    }

    private void ConfigureCollider()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }
}
