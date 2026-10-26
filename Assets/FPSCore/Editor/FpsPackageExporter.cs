using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports Assets/FpsCore as a .unitypackage. Refuses while scripts don't compile, because the
/// FpsCore assembly can't reference shop code: a clean compile proves the package stands alone.
/// </summary>
public static class FpsPackageExporter
{
    const string PackageFolder = "Assets/FPSCore";
    const string DefaultFileName = "FPSCore.unitypackage";

    [MenuItem("Tools/FPS Core/Export .unitypackage")]
    static void ExportFromMenu()
    {
        string path = EditorUtility.SaveFilePanel(
            "Export FPS Core",
            "",
            DefaultFileName,
            "unitypackage"
        );
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("FpsPackageExporter: export cancelled.");
            return;
        }
        Debug.Log($"FpsPackageExporter: export = {TryExport(path)} ({path})");
    }

    /// <summary>Writes the package. False (no file written) if the folder is missing, scripts fail to compile, or the export fails.</summary>
    public static bool TryExport(string filePath)
    {
        if (!AssetDatabase.IsValidFolder(PackageFolder))
        {
            Debug.LogError($"FpsPackageExporter: '{PackageFolder}' not found.");
            return false;
        }
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError(
                "FpsPackageExporter: scripts don't compile. Fix the errors first; they list what the package still needs from the shop."
            );
            return false;
        }

        try
        {
            AssetDatabase.ExportPackage(PackageFolder, filePath, ExportPackageOptions.Recurse);
        }
        catch (Exception e)
        {
            Debug.LogError($"FpsPackageExporter: export failed ({e.Message}).");
            return false;
        }
        return File.Exists(filePath);
    }
}
