using UnityEditor;
using UnityEngine;

/// <summary>
/// Small picker listing the folders in FPSCore/Runtime/_Addon. A click exports that addon via
/// FpsPackageExporter. (A GenericMenu can't be opened from a [MenuItem]: it needs a GUI event.)
/// The window height follows the number of addons.
/// </summary>
public class FpsAddonExportWindow : EditorWindow
{
    private const float WINDOW_WIDTH = 300f;
    private const float WINDOW_PADDING = 10f;
    private const float HEADER_HEIGHT = 22f;
    private const float BUTTON_HEIGHT = 36f;
    private const float BUTTON_SPACING = 4f;
    private const float EMPTY_HEIGHT = 50f;

    public static void Open()
    {
        FpsAddonExportWindow window = GetWindow<FpsAddonExportWindow>(
            true,
            "Export FPS Core Addon",
            true
        );
        window.Show();
    }

    void OnGUI()
    {
        var addonNames = FpsPackageExporter.GetAddonNames();
        ResizeToFit(addonNames.Count);

        using (
            new EditorGUILayout.VerticalScope(
                new GUIStyle { padding = new RectOffset(10, 10, 10, 10) }
            )
        )
        {
            if (addonNames.Count == 0)
            {
                EditorGUILayout.HelpBox("No addons found.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Pick an addon to export", EditorStyles.boldLabel);
            foreach (string addonName in addonNames)
            {
                if (GUILayout.Button(addonName, GUILayout.Height(BUTTON_HEIGHT)))
                {
                    ExportAfterClose(addonName);
                    GUIUtility.ExitGUI();
                }
                GUILayout.Space(BUTTON_SPACING);
            }
        }
    }

    void ResizeToFit(int addonCount)
    {
        float contentHeight =
            addonCount == 0
                ? EMPTY_HEIGHT
                : HEADER_HEIGHT + addonCount * (BUTTON_HEIGHT + BUTTON_SPACING);
        Vector2 size = new Vector2(WINDOW_WIDTH, contentHeight + WINDOW_PADDING * 2f);
        if (minSize == size && maxSize == size)
        {
            return;
        }
        minSize = size;
        maxSize = size;
    }

    void ExportAfterClose(string addonName)
    {
        Close();
        EditorApplication.delayCall += () => FpsPackageExporter.ExportAddonWithSavePanel(addonName);
    }
}
