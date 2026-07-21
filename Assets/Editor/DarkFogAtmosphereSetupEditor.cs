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
    private const string DemoScenePath = DemoSceneFolder + "/DarkFogAtmosphereDemo.unity";

    [MenuItem("Tools/Water and Fire/Create Dark Fog Atmosphere")]
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
        Material farFogMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogFar.mat",
            "WaterAndFire/DarkFogLayer2D");
        Material midFogMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogMid.mat",
            "WaterAndFire/DarkFogLayer2D");
        Material nearFogMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogNear.mat",
            "WaterAndFire/DarkFogLayer2D");
        Material ambientDustMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/AmbientDust.mat",
            "WaterAndFire/AmbientDust2D");
        Material farSilhouetteMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogFarSilhouette.mat",
            "WaterAndFire/DarkFogSilhouette2D");
        Material midSilhouetteMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogMidSilhouette.mat",
            "WaterAndFire/DarkFogSilhouette2D");
        Material demoGameplayMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogDemoGameplay.mat",
            "WaterAndFire/DarkFogSilhouette2D");
        Material demoMarkerMaterial = CreateOrUpdateMaterial(
            MaterialFolder + "/DarkFogDemoMarker.mat",
            "WaterAndFire/DarkFogSilhouette2D");

        ConfigureMaterialDefaults(
            backgroundMaterial,
            farFogMaterial,
            midFogMaterial,
            nearFogMaterial,
            ambientDustMaterial,
            farSilhouetteMaterial,
            midSilhouetteMaterial,
            demoGameplayMaterial,
            demoMarkerMaterial);

        CreateAtmospherePrefab(
            backgroundMaterial,
            farFogMaterial,
            midFogMaterial,
            nearFogMaterial,
            ambientDustMaterial,
            farSilhouetteMaterial,
            midSilhouetteMaterial);
        CreateDemoScene(demoGameplayMaterial, demoMarkerMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Dark fog atmosphere prefab and isolated demo scene were created.");
    }

    private static void CreateAtmospherePrefab(
        Material backgroundMaterial,
        Material farFogMaterial,
        Material midFogMaterial,
        Material nearFogMaterial,
        Material ambientDustMaterial,
        Material farSilhouetteMaterial,
        Material midSilhouetteMaterial)
    {
        GameObject root = new GameObject("DarkFogAtmosphere");
        try
        {
            DarkFogAtmosphere2D atmosphere = root.AddComponent<DarkFogAtmosphere2D>();
            Transform backgroundRoot = CreateGroup(root.transform, "00_Sky_BackgroundGradient", 0f);
            Transform farRoot = CreateGroup(root.transform, "10_FarLayer", 0f);
            Transform midRoot = CreateGroup(root.transform, "20_MidLayer", 0f);
            Transform nearRoot = CreateGroup(root.transform, "30_NearLayer", 0f);
            Transform dustRoot = CreateGroup(root.transform, "40_AmbientDust", 0f);
            AmbientDust2D ambientDust = dustRoot.gameObject.AddComponent<AmbientDust2D>();

            MeshRenderer background = CreateQuad(
                backgroundRoot,
                "Sky_BackgroundGradient",
                new Vector3(0f, 0f, 8f),
                new Vector3(58f, 32f, 1f),
                backgroundMaterial,
                -100);

            Transform farFogGroup = CreateGroup(farRoot, "FarFogLayer", 4.5f);
            CreateQuad(farFogGroup, "FarFog_Back", new Vector3(0f, -0.8f, 0f), new Vector3(60f, 25f, 1f), farFogMaterial, -94);
            CreateQuad(farFogGroup, "FarFog_Front", new Vector3(2.8f, -2.6f, -0.1f), new Vector3(62f, 19f, 1f), farFogMaterial, -82);
            Transform farSilhouetteGroup = CreateGroup(farRoot, "FarSilhouetteLayer", 4f);
            CreateForestSilhouettes(farSilhouetteGroup, farSilhouetteMaterial, true, -88);

            Transform midFogGroup = CreateGroup(midRoot, "MidFogLayer", 3.2f);
            CreateQuad(midFogGroup, "MidFog_Back", new Vector3(-1.5f, -2.2f, 0f), new Vector3(60f, 20f, 1f), midFogMaterial, -74);
            CreateQuad(midFogGroup, "MidFog_Front", new Vector3(2.2f, -4.0f, -0.1f), new Vector3(62f, 14f, 1f), midFogMaterial, -61);
            Transform midSilhouetteGroup = CreateGroup(midRoot, "MidSilhouetteLayer", 2.8f);
            CreateForestSilhouettes(midSilhouetteGroup, midSilhouetteMaterial, false, -68);

            Transform nearFogGroup = CreateGroup(nearRoot, "NearFogLayer", 1.8f);
            CreateQuad(nearFogGroup, "NearFog_LowerBank", new Vector3(0f, -6.2f, 0f), new Vector3(64f, 10f, 1f), nearFogMaterial, -48);
            CreateQuad(nearFogGroup, "NearFog_ThinWisp", new Vector3(-3f, -3.8f, -0.1f), new Vector3(60f, 7f, 1f), nearFogMaterial, -47);

            ParticleSystem farDust = CreateDustParticleSystem(
                dustRoot,
                "Far_AmbientDust",
                ambientDustMaterial,
                -80,
                7f);
            ParticleSystem midDust = CreateDustParticleSystem(
                dustRoot,
                "Mid_AmbientDust",
                ambientDustMaterial,
                -42,
                2f);

            SerializedObject data = new SerializedObject(atmosphere);
            data.FindProperty("backgroundRoot").objectReferenceValue = backgroundRoot;
            data.FindProperty("farLayerRoot").objectReferenceValue = farRoot;
            data.FindProperty("midLayerRoot").objectReferenceValue = midRoot;
            data.FindProperty("nearLayerRoot").objectReferenceValue = nearRoot;
            data.FindProperty("backgroundRenderer").objectReferenceValue = background;
            SetRendererArray(data.FindProperty("farFogRenderers"), farFogGroup.GetComponentsInChildren<Renderer>(true));
            SetRendererArray(data.FindProperty("midFogRenderers"), midFogGroup.GetComponentsInChildren<Renderer>(true));
            SetRendererArray(data.FindProperty("nearFogRenderers"), nearFogGroup.GetComponentsInChildren<Renderer>(true));
            SetRendererArray(data.FindProperty("farSilhouetteRenderers"), farSilhouetteGroup.GetComponentsInChildren<Renderer>(true));
            SetRendererArray(data.FindProperty("midSilhouetteRenderers"), midSilhouetteGroup.GetComponentsInChildren<Renderer>(true));
            data.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.ApplyAtmosphere();

            SerializedObject dustData = new SerializedObject(ambientDust);
            dustData.FindProperty("farDust").objectReferenceValue = farDust;
            dustData.FindProperty("midDust").objectReferenceValue = midDust;
            dustData.ApplyModifiedPropertiesWithoutUndo();
            ambientDust.ApplySettings();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
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

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("DarkFogAtmosphere prefab could not be loaded.");
            }

            GameObject atmosphere = (GameObject)PrefabUtility.InstantiatePrefab(prefab, demoScene);
            atmosphere.transform.position = Vector3.zero;
            SetLayerRecursively(atmosphere, 31);

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

    private static void CreateForestSilhouettes(Transform parent, Material material, bool farLayer, int sortingOrder)
    {
        float[] xPositions = farLayer
            ? new[] { -25f, -19f, -13.5f, -7f, -1f, 5.5f, 12f, 18.5f, 24f }
            : new[] { -24f, -15.5f, -8f, 1.5f, 10f, 17f, 24.5f };

        for (int i = 0; i < xPositions.Length; i++)
        {
            float height = farLayer ? 22f + i % 3 * 1.6f : 24f + i % 2 * 2f;
            float width = farLayer ? 1.15f + i % 2 * 0.25f : 1.8f + i % 3 * 0.35f;
            float bend = ((i % 4) - 1.5f) * (farLayer ? 0.8f : 1.2f);
            Transform tree = CreateGroup(parent, (farLayer ? "FarTree_" : "MidTree_") + (i + 1).ToString("00"), 0f);
            tree.localPosition = new Vector3(xPositions[i], 0f, 0f);

            CreateTrunk(tree, "Trunk", height, width, bend, material, sortingOrder);
            if (i % 2 == 1)
            {
                CreateBranch(tree, "Branch_Left", new Vector3(bend * 0.3f, -1f + height * 0.17f, 0f), new Vector3(-3.8f, 4.4f, 0f), width * 0.45f, material, sortingOrder);
            }
            if (i % 3 != 0)
            {
                CreateBranch(tree, "Branch_Right", new Vector3(bend * 0.55f, 1f + height * 0.24f, 0f), new Vector3(3.3f, 4.8f, 0f), width * 0.38f, material, sortingOrder);
            }
        }

        CreateQuad(parent, farLayer ? "FarGroundMass" : "MidGroundMass", new Vector3(0f, -10.5f, 0f), new Vector3(62f, farLayer ? 3.8f : 4.8f, 1f), material, sortingOrder);
    }

    private static void CreateTrunk(
        Transform parent,
        string name,
        float height,
        float width,
        float bend,
        Material material,
        int sortingOrder)
    {
        LineRenderer line = CreateLine(parent, name, material, sortingOrder, width, width * 0.48f);
        line.positionCount = 5;
        line.SetPositions(new[]
        {
            new Vector3(0f, -10f, 0f),
            new Vector3(bend * 0.12f, -10f + height * 0.28f, 0f),
            new Vector3(bend * 0.42f, -10f + height * 0.55f, 0f),
            new Vector3(bend * 0.72f, -10f + height * 0.80f, 0f),
            new Vector3(bend, -10f + height, 0f)
        });
    }

    private static void CreateBranch(
        Transform parent,
        string name,
        Vector3 start,
        Vector3 direction,
        float width,
        Material material,
        int sortingOrder)
    {
        LineRenderer line = CreateLine(parent, name, material, sortingOrder, width, width * 0.35f);
        line.positionCount = 3;
        line.SetPositions(new[]
        {
            start,
            start + direction * 0.55f + Vector3.up * 0.45f,
            start + direction
        });
    }

    private static LineRenderer CreateLine(
        Transform parent,
        string name,
        Material material,
        int sortingOrder,
        float startWidth,
        float endWidth)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 3;
        line.numCornerVertices = 3;
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.sortingOrder = sortingOrder;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
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
        Material farFog,
        Material midFog,
        Material nearFog,
        Material ambientDust,
        Material farSilhouette,
        Material midSilhouette,
        Material demoGameplay,
        Material demoMarker)
    {
        background.SetColor("_TopColor", new Color(0.002f, 0.008f, 0.045f, 1f));
        background.SetColor("_MiddleColor", new Color(0.012f, 0.07f, 0.21f, 1f));
        background.SetColor("_BottomColor", new Color(0.02f, 0.35f, 0.56f, 1f));
        background.SetFloat("_Brightness", 1f);
        background.SetFloat("_Contrast", 1.08f);

        ConfigureFogMaterial(farFog, new Color(0.06f, 0.22f, 0.45f, 1f), 0.22f, 2.4f, 0.42f);
        ConfigureFogMaterial(midFog, new Color(0.06f, 0.38f, 0.58f, 1f), 0.22f, 3.2f, 0.56f);
        ConfigureFogMaterial(nearFog, new Color(0.08f, 0.52f, 0.65f, 1f), 0.26f, 4.1f, 0.68f);
        ambientDust.SetFloat("_Softness", 0.28f);

        farSilhouette.SetColor("_Tint", new Color(0.018f, 0.075f, 0.16f, 0.50f));
        midSilhouette.SetColor("_Tint", new Color(0.001f, 0.004f, 0.022f, 0.96f));
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
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

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
