#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class DarkFogAtmosphereSetupEditor
{
    private const string PrefabFolder = "Assets/Prefabs/Darkness/Atmosphere";
    private const string MaterialFolder = "Assets/Materials/Darkness/Atmosphere";
    private const string DemoSceneFolder = "Assets/Scenes/Demos";
    private const string PrefabPath = PrefabFolder + "/DarkFogAtmosphere.prefab";
    private const string FogPrefabPath = PrefabFolder + "/FogRegion.prefab";
    private const string DustPrefabPath = PrefabFolder + "/AmbientDustRegion.prefab";
    private const string DemoScenePath = DemoSceneFolder + "/DarkFogAtmosphereDemo.unity";

    [MenuItem("Tools/Water and Fire/Create Regional Atmosphere Prefabs")]
    public static void CreateAtmospherePackage()
    {
        EnsureFolder("Assets/Prefabs/Darkness", "Atmosphere");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Darkness");
        EnsureFolder("Assets/Materials/Darkness", "Atmosphere");
        EnsureFolder("Assets/Scenes", "Demos");

        Material backgroundMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogBackground.mat",
            "WaterAndFire/DarkFogGradient2D");
        Material staticFogMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogStatic.mat",
            "WaterAndFire/DarkFogLayer2D");
        Material driftingFogMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogDrifting.mat",
            "WaterAndFire/DarkFogLayer2D");
        Material ambientDustMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/AmbientDust.mat",
            "WaterAndFire/AmbientDust2D");
        Material demoGameplayMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogDemoGameplay.mat",
            "WaterAndFire/DarkFogSilhouette2D");
        Material demoMarkerMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogDemoMarker.mat",
            "WaterAndFire/DarkFogSilhouette2D");

        ConfigureMaterialDefaults(
            backgroundMaterial,
            staticFogMaterial,
            driftingFogMaterial,
            ambientDustMaterial,
            demoGameplayMaterial,
            demoMarkerMaterial);

        CreateAtmospherePrefab(backgroundMaterial);
        CreateFogRegionPrefab(staticFogMaterial, driftingFogMaterial);
        CreateAmbientDustRegionPrefab(ambientDustMaterial);
        CreateDemoScene(demoGameplayMaterial, demoMarkerMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Regional atmosphere, fog, ambient dust prefabs, and the isolated demo scene were created.");
    }

    private static void CreateAtmospherePrefab(Material backgroundMaterial)
    {
        GameObject root = new GameObject("DarkFogAtmosphere");
        try
        {
            DarkFogAtmosphere2D atmosphere = root.AddComponent<DarkFogAtmosphere2D>();
            Transform backgroundRoot = CreateGroup(root.transform, "BackgroundRoot", 0f);

            MeshRenderer background = CreateQuad(
                backgroundRoot,
                "Sky_BackgroundGradient",
                new Vector3(0f, 0f, 8f),
                new Vector3(58f, 32f, 1f),
                backgroundMaterial,
                -100);

            Transform farSilhouetteRoot = CreateGroup(backgroundRoot, "FarSilhouettes", 4f);
            Transform midSilhouetteRoot = CreateGroup(backgroundRoot, "MidSilhouettes", 2.8f);

            SerializedObject data = new SerializedObject(atmosphere);
            data.FindProperty("backgroundRenderer").objectReferenceValue = background;
            data.FindProperty("farSilhouetteRoot").objectReferenceValue = farSilhouetteRoot;
            data.FindProperty("midSilhouetteRoot").objectReferenceValue = midSilhouetteRoot;
            SetRendererArray(data.FindProperty("farSilhouetteRenderers"), System.Array.Empty<Renderer>());
            SetRendererArray(data.FindProperty("midSilhouetteRenderers"), System.Array.Empty<Renderer>());
            data.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.ApplyAtmosphere();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateFogRegionPrefab(Material staticFogMaterial, Material driftingFogMaterial)
    {
        GameObject root = new GameObject("FogRegion");
        try
        {
            FogRegion2D fogRegion = root.AddComponent<FogRegion2D>();
            Transform staticFogGroup = CreateGroup(root.transform, "StaticFog", 4.2f);
            MeshRenderer staticFog = CreateQuad(
                staticFogGroup,
                "StaticFog_Region",
                Vector3.zero,
                new Vector3(58f, 28f, 1f),
                staticFogMaterial,
                -78);
            Transform driftingFogGroup = CreateGroup(root.transform, "DriftingFog", 2.2f);
            MeshRenderer driftingFog = CreateQuad(
                driftingFogGroup,
                "DriftingFog_Region",
                Vector3.zero,
                new Vector3(58f, 28f, 1f),
                driftingFogMaterial,
                -52);

            SerializedObject fogData = new SerializedObject(fogRegion);
            SetRendererArray(fogData.FindProperty("staticFogRenderers"), new Renderer[] { staticFog });
            SetRendererArray(fogData.FindProperty("driftingFogRenderers"), new Renderer[] { driftingFog });
            fogData.ApplyModifiedPropertiesWithoutUndo();
            fogRegion.ApplyRegion();

            PrefabUtility.SaveAsPrefabAsset(root, FogPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateAmbientDustRegionPrefab(Material ambientDustMaterial)
    {
        GameObject root = new GameObject("AmbientDustRegion");
        try
        {
            Transform farDustRegion = CreateGroup(root.transform, "FarDustRegion", 7f);
            ParticleSystem farDust = CreateDustParticleSystem(
                farDustRegion,
                "FarDustParticles",
                ambientDustMaterial,
                -80,
                0f);
            AmbientDustRegion2D farDustController = farDustRegion.gameObject.AddComponent<AmbientDustRegion2D>();

            Transform midDustRegion = CreateGroup(root.transform, "MidDustRegion", 2f);
            ParticleSystem midDust = CreateDustParticleSystem(
                midDustRegion,
                "MidDustParticles",
                ambientDustMaterial,
                -42,
                0f);
            AmbientDustRegion2D midDustController = midDustRegion.gameObject.AddComponent<AmbientDustRegion2D>();

            ConfigureDustRegion(farDustController, farDust, false);
            ConfigureDustRegion(midDustController, midDust, true);

            PrefabUtility.SaveAsPrefabAsset(root, DustPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateDemoScene(Material gameplayMaterial, Material markerMaterial)
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Light2D[] loadedLights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Light2D> temporarilyDisabledGlobalLights = new List<Light2D>();
        for (int i = 0; i < loadedLights.Length; i++)
        {
            Light2D light = loadedLights[i];
            if (light != null && light.enabled && light.lightType == Light2D.LightType.Global)
            {
                light.enabled = false;
                temporarilyDisabledGlobalLights.Add(light);
            }
        }

        Scene demoScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(demoScene);

        try
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.004f, 0.008f, 0.025f, 1f);
            camera.cullingMask = 1 << 31;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<DarkFogDemoCameraPan2D>();

            GameObject lightObject = new GameObject("Global Light 2D");
            Light2D globalLight = lightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;

            GameObject atmospherePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject fogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogPrefabPath);
            GameObject dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DustPrefabPath);
            if (atmospherePrefab == null || fogPrefab == null || dustPrefab == null)
            {
                throw new System.InvalidOperationException("One or more regional atmosphere prefabs could not be loaded.");
            }

            GameObject atmosphere = (GameObject)PrefabUtility.InstantiatePrefab(atmospherePrefab, demoScene);
            GameObject fog = (GameObject)PrefabUtility.InstantiatePrefab(fogPrefab, demoScene);
            GameObject dust = (GameObject)PrefabUtility.InstantiatePrefab(dustPrefab, demoScene);
            atmosphere.transform.position = Vector3.zero;
            fog.transform.position = Vector3.zero;
            dust.transform.position = Vector3.zero;
            SetLayerRecursively(atmosphere, 31);
            SetLayerRecursively(fog, 31);
            SetLayerRecursively(dust, 31);

            GameObject readability = new GameObject("Demo_GameplayReadability");
            CreateQuad(readability.transform, "Platform_Left", new Vector3(-6.5f, -4.8f, 0f), new Vector3(5.5f, 0.55f, 1f), gameplayMaterial, 0);
            CreateQuad(readability.transform, "Platform_Middle", new Vector3(0f, -2.7f, 0f), new Vector3(4.5f, 0.55f, 1f), gameplayMaterial, 0);
            CreateQuad(readability.transform, "Platform_Right", new Vector3(6.4f, -4.1f, 0f), new Vector3(5.2f, 0.55f, 1f), gameplayMaterial, 0);
            CreateQuad(readability.transform, "Player_Marker", new Vector3(0f, -1.85f, -0.1f), new Vector3(0.75f, 1.2f, 1f), markerMaterial, 2);
            SetLayerRecursively(readability, 31);
            lightObject.layer = 31;

            EditorSceneManager.SaveScene(demoScene, DemoScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(demoScene, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }

            for (int i = 0; i < temporarilyDisabledGlobalLights.Count; i++)
            {
                if (temporarilyDisabledGlobalLights[i] != null)
                {
                    temporarilyDisabledGlobalLights[i].enabled = true;
                }
            }
        }
    }

    private static MeshRenderer CreateQuad(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        int sortingOrder)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = localScale;
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    private static Transform CreateGroup(Transform parent, string name, float z)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = new Vector3(0f, 0f, z);
        return group.transform;
    }

    private static Material CreateOrUpdateMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            throw new System.InvalidOperationException("Shader was not found: " + shaderName);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureMaterialDefaults(
        Material background,
        Material staticFog,
        Material driftingFog,
        Material ambientDust,
        Material demoGameplay,
        Material demoMarker)
    {
        background.SetColor("_TopColor", new Color(0.002f, 0.008f, 0.045f, 1f));
        background.SetColor("_MiddleColor", new Color(0.012f, 0.07f, 0.21f, 1f));
        background.SetColor("_BottomColor", new Color(0.02f, 0.35f, 0.56f, 1f));
        background.SetFloat("_Brightness", 1f);
        background.SetFloat("_Contrast", 1.08f);

        ConfigureFogMaterial(staticFog, new Color(0.05f, 0.28f, 0.50f, 1f), 0.18f, 2.5f, 0.44f);
        ConfigureFogMaterial(driftingFog, new Color(0.07f, 0.46f, 0.62f, 1f), 0.20f, 3.8f, 0.64f);
        ambientDust.SetFloat("_Softness", 0.28f);

        demoGameplay.SetColor("_Tint", new Color(0.055f, 0.085f, 0.12f, 1f));
        demoMarker.SetColor("_Tint", new Color(0.30f, 0.90f, 0.92f, 1f));
    }

    private static void ConfigureFogMaterial(Material material, Color color, float alpha, float noiseScale, float noiseStrength)
    {
        material.SetColor("_FogColor", color);
        material.SetFloat("_FogAlpha", alpha);
        material.SetFloat("_VerticalBias", 0.90f);
        material.SetVector("_ScrollSpeed", new Vector4(0.012f, 0.004f, 0f, 0f));
        material.SetFloat("_NoiseScale", noiseScale);
        material.SetFloat("_NoiseStrength", noiseStrength);
        material.SetFloat("_Brightness", 1f);
        material.SetFloat("_Contrast", 1.08f);
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureDustRegion(
        AmbientDustRegion2D controller,
        ParticleSystem particles,
        bool midLayer)
    {
        SerializedObject data = new SerializedObject(controller);
        data.FindProperty("dustParticles").objectReferenceValue = particles;
        data.FindProperty("depth").enumValueIndex = midLayer ? 1 : 0;
        data.FindProperty("baseParticleCount").intValue = midLayer ? 38 : 70;
        data.FindProperty("regionSize").vector2Value = midLayer ? new Vector2(30f, 16f) : new Vector2(34f, 18f);
        data.FindProperty("alpha").floatValue = midLayer ? 0.21f : 0.14f;
        data.FindProperty("sizeMin").floatValue = midLayer ? 0.035f : 0.018f;
        data.FindProperty("sizeMax").floatValue = midLayer ? 0.095f : 0.055f;
        data.FindProperty("lifetimeMin").floatValue = midLayer ? 7.2f : 10f;
        data.FindProperty("lifetimeMax").floatValue = midLayer ? 12.8f : 18f;
        data.FindProperty("driftSpeed").floatValue = midLayer ? 0.035f : 0.018f;
        data.FindProperty("randomSpeedMin").floatValue = midLayer ? 0.015f : 0.005f;
        data.FindProperty("randomSpeedMax").floatValue = midLayer ? 0.075f : 0.035f;
        data.FindProperty("noiseStrength").floatValue = midLayer ? 0.055f : 0.036f;
        data.FindProperty("sortingOrder").intValue = midLayer ? -42 : -80;
        data.ApplyModifiedPropertiesWithoutUndo();
        controller.ApplyRegion();
    }

    private static ParticleSystem CreateDustParticleSystem(
        Transform parent,
        string name,
        Material material,
        int sortingOrder,
        float z)
    {
        GameObject dustObject = new GameObject(name);
        dustObject.transform.SetParent(parent, false);
        dustObject.transform.localPosition = new Vector3(0f, 0f, z);

        ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return particles;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    private static void SetRendererArray(SerializedProperty property, Renderer[] renderers)
    {
        property.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
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
