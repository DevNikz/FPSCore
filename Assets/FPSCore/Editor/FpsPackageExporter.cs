using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports Assets/FPSCore as .unitypackage files: the Base (everything except the contents of the
/// addon folders) and one package per addon folder in Runtime/_Addon (plus the addons it depends on).
/// Refuses while scripts don't compile, because the FPSCore assembly can't reference shop code:
/// a clean compile proves the package stands alone.
/// </summary>
public static class FpsPackageExporter
{
    const string PackageFolder = "Assets/FPSCore";
    const string AddonFolder = "Assets/FPSCore/Runtime/_Addon";
    const string DefaultFileName = "FPSCore.unitypackage";

    [MenuItem("Tools/FPS Core/Export .unitypackage")]
    static void ExportFromMenu()
    {
        ExportWithSavePanel("Export FPS Core", DefaultFileName, TryExport);
    }

    [MenuItem("Tools/FPS Core/Export Addon...")]
    static void ShowAddonWindow()
    {
        FpsAddonExportWindow.Open();
    }

    /// <summary>Asks for a save path, then writes the addon package (used by the addon picker window).</summary>
    public static void ExportAddonWithSavePanel(string addonName)
    {
        ExportWithSavePanel(
            $"Export FPS Core addon: {addonName}",
            GetAddonFileName(addonName),
            path => TryExportAddon(addonName, path)
        );
    }

    static void ExportWithSavePanel(
        string title,
        string defaultFileName,
        Func<string, bool> tryExport
    )
    {
        string path = EditorUtility.SaveFilePanel(title, "", defaultFileName, "unitypackage");
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("FpsPackageExporter: export cancelled.");
            return;
        }
        Debug.Log($"FpsPackageExporter: export = {tryExport(path)} ({path})");
    }

    /// <summary>Writes the Base package (FPSCore minus the addon folders' contents). False (no file written) if the folder is missing, scripts fail to compile, or the export fails.</summary>
    public static bool TryExport(string filePath)
    {
        if (!AssetDatabase.IsValidFolder(PackageFolder))
        {
            Debug.LogError($"FpsPackageExporter: '{PackageFolder}' not found.");
            return false;
        }
        return TryExportAssets(GetBaseAssetPaths(), filePath);
    }

    /// <summary>Writes one addon package (its folder plus any addons it depends on). False (no file written) if the addon is unknown, scripts fail to compile, or the export fails.</summary>
    public static bool TryExportAddon(string addonName, string filePath)
    {
        if (!AssetDatabase.IsValidFolder($"{AddonFolder}/{addonName}"))
        {
            Debug.LogError(
                $"FpsPackageExporter: addon '{addonName}' not found in '{AddonFolder}'."
            );
            return false;
        }

        List<string> dependencyNames = GetDependencyAddonNames(addonName);
        if (dependencyNames.Count > 0)
        {
            Debug.Log(
                $"FpsPackageExporter: '{addonName}' also includes {string.Join(", ", dependencyNames)}."
            );
        }
        return TryExportAssets(GetAddonAssetPaths(addonName, dependencyNames), filePath);
    }

    static bool TryExportAssets(string[] assetPaths, string filePath)
    {
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError(
                "FpsPackageExporter: scripts don't compile. Fix the errors first; they list what the package still needs from the shop."
            );
            return false;
        }

        try
        {
            AssetDatabase.ExportPackage(assetPaths, filePath, ExportPackageOptions.Default);
        }
        catch (Exception e)
        {
            Debug.LogError($"FpsPackageExporter: export failed ({e.Message}).");
            return false;
        }
        return File.Exists(filePath);
    }

    public static List<string> GetAddonNames()
    {
        if (!AssetDatabase.IsValidFolder(AddonFolder))
        {
            return new List<string>();
        }
        return AssetDatabase
            .GetSubFolders(AddonFolder)
            .Select(folder => Path.GetFileName(folder))
            .ToList();
    }

    static string GetAddonFileName(string addonName)
    {
        return $"FPSCore_{addonName}.unitypackage";
    }

    static string[] GetBaseAssetPaths()
    {
        List<string> addonFolders = GetAddonNames().Select(GetAddonFolderPath).ToList();
        return GetPathsInFolder(PackageFolder)
            .Where(path => !addonFolders.Any(folder => IsPathInFolder(path, folder)))
            .ToArray();
    }

    static string[] GetAddonAssetPaths(string addonName, List<string> dependencyNames)
    {
        return dependencyNames
            .Prepend(addonName)
            .SelectMany(name => GetPathsInFolder(GetAddonFolderPath(name)))
            .ToArray();
    }

    /// <summary>Other addons referenced (directly or through other addons) by the assets of this addon.</summary>
    static List<string> GetDependencyAddonNames(string addonName)
    {
        string[] addonAssets = GetPathsInFolder(GetAddonFolderPath(addonName))
            .Where(path => !AssetDatabase.IsValidFolder(path))
            .ToArray();
        string[] dependencies = AssetDatabase.GetDependencies(addonAssets, true);

        return GetAddonNames()
            .Where(name => name != addonName)
            .Where(name => dependencies.Any(path => IsPathInFolder(path, GetAddonFolderPath(name))))
            .ToList();
    }

    static string GetAddonFolderPath(string addonName)
    {
        return $"{AddonFolder}/{addonName}";
    }

    static IEnumerable<string> GetPathsInFolder(string folder)
    {
        return AssetDatabase.GetAllAssetPaths().Where(path => IsPathInFolder(path, folder));
    }

    static bool IsPathInFolder(string path, string folder)
    {
        return path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal);
    }
}
