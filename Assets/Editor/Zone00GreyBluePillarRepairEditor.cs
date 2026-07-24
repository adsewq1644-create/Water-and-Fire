using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

internal static class Zone00GreyBluePillarRepairEditor
{
    private const string GreyBluePillar1Guid = "4edc5352c5acd61469a7fc9391de45c8";
    private const string GreyBluePillar2Guid = "cfb9e6d244778324abd74055e66de9ac";
    private const string DarkGreyBluePillar1Guid = "be52b3bb8cdd544409a52c2450d0f893";
    private const string Tree5Guid = "171561b95691d204abff13a3c3b1e83a";
    private const string DarkGreyBlueArch1Guid = "5cd1b674a9f076c4c8e48700d1e74370";
    private const string GroundGrassStrip1Guid = "469a3d20e304c9a4891212a29c178afc";
    private const string ThinPlatformGuid = "92f427b69cc9a4d4b80d545dcbefaf46";
    private const string GroundArtSessionKey = "Zone00GroundArt.Ground03To13.v7";
    private const string GroundArtVisualName = "Visual";
    private const float Pillar10HeightRatio = 1547f / 768f;

    [InitializeOnLoadMethod]
    private static void ScheduleGroundArtOnce()
    {
        if (SessionState.GetBool(GroundArtSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(GroundArtSessionKey, true);
        EditorApplication.delayCall += ApplyThinPlatformArtToZone00Grounds;
    }

    [MenuItem("Tools/Water and Fire/Repair Renamed Zone 00 Art")]
    private static void RepairLoadedScenes()
    {
        Sprite greyBluePillar1 = LoadSpriteByGuid(GreyBluePillar1Guid);
        Sprite greyBluePillar2 = LoadSpriteByGuid(GreyBluePillar2Guid);
        Sprite darkGreyBluePillar1 = LoadSpriteByGuid(DarkGreyBluePillar1Guid);
        Sprite tree5 = LoadSpriteByGuid(Tree5Guid);
        Sprite darkGreyBlueArch1 = LoadSpriteByGuid(DarkGreyBlueArch1Guid);
        Sprite groundGrassStrip1 = LoadSpriteByGuid(GroundGrassStrip1Guid);
        if (greyBluePillar1 == null ||
            greyBluePillar2 == null ||
            darkGreyBluePillar1 == null ||
            tree5 == null ||
            darkGreyBlueArch1 == null ||
            groundGrassStrip1 == null)
        {
            return;
        }

        int repairedCount = 0;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Sprite replacement = GetReplacement(
                        renderer,
                        greyBluePillar1,
                        greyBluePillar2,
                        darkGreyBluePillar1,
                        tree5,
                        darkGreyBlueArch1,
                        groundGrassStrip1,
                        out bool preservePillar10Height);
                    if (replacement == null)
                    {
                        continue;
                    }

                    Undo.RecordObject(renderer, "Reconnect Renamed Zone 00 Sprite");
                    renderer.sprite = replacement;
                    renderer.enabled = true;
                    EditorUtility.SetDirty(renderer);
                    SceneVisibilityManager.instance.Show(renderer.gameObject, true);

                    if (preservePillar10Height && renderer.transform.localScale.y < 1.5f)
                    {
                        Undo.RecordObject(renderer.transform, "Preserve Pillar World Height");
                        Vector3 scale = renderer.transform.localScale;
                        scale.y *= Pillar10HeightRatio;
                        renderer.transform.localScale = scale;
                        EditorUtility.SetDirty(renderer.transform);
                    }

                    repairedCount++;
                    sceneChanged = true;
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        SceneView.RepaintAll();
        Debug.Log($"[Zone00 Art] Reconnected {repairedCount} renamed Zone 00 renderer(s).");
    }

    private static Sprite LoadSpriteByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"[Zone00 Art] Could not resolve an asset path for GUID '{guid}'.");
            return null;
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"[Zone00 Art] Could not load the current Single Sprite at '{path}'.");
        }

        return sprite;
    }

    [MenuItem("Tools/Water and Fire/Apply Zone 00 Ground Art (3-13)")]
    private static void ApplyThinPlatformArtToZone00Grounds()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Zone00 Art] Ground art was not applied during Play Mode.");
            return;
        }

        Sprite thinPlatform = LoadSpriteByGuid(ThinPlatformGuid);
        if (thinPlatform == null)
        {
            return;
        }

        Scene targetScene = default;
        Transform zoneRoot = null;
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Zone_00")
                {
                    targetScene = scene;
                    zoneRoot = root.transform;
                    break;
                }
            }

            if (zoneRoot != null)
            {
                break;
            }
        }

        Transform groundRoot = zoneRoot != null ? zoneRoot.Find("Ground") : null;
        if (groundRoot == null)
        {
            Debug.LogError("[Zone00 Art] Could not find Zone_00/Ground in a loaded scene.");
            return;
        }

        List<GroundArtTarget> targets = new List<GroundArtTarget>();
        foreach (Transform child in groundRoot)
        {
            if (!TryGetGroundIndex(child.name, out int index) || index < 3 || index > 13)
            {
                continue;
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            BoxCollider2D collider = child.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                Debug.LogError($"[Zone00 Art] '{child.name}' needs a BoxCollider2D.");
                return;
            }

            targets.Add(new GroundArtTarget(child, renderer, collider));
        }

        if (targets.Count != 11)
        {
            Debug.LogError($"[Zone00 Art] Expected Ground (3) through Ground (13), but found {targets.Count} target(s).");
            return;
        }

        targets.Sort((left, right) => left.Index.CompareTo(right.Index));
        int changedCount = 0;
        foreach (GroundArtTarget target in targets)
        {
            bool visualChanged = target.ApplyVisual(thinPlatform);

            if (!target.HasUnchangedGameplayShape())
            {
                Debug.LogError($"[Zone00 Art] Gameplay shape changed unexpectedly on '{target.Transform.name}'.");
                return;
            }

            if (visualChanged)
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
        }

        SceneView.RepaintAll();
        Debug.Log(
            $"[Zone00 Art] Applied thin platform art to 11 grounds; changed {changedCount}. " +
            $"Visuals and BoxCollider2D components are now separated for independent editing.");
    }

    private static bool TryGetGroundIndex(string objectName, out int index)
    {
        const string prefix = "Ground (";
        index = -1;
        if (!objectName.StartsWith(prefix) || !objectName.EndsWith(")"))
        {
            return false;
        }

        string number = objectName.Substring(prefix.Length, objectName.Length - prefix.Length - 1);
        return int.TryParse(number, out index);
    }

    private sealed class GroundArtTarget
    {
        public readonly Transform Transform;
        public readonly SpriteRenderer Renderer;
        public readonly int Index;

        private readonly BoxCollider2D collider;
        private readonly Vector3 localPosition;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;
        private readonly Vector2 colliderSize;
        private readonly Vector2 colliderOffset;
        private readonly float colliderEdgeRadius;
        private readonly bool colliderEnabled;
        private readonly bool colliderIsTrigger;
        private readonly Material visualMaterial;
        private readonly int visualSortingLayerId;
        private readonly int visualSortingOrder;

        public GroundArtTarget(Transform transform, SpriteRenderer renderer, BoxCollider2D boxCollider)
        {
            Transform = transform;
            Renderer = renderer;
            collider = boxCollider;
            TryGetGroundIndex(transform.name, out Index);
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            Vector3 currentScale = transform.localScale;
            bool recoverNormalizedScale =
                Mathf.Approximately(currentScale.x, 1f) &&
                Mathf.Approximately(currentScale.y, 1f) &&
                boxCollider.size != Vector2.one &&
                transform.Find(GroundArtVisualName) != null;

            if (recoverNormalizedScale)
            {
                float restoredScaleX = Mathf.Max(Mathf.Abs(boxCollider.size.x), Mathf.Epsilon);
                float restoredScaleY = Mathf.Max(Mathf.Abs(boxCollider.size.y), Mathf.Epsilon);
                localScale = new Vector3(restoredScaleX, restoredScaleY, currentScale.z);
                colliderSize = Vector2.one;
                colliderOffset = new Vector2(
                    boxCollider.offset.x / restoredScaleX,
                    boxCollider.offset.y / restoredScaleY);
                colliderEdgeRadius =
                    boxCollider.edgeRadius / Mathf.Min(restoredScaleX, restoredScaleY);
            }
            else
            {
                localScale = currentScale;
                colliderSize = boxCollider.size;
                colliderOffset = boxCollider.offset;
                colliderEdgeRadius = boxCollider.edgeRadius;
            }
            colliderEnabled = boxCollider.enabled;
            colliderIsTrigger = boxCollider.isTrigger;

            Transform existingVisualTransform = transform.Find(GroundArtVisualName);
            SpriteRenderer existingVisual = existingVisualTransform != null
                ? existingVisualTransform.GetComponent<SpriteRenderer>()
                : null;
            SpriteRenderer styleSource = renderer != null ? renderer : existingVisual;
            visualMaterial = styleSource != null ? styleSource.sharedMaterial : null;
            visualSortingLayerId = styleSource != null ? styleSource.sortingLayerID : 0;
            visualSortingOrder = styleSource != null ? styleSource.sortingOrder : 0;
        }

        public bool ApplyVisual(Sprite sprite)
        {
            bool changed = false;
            Transform legacyVisual = Transform.Find("ArtVisual");
            if (legacyVisual != null)
            {
                Undo.DestroyObjectImmediate(legacyVisual.gameObject);
                changed = true;
            }

            Transform visualTransform = Transform.Find(GroundArtVisualName);
            SpriteRenderer visualRenderer;
            if (visualTransform == null)
            {
                GameObject visualObject = new GameObject(GroundArtVisualName);
                visualObject.layer = Transform.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(visualObject, "Create Ground Visual");
                visualTransform = visualObject.transform;
                visualTransform.SetParent(Transform, false);
                visualRenderer = Undo.AddComponent<SpriteRenderer>(visualObject);
                changed = true;
            }
            else
            {
                visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
                if (visualRenderer == null)
                {
                    visualRenderer = Undo.AddComponent<SpriteRenderer>(visualTransform.gameObject);
                    changed = true;
                }
            }

            bool transformNeedsChange =
                Transform.localScale != localScale ||
                visualTransform.localPosition !=
                new Vector3(colliderOffset.x, colliderOffset.y, 0f) ||
                visualTransform.localRotation != Quaternion.identity ||
                visualTransform.localScale != Vector3.one;
            bool colliderNeedsChange =
                collider.size != colliderSize ||
                collider.offset != colliderOffset ||
                !Mathf.Approximately(collider.edgeRadius, colliderEdgeRadius) ||
                collider.enabled != colliderEnabled ||
                collider.isTrigger != colliderIsTrigger ||
                collider.autoTiling;
            bool rootRendererNeedsChange = Renderer != null;
            bool visualRendererNeedsChange =
                visualRenderer.sprite != sprite ||
                visualRenderer.drawMode != SpriteDrawMode.Sliced ||
                visualRenderer.size != colliderSize ||
                visualRenderer.color != Color.white ||
                !visualRenderer.enabled ||
                visualRenderer.sharedMaterial != visualMaterial ||
                visualRenderer.sortingLayerID != visualSortingLayerId ||
                visualRenderer.sortingOrder != visualSortingOrder;

            if (transformNeedsChange ||
                colliderNeedsChange ||
                rootRendererNeedsChange ||
                visualRendererNeedsChange)
            {
                Undo.RecordObjects(
                    new Object[] { Transform, visualTransform, visualRenderer, collider },
                    "Separate Zone 00 Ground Visual And Collider");

                Transform.localScale = localScale;
                visualTransform.localPosition =
                    new Vector3(colliderOffset.x, colliderOffset.y, 0f);
                visualTransform.localRotation = Quaternion.identity;
                visualTransform.localScale = Vector3.one;

                collider.size = colliderSize;
                collider.offset = colliderOffset;
                collider.edgeRadius = colliderEdgeRadius;
                collider.enabled = colliderEnabled;
                collider.isTrigger = colliderIsTrigger;
                collider.autoTiling = false;

                visualRenderer.sprite = sprite;
                visualRenderer.drawMode = SpriteDrawMode.Sliced;
                visualRenderer.size = colliderSize;
                visualRenderer.color = Color.white;
                visualRenderer.enabled = true;
                visualRenderer.sharedMaterial = visualMaterial;
                visualRenderer.sortingLayerID = visualSortingLayerId;
                visualRenderer.sortingOrder = visualSortingOrder;

                PrefabUtility.RecordPrefabInstancePropertyModifications(Transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                EditorUtility.SetDirty(Transform);
                EditorUtility.SetDirty(visualTransform);
                EditorUtility.SetDirty(visualRenderer);
                EditorUtility.SetDirty(collider);

                if (Renderer != null)
                {
                    Undo.DestroyObjectImmediate(Renderer);
                }

                changed = true;
            }

            return changed;
        }

        public bool HasUnchangedGameplayShape()
        {
            return Transform.localPosition == localPosition &&
                   Transform.localRotation == localRotation &&
                   Transform.localScale == localScale &&
                   collider.size == colliderSize &&
                   collider.offset == colliderOffset &&
                   Mathf.Approximately(collider.edgeRadius, colliderEdgeRadius) &&
                   collider.enabled == colliderEnabled &&
                   collider.isTrigger == colliderIsTrigger &&
                   !collider.autoTiling;
        }
    }

    private static Sprite GetReplacement(
        SpriteRenderer renderer,
        Sprite greyBluePillar1,
        Sprite greyBluePillar2,
        Sprite darkGreyBluePillar1,
        Sprite tree5,
        Sprite darkGreyBlueArch1,
        Sprite groundGrassStrip1,
        out bool preservePillar10Height)
    {
        preservePillar10Height = false;
        string objectName = renderer.gameObject.name;

        if (objectName.StartsWith("기둥3_0"))
        {
            return greyBluePillar1;
        }

        if (objectName.StartsWith("기둥8_0"))
        {
            return greyBluePillar2;
        }

        if (objectName.StartsWith("기둥9_0"))
        {
            return darkGreyBluePillar1;
        }

        if (objectName.StartsWith("기둥10_0"))
        {
            preservePillar10Height = true;
            return darkGreyBluePillar1;
        }

        if (objectName.StartsWith("나무 6_0"))
        {
            return tree5;
        }

        if (objectName.StartsWith("아치_0"))
        {
            return darkGreyBlueArch1;
        }

        if (objectName.StartsWith("나무 7_0"))
        {
            return groundGrassStrip1;
        }

        return null;
    }
}
