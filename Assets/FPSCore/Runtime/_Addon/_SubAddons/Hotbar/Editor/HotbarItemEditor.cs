#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HotbarItem inspector: the definition field plus a "New" button that creates a
/// HotbarItemDefinition in the Hotbar _Data folder and assigns it.
/// Lives next to HotbarItem (editor-only via #if) so it travels with the Hotbar addon.
/// </summary>
[CustomEditor(typeof(HotbarItem))]
[CanEditMultipleObjects]
public class HotbarItemEditor : Editor
{
    const string DataFolder = "Assets/FPSCore/Runtime/_Addon/_SubAddons/Hotbar/_Data";

    SerializedProperty _definition;

    void OnEnable()
    {
        _definition = serializedObject.FindProperty("definition");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool create;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(_definition);
            create = GUILayout.Button(
                new GUIContent(
                    "New",
                    "Create a new Hotbar Item Definition in _Data and assign it."
                ),
                GUILayout.Width(44f)
            );
        }

        if (create)
            CreateDefinition();

        serializedObject.ApplyModifiedProperties();
    }

    // One new asset, assigned to every selected HotbarItem. Named after the first selected object.
    void CreateDefinition()
    {
        EnsureFolder(DataFolder);

        string baseName = string.Concat(
            ((HotbarItem)target).gameObject.name.Split(Path.GetInvalidFileNameChars())
        );
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "HotbarItem";

        string path = AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{baseName}.asset");
        var asset = CreateInstance<HotbarItemDefinition>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty("displayName").stringValue = baseName;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        _definition.objectReferenceValue = asset;
        EditorGUIUtility.PingObject(asset);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
