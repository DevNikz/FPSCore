using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports the entirety of Assets/FPSCore (base plus every addon's contents) as one .unitypackage.
/// Reuses FpsPackageExporter's save panel and compile guard.
/// </summary>
public static class FpsCompletePackageExporter
{
    const string Version = "1.1.7";

    [MenuItem("Tools/FPS Core/Export Complete .unitypackage")]
    static void ExportFromMenu()
    {
        FpsPackageExporter.ExportWithSavePanel(
            "Export FPS Core (Complete)",
            GetCompleteFileName(),
            TryExportComplete
        );
    }

    /// <summary>Writes the complete package (all of FPSCore, no exclusions). False (no file written) if the folder is missing, scripts fail to compile, or the export fails.</summary>
    public static bool TryExportComplete(string filePath)
    {
        if (!AssetDatabase.IsValidFolder(FpsPackageExporter.PackageFolder))
        {
            Debug.LogError(
                $"FpsCompletePackageExporter: '{FpsPackageExporter.PackageFolder}' not found."
            );
            return false;
        }

        string[] assetPaths = FpsPackageExporter
            .GetPathsInFolder(FpsPackageExporter.PackageFolder)
            .ToArray();
        return FpsPackageExporter.TryExportAssets(assetPaths, filePath);
    }

    static string GetCompleteFileName()
    {
        return $"FPSCore_Complete_v{Version}.unitypackage";
    }
}
