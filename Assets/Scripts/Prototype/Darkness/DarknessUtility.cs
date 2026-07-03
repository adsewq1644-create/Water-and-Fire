using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal static class DarknessLocalPlayerUtility
{
    private static readonly string[] LocalPlayerMemberNames =
    {
        "IsLocalPlayer",
        "isLocalPlayer",
        "IsOwner",
        "isOwner",
        "IsLocal",
        "isLocal",
        "IsMine",
        "isMine"
    };

    public static bool IsLocalPlayer(PlayerCharacter player)
    {
        if (player == null)
        {
            return false;
        }

        if (TryReadLocalFlag(player, out bool playerResult))
        {
            return playerResult;
        }

        MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == player)
            {
                continue;
            }

            if (TryReadLocalFlag(behaviour, out bool componentResult))
            {
                return componentResult;
            }
        }

        return true;
    }

    private static bool TryReadLocalFlag(Component component, out bool value)
    {
        value = false;
        Type type = component.GetType();

        foreach (string memberName in LocalPlayerMemberNames)
        {
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(bool))
            {
                value = (bool)property.GetValue(component);
                return true;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(component);
                return true;
            }
        }

        return false;
    }
}

internal static class DarknessLightUtility
{
    public static Light2D FindGlobalLight()
    {
        Light2D[] lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        foreach (Light2D light in lights)
        {
            if (light != null && light.lightType == Light2D.LightType.Global)
            {
                return light;
            }
        }

        return null;
    }

    public static void ConfigurePointLight(Light2D light, float radius, float intensity, Color color)
    {
        if (light == null)
        {
            return;
        }

        light.intensity = Mathf.Max(0f, intensity);
        light.color = color;
        light.pointLightOuterRadius = Mathf.Max(0.01f, radius);
        light.pointLightInnerRadius = Mathf.Max(0f, radius * 0.18f);
    }

    public static void SetSpriteAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }
}
