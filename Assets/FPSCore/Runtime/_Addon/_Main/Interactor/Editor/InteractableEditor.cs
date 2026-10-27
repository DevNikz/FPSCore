using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Interactor Editor Inspector
/// - Draws the default inspector, then an "Add-ons" foldout.
/// - Lists one "Add X" button per [FPSAddon] component found in Assets/FPSCore/Runtime/Addon/
///   that is not yet on the object.
/// - Clicking adds the component (Undo-aware, multi-object); the button then disappears.
/// - Removing the component brings the button back.
/// </summary>
[CustomEditor(typeof(Interactable), true)]
[CanEditMultipleObjects]
public class InteractableEditor : Editor
{
    private const string AddonFolder = "Assets/FPSCore/Runtime/_Addon/_SubAddons";
    private const string FoldoutKey = "FPSCore.InteractableEditor.AddonsFoldout";

    private static readonly HashSet<Type> _warned = new HashSet<Type>();

    private Type[] _addonTypes = Array.Empty<Type>();

    private void OnEnable()
    {
        _addonTypes = GetAddonTypes();
        EditorApplication.hierarchyChanged += Repaint;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Repaint;
        Undo.undoRedoPerformed -= Repaint;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawAddonSection();
    }

    private void DrawAddonSection()
    {
        var missing = _addonTypes.Where(IsAddonMissing).ToList();
        if (missing.Count == 0)
            return;

        EditorGUILayout.Space();
        bool open = SessionState.GetBool(FoldoutKey, true);
        open = EditorGUILayout.Foldout(open, "Add-ons", true);
        SessionState.SetBool(FoldoutKey, open);
        if (!open)
            return;

        foreach (var type in missing)
        {
            if (!GUILayout.Button($"Add {GetAddonLabel(type)}"))
                continue;
            AddAddonToTargets(type);
            GUIUtility.ExitGUI(); // component set changed mid-layout
        }
    }

    // True if any selected object lacks the addon.
    private bool IsAddonMissing(Type type)
    {
        foreach (var t in targets)
        {
            var c = t as Component;
            if (c != null && c.GetComponent(type) == null)
                return true;
        }
        return false;
    }

    private void AddAddonToTargets(Type type)
    {
        foreach (var t in targets)
        {
            var c = t as Component;
            if (c == null || c.GetComponent(type) != null)
                continue;
            if (Undo.AddComponent(c.gameObject, type) == null)
                Debug.LogError($"{c.name}: failed to add {type.Name}.", c);
        }
    }

    private static string GetAddonLabel(Type type)
    {
        var attr = type.GetCustomAttribute<InteractableAddonAttribute>();
        return attr != null && !string.IsNullOrWhiteSpace(attr.DisplayName)
            ? attr.DisplayName
            : ObjectNames.NicifyVariableName(type.Name);
    }

    private static bool IsValidAddonType(Type type)
    {
        return typeof(MonoBehaviour).IsAssignableFrom(type)
            && !type.IsAbstract
            && !type.IsGenericTypeDefinition;
    }

    // [FPSAddon] types (reflection) that live in the Addon folder, sorted by label.
    private static Type[] GetAddonTypes()
    {
        var folderTypes = GetTypesInAddonFolder();
        var result = new List<Type>();

        foreach (var type in TypeCache.GetTypesWithAttribute<InteractableAddonAttribute>())
        {
            if (!folderTypes.Contains(type))
                continue;
            if (!IsValidAddonType(type))
            {
                if (_warned.Add(type))
                    Debug.LogWarning(
                        $"{type.Name}: [FPSAddon] needs a concrete, non-generic MonoBehaviour. Skipped."
                    );
                continue;
            }
            result.Add(type);
        }

        result.Sort(
            (a, b) =>
                string.Compare(
                    GetAddonLabel(a),
                    GetAddonLabel(b),
                    StringComparison.OrdinalIgnoreCase
                )
        );
        return result.ToArray();
    }

    private static HashSet<Type> GetTypesInAddonFolder()
    {
        var types = new HashSet<Type>();
        if (!AssetDatabase.IsValidFolder(AddonFolder))
            return types;

        foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { AddonFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var type = AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass();
            if (type != null)
                types.Add(type);
        }
        return types;
    }
}
