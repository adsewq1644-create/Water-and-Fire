#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ShockwaveHiddenPlatformRevealSetupEditor
{
    private const string MaterialFolder = "Assets/Materials/Darkness";
    private const string MaterialPath = MaterialFolder + "/ShockwaveRevealSpore.mat";

    private static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/Darkness/ShockwaveHiddenPlatform.prefab",
        "Assets/Prefabs/Darkness/BatNavigation/BatHiddenStaticGround.prefab",
        "Assets/Prefabs/Darkness/BatNavigation/BatHiddenStaticWall.prefab",
        "Assets/Prefabs/Darkness/BatNavigation/BatHiddenSlipperySlope.prefab",
        "Assets/Prefabs/Darkness/BatNavigation/BatHiddenMovingPlatform.prefab"
    };

    [MenuItem("Tools/Water and Fire/Upgrade Hidden Platform Edge Reveal")]
    public static void UpgradeHiddenPlatformRevealPrefabs()
    {
        EnsureFolder("Assets/Materials", "Darkness");
        Material material = CreateOrUpdateSporeMaterial();

        int updatedCount = 0;
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            if (UpgradePrefab(PrefabPaths[i], material))
            {
                updatedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Shockwave edge-spore reveal configured on {updatedCount} hidden-platform prefabs.");
    }

    private static Material CreateOrUpdateSporeMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("WaterAndFire/AmbientDust2D");
        if (shader == null)
        {
            throw new InvalidOperationException("WaterAndFire/AmbientDust2D shader was not found.");
        }

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "ShockwaveRevealSpore"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_Softness"))
        {
            material.SetFloat("_Softness", 0.32f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool UpgradePrefab(string path, Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ShockwaveHiddenPlatform2D target = root.GetComponentInChildren<ShockwaveHiddenPlatform2D>(true);
            if (target == null)
            {
                Debug.LogWarning($"ShockwaveHiddenPlatform2D was not found in {path}.");
                return false;
            }

            Transform generatedFx = target.transform.Find("ShockwaveEdgeRevealFx");
            if (generatedFx != null)
            {
                UnityEngine.Object.DestroyImmediate(generatedFx.gameObject);
            }

            Transform visualRoot = FindVisualRoot(target.transform);
            SpriteRenderer[] renderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<SpriteRenderer>(true)
                : target.GetComponentsInChildren<SpriteRenderer>(true);

            Collider2D[] allColliders = target.GetComponentsInChildren<Collider2D>(true);
            var solidColliders = new List<Collider2D>(allColliders.Length);
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null && !allColliders[i].isTrigger)
                {
                    solidColliders.Add(allColliders[i]);
                }
            }

            SerializedObject serialized = new SerializedObject(target);
            SetObjectArray(serialized.FindProperty("silhouetteRenderers"), renderers);
            serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot != null ? visualRoot : target.transform;
            SetObjectArray(serialized.FindProperty("platformColliders"), solidColliders.ToArray());
            serialized.FindProperty("particleMaterial").objectReferenceValue = material;

            SetFloat(serialized, "revealDuration", 0.8f);
            SetFloat(serialized, "fadeInTime", 0.08f);
            SetFloat(serialized, "fadeOutTime", 0.32f);
            SetFloat(serialized, "minimumRevealInterval", 0.08f);
            SetFloat(serialized, "idleSilhouetteAlpha", 0f);

            SetColor(serialized, "edgeGlowColor", new Color(0.42f, 1.15f, 2.1f, 1f));
            SetFloat(serialized, "edgeGlowAlpha", 0.72f);
            SetFloat(serialized, "edgeGlowWidth", 0.075f);
            SetFloat(serialized, "edgeGlowJitter", 0.035f);
            SetBool(serialized, "edgeGlowTopOnly", true);
            SetFloat(serialized, "edgeGlowCornerBoost", 1.65f);

            SetColor(serialized, "particleColor", new Color(0.62f, 0.88f, 2.2f, 1f));
            SetFloat(serialized, "particleAlpha", 0.82f);
            SetInt(serialized, "particleCount", 38);
            SetFloat(serialized, "particleLifetimeMin", 0.38f);
            SetFloat(serialized, "particleLifetimeMax", 0.85f);
            SetFloat(serialized, "particleSizeMin", 0.025f);
            SetFloat(serialized, "particleSizeMax", 0.085f);
            SetFloat(serialized, "particleSpeedMin", 0.08f);
            SetFloat(serialized, "particleSpeedMax", 0.38f);
            SetFloat(serialized, "particleSpread", 0.48f);
            SetFloat(serialized, "particleNoiseStrength", 0.18f);
            SetFloat(serialized, "edgeParticleMultiplier", 1f);
            SetFloat(serialized, "cornerParticleMultiplier", 2.2f);

            SerializedProperty curve = serialized.FindProperty("distanceFalloffCurve");
            if (curve != null)
            {
                curve.animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
            SetFloat(serialized, "minIntensity", 0.22f);
            SetFloat(serialized, "maxIntensity", 1f);
            SetInt(serialized, "particleSortingOrder", GetParticleSortingOrder(renderers));
            SetBool(serialized, "showEdgeGizmos", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = renderers[i].color;
                color.a = 0f;
                renderers[i].color = color;
                EditorUtility.SetDirty(renderers[i]);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindVisualRoot(Transform root)
    {
        Transform visual = root.Find("VisualRoot");
        if (visual != null)
        {
            return visual;
        }

        Transform shockwaveVisual = root.Find("ShockwaveVisualRoot");
        return shockwaveVisual != null ? shockwaveVisual : root;
    }

    private static int GetParticleSortingOrder(SpriteRenderer[] renderers)
    {
        int order = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            order = Mathf.Max(order, renderers[i].sortingOrder);
        }
        return order + 2;
    }

    private static void SetObjectArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetColor(SerializedObject serialized, string name, Color value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.colorValue = value;
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
