using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class ShockwaveDistortionRendererFeatureInstaller
{
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";
    private const string ShaderPath =
        "Assets/Shaders/Effects/ShockwaveDistortion2D.shader";

    static ShockwaveDistortionRendererFeatureInstaller()
    {
        EditorApplication.delayCall += EnsureRendererFeature;
    }

    private static void EnsureRendererFeature()
    {
        Renderer2DData rendererData =
            AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (rendererData == null || shader == null)
        {
            return;
        }

        ShockwaveDistortionRendererFeature feature = null;
        Object[] rendererAssets = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
        for (int index = 0; index < rendererAssets.Length; index++)
        {
            if (rendererAssets[index] is ShockwaveDistortionRendererFeature existing)
            {
                feature = existing;
                break;
            }
        }

        bool created = feature == null;
        if (created)
        {
            feature =
                ScriptableObject.CreateInstance<ShockwaveDistortionRendererFeature>();
            feature.name = "Shockwave Screen Distortion";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
        }

        feature.Shader = shader;
        EditorUtility.SetDirty(feature);

        SerializedObject serializedRenderer = new SerializedObject(rendererData);
        SerializedProperty features =
            serializedRenderer.FindProperty("m_RendererFeatures");
        SerializedProperty featureMap =
            serializedRenderer.FindProperty("m_RendererFeatureMap");

        int featureIndex = FindFeatureIndex(features, feature);
        if (featureIndex < 0)
        {
            featureIndex = features.arraySize;
            features.InsertArrayElementAtIndex(featureIndex);
            features.GetArrayElementAtIndex(featureIndex).objectReferenceValue = feature;
        }

        if (featureMap != null && featureMap.arraySize <= featureIndex)
        {
            featureMap.InsertArrayElementAtIndex(featureIndex);
        }

        if (featureMap != null &&
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                feature,
                out string _,
                out long localId))
        {
            featureMap.GetArrayElementAtIndex(featureIndex).longValue = localId;
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
    }

    private static int FindFeatureIndex(
        SerializedProperty features,
        ShockwaveDistortionRendererFeature feature)
    {
        for (int index = 0; index < features.arraySize; index++)
        {
            if (features.GetArrayElementAtIndex(index).objectReferenceValue == feature)
            {
                return index;
            }
        }

        return -1;
    }
}
