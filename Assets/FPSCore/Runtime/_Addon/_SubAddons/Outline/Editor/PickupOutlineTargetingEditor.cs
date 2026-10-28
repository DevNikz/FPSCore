using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Inspector for PickupOutlineTargeting: shows whether the URP renderer feature is installed and adds it.</summary>
[CustomEditor(typeof(PickupOutlineTargeting))]
public class PickupOutlineTargetingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        var data = GetDefaultRendererData(out string error);
        if (data == null)
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
            return;
        }

        bool hasFeature = HasFeature(data);
        EditorGUILayout.HelpBox(
            hasFeature
                ? $"Renderer feature present on '{data.name}'."
                : $"Renderer feature missing on '{data.name}'. Outlines will not draw.",
            hasFeature ? MessageType.Info : MessageType.Warning
        );
        using (new EditorGUI.DisabledScope(hasFeature))
        {
            if (GUILayout.Button("Add Outline Renderer Feature"))
                AddFeature(data);
        }
    }

    static ScriptableRendererData GetDefaultRendererData(out string error)
    {
        error = null;
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
        {
            error = "No active URP asset found (Project Settings > Graphics).";
            return null;
        }

        var so = new SerializedObject(asset);
        var list = so.FindProperty("m_RendererDataList");
        int index = so.FindProperty("m_DefaultRendererIndex").intValue;
        if (list == null || index < 0 || index >= list.arraySize)
        {
            error = $"URP asset '{asset.name}' has no default renderer.";
            return null;
        }

        var data =
            list.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
        if (data == null)
            error = $"URP asset '{asset.name}' default renderer slot is empty.";
        return data;
    }

    static bool HasFeature(ScriptableRendererData data)
    {
        foreach (var f in data.rendererFeatures)
            if (f is PickupOutlineRendererFeature)
                return true;
        return false;
    }

    static void AddFeature(ScriptableRendererData data)
    {
        var shader = Shader.Find("Hidden/PickupOutline");
        if (shader == null)
        {
            Debug.LogWarning("PickupOutline: shader 'Hidden/PickupOutline' not found.");
            return;
        }

        var feature = CreateInstance<PickupOutlineRendererFeature>();
        feature.name = "PickupOutline";
        var featureSo = new SerializedObject(feature);
        featureSo.FindProperty("shader").objectReferenceValue = shader;
        featureSo.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCompleteObjectUndo(data, "Add Outline Renderer Feature");
        AssetDatabase.AddObjectToAsset(feature, data);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        var so = new SerializedObject(data);
        var features = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }
}
