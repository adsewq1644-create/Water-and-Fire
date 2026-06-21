using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class SelectiveGlowBloomInstaller
{
    private const string GlowLayerName = "Glow";
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";
    private const string VolumeProfilePath = "Assets/DefaultVolumeProfile.asset";
    private const string GlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";

    static SelectiveGlowBloomInstaller()
    {
        EditorApplication.delayCall += Install;
    }

    private static void Install()
    {
        EnsureGlowLayer();
        DisableCompatibilityMode();
        DisableGlobalBloom();
        EnsureRendererFeature();
    }

    private static void EnsureGlowLayer()
    {
        Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == GlowLayerName)
            {
                return;
            }
        }

        SerializedProperty layer = layers.GetArrayElementAtIndex(8);
        if (string.IsNullOrEmpty(layer.stringValue))
        {
            layer.stringValue = GlowLayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }

    private static void DisableCompatibilityMode()
    {
        Object globalSettings = AssetDatabase.LoadAssetAtPath<Object>(GlobalSettingsPath);
        if (globalSettings == null)
        {
            return;
        }

        SerializedObject serializedSettings = new SerializedObject(globalSettings);
        SerializedProperty references = serializedSettings.FindProperty("references.RefIds");
        if (references == null)
        {
            return;
        }

        for (int i = 0; i < references.arraySize; i++)
        {
            SerializedProperty entry = references.GetArrayElementAtIndex(i);
            SerializedProperty type = entry.FindPropertyRelative("type");
            if (type != null && type.FindPropertyRelative("class").stringValue == "RenderGraphSettings")
            {
                SerializedProperty compatibility = entry.FindPropertyRelative("data.m_EnableRenderCompatibilityMode");
                if (compatibility != null && compatibility.intValue != 0)
                {
                    compatibility.intValue = 0;
                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(globalSettings);
                    AssetDatabase.SaveAssets();
                }

                return;
            }
        }
    }

    private static void DisableGlobalBloom()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(VolumeProfilePath);
        foreach (Object asset in assets)
        {
            if (asset == null || asset.name != "Bloom")
            {
                continue;
            }

            SerializedObject serializedProfile = new SerializedObject(asset);
            SerializedProperty active = serializedProfile.FindProperty("active");
            if (active != null && active.boolValue)
            {
                active.boolValue = false;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private static void EnsureRendererFeature()
    {
        Renderer2DData rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        if (rendererData == null)
        {
            return;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
        foreach (Object asset in assets)
        {
            if (asset is SelectiveGlowBloomFeature)
            {
                return;
            }
        }

        SelectiveGlowBloomFeature feature = ScriptableObject.CreateInstance<SelectiveGlowBloomFeature>();
        feature.name = "Selective Glow Bloom";
        int glowLayer = LayerMask.NameToLayer(GlowLayerName);
        feature.settings.glowLayer = 1 << (glowLayer >= 0 ? glowLayer : 8);
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        AssetDatabase.ImportAsset(RendererPath);

        SerializedObject serializedRenderer = new SerializedObject(rendererData);
        SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
        SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        int index = features.arraySize;
        features.InsertArrayElementAtIndex(index);
        features.GetArrayElementAtIndex(index).objectReferenceValue = feature;

        if (featureMap != null)
        {
            featureMap.InsertArrayElementAtIndex(index);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
            {
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
            }
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
    }
}
