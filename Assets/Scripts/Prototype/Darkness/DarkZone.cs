using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class DarkZone : MonoBehaviour
{
    private static readonly HashSet<DarkZone> registeredZones = new HashSet<DarkZone>();

    [SerializeField] private Light2D globalLight2D;
    [SerializeField] private float normalLightIntensity = 1f;
    [SerializeField] private float darkLightIntensity = 0.24f;
    [SerializeField] private float transitionDuration = 0.55f;
    [SerializeField] private Color darkColor = new Color(0.45f, 0.5f, 0.68f, 1f);
    [SerializeField] private bool onlyAffectLocalPlayer = true;
    [SerializeField] private bool debugGizmos = true;

    private readonly HashSet<PlayerCharacter> playersInside = new HashSet<PlayerCharacter>();
    private Collider2D zoneCollider;

    public Light2D GlobalLight2D => globalLight2D;
    public float NormalLightIntensity => normalLightIntensity;
    public float DarkLightIntensity => darkLightIntensity;
    public float TransitionDuration => transitionDuration;
    public Color DarkColor => darkColor;
    public bool IsActiveForLocalView => playersInside.Count > 0;

    private void Awake()
    {
        ResolveZoneCollider();
    }

    private void OnEnable()
    {
        ResolveZoneCollider();
        registeredZones.Add(this);
    }

    private void OnDisable()
    {
        registeredZones.Remove(this);
        playersInside.Clear();
        if (DarkZoneManager.TryGetExisting(out DarkZoneManager manager))
        {
            manager.SetZoneActive(this, false);
        }
    }

    public static bool ContainsWorldPoint(Vector2 worldPoint)
    {
        registeredZones.RemoveWhere(zone => zone == null);
        foreach (DarkZone zone in registeredZones)
        {
            if (zone != null && zone.isActiveAndEnabled && zone.ContainsPoint(worldPoint))
            {
                return true;
            }
        }

        return false;
    }

    public bool ContainsPoint(Vector2 worldPoint)
    {
        ResolveZoneCollider();
        return zoneCollider != null &&
               zoneCollider.enabled &&
               zoneCollider.gameObject.activeInHierarchy &&
               zoneCollider.OverlapPoint(worldPoint);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        if (!ShouldAffectPlayer(player))
        {
            return;
        }

        if (playersInside.Add(player))
        {
            DarkZoneManager.Instance.SetZoneActive(this, true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        if (player == null)
        {
            return;
        }

        if (playersInside.Remove(player) && playersInside.Count == 0)
        {
            if (DarkZoneManager.TryGetExisting(out DarkZoneManager manager))
            {
                manager.SetZoneActive(this, false);
            }
        }
    }

    private bool ShouldAffectPlayer(PlayerCharacter player)
    {
        if (player == null)
        {
            return false;
        }

        return !onlyAffectLocalPlayer || DarknessLocalPlayerUtility.IsLocalPlayer(player);
    }

    private void ResolveZoneCollider()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider2D>();
        }

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        normalLightIntensity = Mathf.Max(0f, normalLightIntensity);
        darkLightIntensity = Mathf.Max(0f, darkLightIntensity);
        transitionDuration = Mathf.Max(0f, transitionDuration);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.25f, 0.35f, 0.85f, 0.25f);
        Collider2D collider2d = GetComponent<Collider2D>();
        if (collider2d != null)
        {
            Gizmos.DrawCube(collider2d.bounds.center, collider2d.bounds.size);
        }
    }
}
