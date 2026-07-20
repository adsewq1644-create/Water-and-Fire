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

        root.transform.position = new Vector3(35.5f, 103f, 0f);
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

        BatNavigationZone2D coordinator = GetOrAdd<BatNavigationZone2D>(root);
        SerializedObject coordinatorData = new SerializedObject(coordinator);
        coordinatorData.FindProperty("surface").objectReferenceValue = surface;
        coordinatorData.FindProperty("allowSettledObstacleRebuilds").boolValue = false;
        coordinatorData.ApplyModifiedPropertiesWithoutUndo();

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
        flightBounds.transform.position = new Vector3(35.5f, 103f, 0f);
        flightBounds.transform.rotation = Quaternion.identity;
        flightBounds.transform.localScale = Vector3.one;
        BoxCollider2D flightCollider = GetOrAdd<BoxCollider2D>(flightBounds);
        flightCollider.isTrigger = true;
        flightCollider.size = new Vector2(43f, 28f);
        flightCollider.offset = Vector2.zero;
        NavMeshModifier walkable = GetOrAdd<NavMeshModifier>(flightBounds);
        walkable.ignoreFromBuild = false;
        walkable.overrideArea = true;
        walkable.area = 0;

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(surface);
        EditorUtility.SetDirty(flightBounds);
        return surface;
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
                collider.gameObject.layer = movingLayer;
                MovingNavObstacle2D moving = GetOrAdd<MovingNavObstacle2D>(movingRoot.gameObject);
                EditorUtility.SetDirty(moving);
                continue;
            }

            collider.gameObject.layer = staticLayer;
            NavMeshModifier modifier = GetOrAdd<NavMeshModifier>(collider.gameObject);
            modifier.ignoreFromBuild = false;
            modifier.overrideArea = true;
            modifier.area = 1;
            EditorUtility.SetDirty(collider.gameObject);
        }
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
                    Collider2D[] colliders = body.GetComponentsInChildren<Collider2D>(true);
                    bool hasSolid = false;
                    for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    {
                        if (!colliders[colliderIndex].isTrigger)
                        {
                            colliders[colliderIndex].gameObject.layer = LayerMask.NameToLayer(BatMovingLayerName);
                            hasSolid = true;
                        }
                    }

                    if (hasSolid && body.GetComponent<MovingNavObstacle2D>() == null)
                    {
                        body.gameObject.AddComponent<MovingNavObstacle2D>();
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
