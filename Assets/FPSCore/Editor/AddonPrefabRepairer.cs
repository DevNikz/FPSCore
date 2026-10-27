using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

// Keeps prefabs that carry addon components (Interactable, Carriable, ...) safe while the
// addon package is missing, and re-validates them once the addon is installed.
//
// Missing-script components are NEVER removed here: Unity keeps their serialized data in the
// prefab YAML, and the link comes back by script GUID when the addon scripts are imported.
// Repair = force reimport of the affected prefabs + verify that nothing is still missing.
public static class AddonPrefabRepairer
{
    const string MenuPath = "Tools/FPS Core/Repair Addon Prefabs";
    const string ScanRoot = "Assets/FPSCore";
    const string AddonFolder = "Assets/FPSCore/Runtime/_Addon";
    const string PendingKey = "FPSCore.AddonPrefabRepair.Pending";
    const string CheckedKey = "FPSCore.AddonPrefabRepair.CheckedThisSession";
    const string LogPrefix = "[FPSCore] ";

    [MenuItem(MenuPath)]
    static void RepairFromMenu() => RunRepair(true);

    // Runs after every script compile / domain reload. Addon import brings new scripts,
    // so this is the moment their GUIDs become resolvable.
    [DidReloadScripts]
    static void OnScriptsReloaded()
    {
        bool isFirstReloadThisSession = !SessionState.GetBool(CheckedKey, false);
        bool isRepairPending = SessionState.GetBool(PendingKey, false);
        if (!isFirstReloadThisSession && !isRepairPending)
            return;

        SessionState.SetBool(CheckedKey, true);
        SchedulePendingRepair();
    }

    internal static void SchedulePendingRepair()
    {
        SessionState.SetBool(PendingKey, true);
        EditorApplication.delayCall += RunPendingRepair;
    }

    static void RunPendingRepair()
    {
        // A compile will end in a domain reload, which calls OnScriptsReloaded and retries.
        if (EditorApplication.isCompiling)
            return;

        if (EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunPendingRepair;
            return;
        }

        if (!SessionState.GetBool(PendingKey, false))
            return;

        SessionState.SetBool(PendingKey, false);
        RunRepair(false);
    }

    static void RunRepair(bool isVerbose)
    {
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogWarning(
                LogPrefix + "Scripts have compile errors; addon prefab repair skipped."
            );
            return;
        }

        List<string> brokenPaths = FindPrefabsWithMissingScripts();
        if (brokenPaths.Count == 0)
        {
            if (isVerbose)
                Debug.Log(LogPrefix + "No prefabs with missing scripts under " + ScanRoot + ".");
            return;
        }

        ReimportPrefabs(brokenPaths);

        int repairedCount = 0;
        foreach (string path in brokenPaths)
        {
            if (IsFullyResolved(path))
            {
                repairedCount++;
                continue;
            }

            Debug.LogWarning(
                LogPrefix + "Still has missing scripts (addon not installed yet?): " + path
            );
        }

        if (repairedCount > 0 || isVerbose)
            Debug.Log(
                LogPrefix
                    + "Addon prefab repair: "
                    + repairedCount
                    + " of "
                    + brokenPaths.Count
                    + " prefab(s) resolved."
            );
    }

    static List<string> FindPrefabsWithMissingScripts()
    {
        var brokenPaths = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ScanRoot });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null && CountMissingScriptsInHierarchy(root) > 0)
                brokenPaths.Add(path);
        }

        return brokenPaths;
    }

    static void ReimportPrefabs(List<string> paths)
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in paths)
            {
                try
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(LogPrefix + "Could not reimport " + path + ": " + e.Message);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    static bool IsFullyResolved(string path)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return root != null && CountMissingScriptsInHierarchy(root) == 0;
    }

    static int CountMissingScriptsInHierarchy(GameObject root)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        return count;
    }
}

// Flags a repair whenever anything under the addon folder is imported
// (an addon .unitypackage landing in the project). Prefab-only imports cause no
// domain reload, so the delayCall in SchedulePendingRepair covers that case.
class AddonImportPostprocessor : AssetPostprocessor
{
    const string AddonFolder = "Assets/FPSCore/Runtime/_Addon";

    static void OnPostprocessAllAssets(
        string[] imported,
        string[] deleted,
        string[] moved,
        string[] movedFrom
    )
    {
        if (imported.Any(path => path.StartsWith(AddonFolder, StringComparison.Ordinal)))
            AddonPrefabRepairer.SchedulePendingRepair();
    }
}
