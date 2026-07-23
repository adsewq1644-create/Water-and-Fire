#if UNITY_EDITOR
using System.Collections.Generic;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class BatNavigationSetupEditor
{
    public const string BatStaticLayerName = "BatStaticObstacle";
    public const string BatMovingLayerName = "BatMovingObstacle";
    public const string DarkBatLayerName = "DarkBat";
    public const string AgentName = "BatFlyingAgent";
    public const int AgentTypeId = 183602731;

    private const string DarkBatPrefabPath = "Assets/Prefabs/Darkness/DarkBat.prefab";
    private const string BatPlacementPrefabFolder = "Assets/Prefabs/Darkness/BatNavigation";
    private const string VisionLimitedPrefabFolder = "Assets/Prefabs/Darkness/VisionLimited";
    private const string GroundPrefabPath = "Assets/Prefabs/Ground/Ground.prefab";
    private const string WallPrefabPath = "Assets/Prefabs/Ground/Wall.prefab";
    private const string SlipperySlopePrefabPath = "Assets/Prefabs/Ground/SlipperySlope.prefab";
    private const string MovingPlatformPrefabPath = "Assets/Prefabs/Platforms/MovingPlatform_Right.prefab";
    private const string NavigationRootName = "95_BatNavigation";
    private const string FlightBoundsName = "BatFlightBounds";

    [MenuItem("Tools/Water and Fire/Setup Zone 02 Bat Navigation")]
    public static void SetupZone02()
    {
        EnsureLayer(BatStaticLayerName);
        EnsureLayer(BatMovingLayerName);
        EnsureLayer(DarkBatLayerName);
        EnsureBatAgentType();
        ConfigureDarkBatPrefab();
        ConfigureReusableMovingObstaclePrefabs();
        CreateBatNavigationPlacementPrefabs();

        GameObject zone = GameObject.Find("Zone_02");
        if (zone == null)
        {
            Debug.LogError("Zone_02 was not found in the active scene.");
            return;
        }

        int staticLayer = LayerMask.NameToLayer(BatStaticLayerName);
        int movingLayer = LayerMask.NameToLayer(BatMovingLayerName);
        int darkBatLayer = LayerMask.NameToLayer(DarkBatLayerName);
        ConfigureZoneStaticSources(zone, staticLayer, movingLayer);
        NavMeshSurface surface = ConfigureSurface(zone, staticLayer);
        ConfigureSceneBats(zone, staticLayer, movingLayer, darkBatLayer);

        Physics2D.SyncTransforms();
        surface.RemoveData();
        surface.BuildNavMesh();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Zone_02 BatFlyingAgent NavMeshPlus setup and bake completed.", surface);
    }

    private static NavMeshSurface ConfigureSurface(GameObject zone, int staticLayer)
    {
        Transform rootTransform = zone.transform.Find(NavigationRootName);
        GameObject root;
        if (rootTransform == null)
        {
            root = new GameObject(NavigationRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create bat navigation root");
            root.transform.SetParent(zone.transform, false);
        }
        else
        {
            root = rootTransform.gameObject;
        }

        if (rootTransform == null)
        {
            root.transform.position = new Vector3(35.5f, 103f, 0f);
        }
        root.transform.rotation = Quaternion.Euler(270f, 0f, 0f);
        root.transform.localScale = Vector3.one;

        NavMeshSurface surface = GetOrAdd<NavMeshSurface>(root);
        surface.agentTypeID = AgentTypeId;
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << staticLayer;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.hideEditorLogs = true;
        GetOrAdd<CollectSources2d>(root);

        Transform boundsTransform = root.transform.Find(FlightBoundsName);
        GameObject flightBounds;
        if (boundsTransform == null)
        {
            flightBounds = new GameObject(FlightBoundsName);
            flightBounds.transform.SetParent(root.transform, false);
        }
        else
        {
            flightBounds = boundsTransform.gameObject;
        }

        flightBounds.layer = staticLayer;
        BoxCollider2D flightCollider = GetOrAdd<BoxCollider2D>(flightBounds);
        if (boundsTransform == null)
        {
            flightBounds.transform.position = new Vector3(35.5f, 103f, 0f);
            flightBounds.transform.rotation = Quaternion.identity;
            flightBounds.transform.localScale = Vector3.one;
            flightCollider.size = new Vector2(43f, 28f);
            flightCollider.offset = Vector2.zero;
        }
        flightCollider.isTrigger = true;
        NavMeshModifier walkable = GetOrAdd<NavMeshModifier>(flightBounds);
        walkable.ignoreFromBuild = false;
        walkable.overrideArea = true;
        walkable.area = 0;

        BatNavigationZone2D coordinator = GetOrAdd<BatNavigationZone2D>(root);
        SerializedObject coordinatorData = new SerializedObject(coordinator);
        coordinatorData.FindProperty("surface").objectReferenceValue = surface;
        coordinatorData.FindProperty("flightBounds").objectReferenceValue = flightCollider;
        coordinatorData.FindProperty("zoneCenter").vector2Value = flightBounds.transform.position;
        coordinatorData.FindProperty("zoneSize").vector2Value = flightCollider.size;
        coordinatorData.FindProperty("allowSettledObstacleRebuilds").boolValue = true;
        coordinatorData.ApplyModifiedPropertiesWithoutUndo();
        coordinator.ApplyZoneBounds();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(surface);
        EditorUtility.SetDirty(flightBounds);
        return surface;
    }

    [MenuItem("Tools/Water and Fire/Create Zone 02 Bat Placement Prefabs")]
    public static void CreateBatNavigationPlacementPrefabs()
    {
        EnsureFolder("Assets/Prefabs/Darkness", "BatNavigation");
        EnsureFolder("Assets/Prefabs/Darkness", "VisionLimited");
        CreateConfiguredPlacementPrefab(GroundPrefabPath, BatPlacementPrefabFolder + "/BatStaticGround.prefab", false);
        CreateConfiguredPlacementPrefab(WallPrefabPath, BatPlacementPrefabFolder + "/BatStaticWall.prefab", false);
        CreateConfiguredPlacementPrefab(SlipperySlopePrefabPath, BatPlacementPrefabFolder + "/BatSlipperySlope.prefab", false);
        CreateConfiguredPlacementPrefab(MovingPlatformPrefabPath, BatPlacementPrefabFolder + "/BatMovingPlatform.prefab", true);
        CreateShockwaveHiddenPlacementPrefab(
            BatPlacementPrefabFolder + "/BatStaticGround.prefab",
            BatPlacementPrefabFolder + "/BatHiddenStaticGround.prefab");
        CreateShockwaveHiddenPlacementPrefab(
            BatPlacementPrefabFolder + "/BatStaticWall.prefab",
            BatPlacementPrefabFolder + "/BatHiddenStaticWall.prefab");
        CreateShockwaveHiddenPlacementPrefab(
            BatPlacementPrefabFolder + "/BatSlipperySlope.prefab",
            BatPlacementPrefabFolder + "/BatHiddenSlipperySlope.prefab");
        CreateShockwaveHiddenPlacementPrefab(
            BatPlacementPrefabFolder + "/BatMovingPlatform.prefab",
            BatPlacementPrefabFolder + "/BatHiddenMovingPlatform.prefab");
        ConfigureVisionLimitedPlacementPrefab(BatPlacementPrefabFolder + "/BatStaticGround.prefab");
        ConfigureVisionLimitedPlacementPrefab(BatPlacementPrefabFolder + "/BatStaticWall.prefab");
        ConfigureVisionLimitedPlacementPrefab(BatPlacementPrefabFolder + "/BatSlipperySlope.prefab");
        ConfigureVisionLimitedPlacementPrefab(BatPlacementPrefabFolder + "/BatMovingPlatform.prefab");
        CreateVisionLimitedGroundPrefab();
        CreateVisionLimitedMidgroundPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateConfiguredPlacementPrefab(string sourcePath, string destinationPath, bool moving)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                Debug.LogError("Could not create bat navigation prefab from " + sourcePath);
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(destinationPath);
        try
        {
            int targetLayer = LayerMask.NameToLayer(moving ? BatMovingLayerName : BatStaticLayerName);
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider.isTrigger)
                {
                    continue;
                }

                collider.gameObject.layer = targetLayer;
                if (!moving)
                {
                    NavMeshModifier modifier = GetOrAdd<NavMeshModifier>(collider.gameObject);
                    modifier.ignoreFromBuild = false;
                    modifier.overrideArea = true;
                    modifier.area = 1;
                }
            }

            if (moving)
            {
                Transform movingRoot = FindMovingRoot(root.transform, null);
                MovingNavObstacle2D obstacle = GetOrAdd<MovingNavObstacle2D>(
                    (movingRoot != null ? movingRoot : root.transform).gameObject);
                ConfigureMovingObstacle(obstacle, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateShockwaveHiddenPlacementPrefab(string sourcePath, string destinationPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                Debug.LogError("Could not create shockwave-hidden bat navigation prefab from " + sourcePath);
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(destinationPath);
        try
        {
            Transform visualRoot = root.transform.Find("ShockwaveVisualRoot");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("ShockwaveVisualRoot").transform;
                visualRoot.SetParent(root.transform, false);
            }

            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
            SpriteRenderer visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                if (visualRenderer == null)
                {
                    visualRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
                }

                EditorUtility.CopySerialized(rootRenderer, visualRenderer);
                Object.DestroyImmediate(rootRenderer);
            }

            SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            ShockwaveHiddenPlatform2D hiddenPlatform = GetOrAdd<ShockwaveHiddenPlatform2D>(root);
            SerializedObject hiddenData = new SerializedObject(hiddenPlatform);
            SerializedProperty rendererData = hiddenData.FindProperty("silhouetteRenderers");
            rendererData.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererData.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }

            hiddenData.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            hiddenData.FindProperty("hiddenAlpha").floatValue = 0f;
            hiddenData.FindProperty("revealedAlpha").floatValue = 0.72f;
            hiddenData.FindProperty("startHidden").boolValue = true;
            if (renderers.Length > 0)
            {
                Color revealColor = renderers[0].color;
                revealColor.a = 1f;
                hiddenData.FindProperty("silhouetteColor").colorValue = revealColor;
            }

            hiddenData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateVisionLimitedGroundPrefab()
    {
        string sourcePath = BatPlacementPrefabFolder + "/BatStaticGround.prefab";
        string destinationPath = VisionLimitedPrefabFolder + "/VisionLimitedGround.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                Debug.LogError("Could not create vision-limited ground prefab.");
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(destinationPath);
        try
        {
            ConfigureVisionComponent(root);
            PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureVisionLimitedPlacementPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ConfigureVisionComponent(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateVisionLimitedMidgroundPrefab()
    {
        string destinationPath = VisionLimitedPrefabFolder + "/VisionLimitedMidground.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) == null)
        {
            if (!AssetDatabase.CopyAsset(GroundPrefabPath, destinationPath))
            {
                Debug.LogError("Could not create vision-limited midground prefab.");
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(destinationPath);
        try
        {
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(colliders[i]);
            }

            Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = bodies.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(bodies[i]);
            }

            NavMeshModifier[] modifiers = root.GetComponentsInChildren<NavMeshModifier>(true);
            for (int i = modifiers.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(modifiers[i]);
            }

            MovingNavObstacle2D[] movingObstacles = root.GetComponentsInChildren<MovingNavObstacle2D>(true);
            for (int i = movingObstacles.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(movingObstacles[i]);
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = 0;
            }

            root.tag = "Untagged";
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = -10;
                renderers[i].color = new Color(0.24f, 0.13f, 0.42f, 1f);
            }

            ConfigureVisionComponent(root);
            PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureVisionComponent(GameObject root)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        CharacterVisionVisible2D vision = GetOrAdd<CharacterVisionVisible2D>(root);
        SerializedObject visionData = new SerializedObject(vision);
        SerializedProperty rendererData = visionData.FindProperty("renderers");
        rendererData.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
        {
            rendererData.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        visionData.FindProperty("restrictOnlyInDarkZone").boolValue = true;
        visionData.FindProperty("hiddenAlpha").floatValue = 0f;
        visionData.FindProperty("visibleAlpha").floatValue = 1f;
        visionData.FindProperty("visionSoftness").floatValue = 0.8f;
        visionData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void ConfigureZoneStaticSources(GameObject zone, int staticLayer, int movingLayer)
    {
        Collider2D[] colliders = zone.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<DarkBat2D>() != null)
            {
                continue;
            }

            Transform movingRoot = FindMovingRoot(collider.transform, zone.transform);
            if (movingRoot != null)
            {
                bool stateBased = IsStateBasedMovingRoot(movingRoot);
                collider.gameObject.layer = stateBased ? staticLayer : movingLayer;
                if (stateBased)
                {
                    ConfigureStaticNavMeshModifier(collider);
                }
                MovingNavObstacle2D moving = GetOrAdd<MovingNavObstacle2D>(movingRoot.gameObject);
                ConfigureMovingObstacle(moving, stateBased);
                EditorUtility.SetDirty(moving);
                continue;
            }

            collider.gameObject.layer = staticLayer;
            ConfigureStaticNavMeshModifier(collider);
            EditorUtility.SetDirty(collider.gameObject);
        }
    }

    private static bool IsStateBasedMovingRoot(Transform movingRoot)
    {
        return movingRoot != null && movingRoot.GetComponent<VineReleaseBridge2D>() != null;
    }

    private static Transform FindMovingRoot(Transform source, Transform zoneRoot)
    {
        Transform current = source;
        while (current != null && current != zoneRoot)
        {
            if (current.GetComponent<MovingPlatform2D>() != null ||
                current.GetComponent<VineReleaseBridge2D>() != null ||
                current.GetComponent<WaterBalancePlatform>() != null ||
                current.GetComponent<Rigidbody2D>() != null &&
                current.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static void ConfigureSceneBats(GameObject zone, int staticLayer, int movingLayer, int darkBatLayer)
    {
        DarkBat2D[] bats = zone.GetComponentsInChildren<DarkBat2D>(true);
        for (int i = 0; i < bats.Length; i++)
        {
            DarkBat2D bat = bats[i];
            bat.gameObject.layer = darkBatLayer;
            NavMeshAgent agent = GetOrAdd<NavMeshAgent>(bat.gameObject);
            ConfigureAgent(agent);
            BatNavMeshMotor2D motor = GetOrAdd<BatNavMeshMotor2D>(bat.gameObject);
            ConfigureMotor(motor, staticLayer, movingLayer);
            EditorUtility.SetDirty(bat.gameObject);
        }
    }

    private static void ConfigureDarkBatPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DarkBatPrefabPath);
        try
        {
            int staticLayer = LayerMask.NameToLayer(BatStaticLayerName);
            int movingLayer = LayerMask.NameToLayer(BatMovingLayerName);
            int darkBatLayer = LayerMask.NameToLayer(DarkBatLayerName);
            root.layer = darkBatLayer;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            NavMeshAgent agent = GetOrAdd<NavMeshAgent>(root);
            ConfigureAgent(agent);
            BatNavMeshMotor2D motor = GetOrAdd<BatNavMeshMotor2D>(root);
            ConfigureMotor(motor, staticLayer, movingLayer);

            Transform visualRoot = root.transform.Find("VisualRoot");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(root.transform, false);
            }

            Transform interaction = visualRoot.Find("InteractionRoot");
            if (interaction == null)
            {
                interaction = new GameObject("InteractionRoot").transform;
                interaction.SetParent(visualRoot, false);
            }

            interaction.localPosition = Vector3.zero;
            interaction.localRotation = Quaternion.identity;
            Vector3 visualScale = visualRoot.localScale;
            interaction.localScale = new Vector3(
                Mathf.Abs(visualScale.x) > 0.001f ? 1f / visualScale.x : 1f,
                Mathf.Abs(visualScale.y) > 0.001f ? 1f / visualScale.y : 1f,
                1f);
            interaction.gameObject.layer = darkBatLayer;

            CircleCollider2D rootCollider = root.GetComponent<CircleCollider2D>();
            CircleCollider2D hitCollider = GetOrAdd<CircleCollider2D>(interaction.gameObject);
            if (rootCollider != null)
            {
                hitCollider.radius = rootCollider.radius;
                hitCollider.offset = rootCollider.offset;
                Object.DestroyImmediate(rootCollider);
            }
            else if (hitCollider.radius <= 0f)
            {
                hitCollider.radius = 0.42f;
            }
            hitCollider.isTrigger = true;

            DarkBat2D bat = GetOrAdd<DarkBat2D>(root);
            SerializedObject batData = new SerializedObject(bat);
            batData.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            batData.FindProperty("hitCollider").objectReferenceValue = hitCollider;
            batData.FindProperty("body").objectReferenceValue = body;
            batData.FindProperty("motor").objectReferenceValue = motor;
            batData.FindProperty("renderers").arraySize = visualRoot.GetComponentsInChildren<SpriteRenderer>(true).Length;
            SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                batData.FindProperty("renderers").GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }
            batData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DarkBatPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureReusableMovingObstaclePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null ||
                prefab.GetComponentInChildren<MovingPlatform2D>(true) == null &&
                prefab.GetComponentInChildren<VineReleaseBridge2D>(true) == null &&
                prefab.GetComponentInChildren<WaterBalancePlatform>(true) == null)
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
                for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
                {
                    Rigidbody2D body = bodies[bodyIndex];
                    bool stateBased = body.GetComponentInParent<VineReleaseBridge2D>() != null;
                    Collider2D[] colliders = body.GetComponentsInChildren<Collider2D>(true);
                    bool hasSolid = false;
                    for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    {
                        Collider2D collider = colliders[colliderIndex];
                        if (!collider.isTrigger)
                        {
                            collider.gameObject.layer = LayerMask.NameToLayer(
                                stateBased ? BatStaticLayerName : BatMovingLayerName);
                            if (stateBased)
                            {
                                ConfigureStaticNavMeshModifier(collider);
                            }
                            hasSolid = true;
                        }
                    }

                    if (hasSolid)
                    {
                        MovingNavObstacle2D obstacle = GetOrAdd<MovingNavObstacle2D>(body.gameObject);
                        ConfigureMovingObstacle(obstacle, stateBased);
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ConfigureMovingObstacle(MovingNavObstacle2D obstacle, bool stateBased)
    {
        SerializedObject data = new SerializedObject(obstacle);
        data.FindProperty("mode").enumValueIndex = stateBased
            ? (int)MovingNavObstacle2D.ObstacleMode.StateBased
            : (int)MovingNavObstacle2D.ObstacleMode.Continuous;
        data.FindProperty("updateNavMeshWhenSettled").boolValue = stateBased;
        data.FindProperty("movingObstacleLayer").stringValue = BatMovingLayerName;
        data.FindProperty("settledStaticLayer").stringValue = BatStaticLayerName;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(obstacle);
    }

    private static void ConfigureStaticNavMeshModifier(Collider2D collider)
    {
        NavMeshModifier modifier = GetOrAdd<NavMeshModifier>(collider.gameObject);
        modifier.ignoreFromBuild = false;
        modifier.overrideArea = true;
        modifier.area = 1;
        EditorUtility.SetDirty(modifier);
    }

    private static void ConfigureAgent(NavMeshAgent agent)
    {
        agent.agentTypeID = AgentTypeId;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.autoBraking = false;
        agent.autoRepath = false;
        agent.radius = 0.24f;
        agent.height = 0.5f;
        agent.speed = 8f;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        agent.avoidancePriority = 40 + Mathf.Abs(agent.GetInstanceID() % 40);
    }

    private static void ConfigureMotor(BatNavMeshMotor2D motor, int staticLayer, int movingLayer)
    {
        SerializedObject data = new SerializedObject(motor);
        data.FindProperty("staticObstacleMask").intValue = 1 << staticLayer;
        data.FindProperty("movingObstacleMask").intValue = 1 << movingLayer;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(motor);
    }

    private static void EnsureBatAgentType()
    {
        const string settingsPath = "ProjectSettings/NavMeshAreas.asset";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
        if (assets == null || assets.Length == 0)
        {
            throw new System.InvalidOperationException("NavMeshAreas.asset could not be loaded.");
        }

        SerializedObject settings = new SerializedObject(assets[0]);
        SerializedProperty agents = settings.FindProperty("m_Settings");
        SerializedProperty names = settings.FindProperty("m_SettingNames");
        for (int i = 0; i < agents.arraySize; i++)
        {
            if (agents.GetArrayElementAtIndex(i).FindPropertyRelative("agentTypeID").intValue == AgentTypeId)
            {
                names.GetArrayElementAtIndex(i).stringValue = AgentName;
                ConfigureAgentSettings(agents.GetArrayElementAtIndex(i));
                settings.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        int index = agents.arraySize;
        agents.InsertArrayElementAtIndex(index);
        names.InsertArrayElementAtIndex(index);
        names.GetArrayElementAtIndex(index).stringValue = AgentName;
        SerializedProperty added = agents.GetArrayElementAtIndex(index);
        SetInt(added, "agentTypeID", AgentTypeId);
        ConfigureAgentSettings(added);
        SerializedProperty lastAgentTypeId = settings.FindProperty("m_LastAgentTypeID");
        if (lastAgentTypeId != null)
        {
            lastAgentTypeId.intValue = AgentTypeId;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAgentSettings(SerializedProperty agent)
    {
        SetFloat(agent, "agentRadius", 0.24f);
        SetFloat(agent, "agentHeight", 0.5f);
        SetFloat(agent, "agentSlope", 89f);
        SetFloat(agent, "agentClimb", 0.1f);
        SetFloat(agent, "ledgeDropHeight", 0f);
        SetFloat(agent, "maxJumpAcrossDistance", 0f);
        SetFloat(agent, "minRegionArea", 0.05f);
        SetInt(agent, "manualCellSize", 1);
        SetFloat(agent, "cellSize", 0.08f);
        SetInt(agent, "manualTileSize", 0);
        SetInt(agent, "tileSize", 256);
        SetInt(agent, "accuratePlacement", 1);
    }

    private static void SetFloat(SerializedProperty parent, string relativeName, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInt(SerializedProperty parent, string relativeName, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void EnsureLayer(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) >= 0)
        {
            return;
        }

        SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tags.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tags.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        throw new System.InvalidOperationException("No empty Unity layer slot is available for " + layerName + ".");
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
#endif
