using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class DarkZoneManager : MonoBehaviour
{
    private static DarkZoneManager instance;

    [SerializeField] private Light2D globalLight2D;
    [SerializeField] private float normalLightIntensity = 1f;
    [SerializeField] private float darkLightIntensity = 0.24f;
    [SerializeField] private float transitionDuration = 0.55f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color darkColor = new Color(0.45f, 0.5f, 0.68f, 1f);

    private readonly HashSet<DarkZone> activeZones = new HashSet<DarkZone>();
    private bool capturedInitialLight;

    public static DarkZoneManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<DarkZoneManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject("DarkZoneManager");
            instance = managerObject.AddComponent<DarkZoneManager>();
            return instance;
        }
    }

    public static bool IsLocalViewDark => instance != null && instance.activeZones.Count > 0;

    public static bool TryGetExisting(out DarkZoneManager manager)
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<DarkZoneManager>();
        }

        manager = instance;
        return manager != null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureGlobalLight();
        CaptureInitialLightValues();
    }

    private void OnEnable()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        EnsureGlobalLight();
        if (globalLight2D == null)
        {
            return;
        }

        DarkZone controllingZone = GetControllingZone();
        float targetIntensity = controllingZone != null ? controllingZone.DarkLightIntensity : normalLightIntensity;
        Color targetColor = controllingZone != null ? controllingZone.DarkColor : normalColor;
        float duration = controllingZone != null ? controllingZone.TransitionDuration : transitionDuration;
        float t = duration <= 0f ? 1f : Time.deltaTime / duration;

        globalLight2D.intensity = Mathf.Lerp(globalLight2D.intensity, targetIntensity, t);
        globalLight2D.color = Color.Lerp(globalLight2D.color, targetColor, t);
    }

    public void SetZoneActive(DarkZone zone, bool active)
    {
        if (zone == null)
        {
            return;
        }

        if (active)
        {
            if (zone.GlobalLight2D != null)
            {
                globalLight2D = zone.GlobalLight2D;
                CaptureInitialLightValues();
            }

            activeZones.Add(zone);
        }
        else
        {
            activeZones.Remove(zone);
        }
    }

    public void ForceClearAllZones()
    {
        activeZones.Clear();
    }

    private DarkZone GetControllingZone()
    {
        DarkZone selected = null;
        foreach (DarkZone zone in activeZones)
        {
            if (zone == null || !zone.isActiveAndEnabled)
            {
                continue;
            }

            if (selected == null || zone.DarkLightIntensity < selected.DarkLightIntensity)
            {
                selected = zone;
            }
        }

        return selected;
    }

    private void EnsureGlobalLight()
    {
        if (globalLight2D == null)
        {
            globalLight2D = DarknessLightUtility.FindGlobalLight();
            CaptureInitialLightValues();
        }
    }

    private void CaptureInitialLightValues()
    {
        if (capturedInitialLight || globalLight2D == null)
        {
            return;
        }

        normalLightIntensity = globalLight2D.intensity;
        normalColor = globalLight2D.color;
        capturedInitialLight = true;
    }

    private void OnValidate()
    {
        normalLightIntensity = Mathf.Max(0f, normalLightIntensity);
        darkLightIntensity = Mathf.Max(0f, darkLightIntensity);
        transitionDuration = Mathf.Max(0f, transitionDuration);
    }
}
