using UnityEditor;
using UnityEngine;

namespace SSGICore.Editor
{
    public static class SSGICoreExporter
    {
        const string kRoot = "Assets/SSGICore";

        [MenuItem("Tools/SSGICore/Export .unitypackage")]
        public static void Export()
        {
            if (!AssetDatabase.IsValidFolder(kRoot))
                throw new System.InvalidOperationException("SSGICore folder not found at " + kRoot);

            string path = EditorUtility.SaveFilePanel("Export SSGICore", "", "SSGICore_v0.1.0", "unitypackage");
            if (string.IsNullOrEmpty(path))
                return;

            AssetDatabase.ExportPackage(kRoot, path, ExportPackageOptions.Recurse);
            Debug.Log("[SSGICore] Exported package to " + path);
        }
    }
}
