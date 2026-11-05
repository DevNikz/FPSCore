using UnityEditor;
using UnityEngine;

namespace GridMaster.EditorTools
{
    public class GridMasterShaderGUI : ShaderGUI
    {
        private const string LogPrefix = "[GridMaster] ";
        private const string ProjectionObjectKeyword = "_PROJECTION_OBJECT";
        private const string PatternCheckerKeyword = "_PATTERN_CHECKER";
        private const string PatternDotsKeyword = "_PATTERN_DOTS";
        private const string LabelsKeyword = "_LABELS";
        private const string TintAxisKeyword = "_TINT_AXIS";
        private const string TintFaceKeyword = "_TINT_FACE";
        private static readonly int ProjectionId = Shader.PropertyToID("_Projection");
        private static readonly int PatternModeId = Shader.PropertyToID("_PatternMode");
        private static readonly int TintModeId = Shader.PropertyToID("_TintMode");
        private static readonly int GlobalScaleId = Shader.PropertyToID(GridMasterGlobals.GlobalScaleName);
        private static readonly int LabelsEnabledId = Shader.PropertyToID("_LabelsEnabled");
        private static readonly int LabelAtlasId = Shader.PropertyToID("_LabelAtlas");

        private static bool isSurfaceOpen = true;
        private static bool isProjectionOpen = true;
        private static bool isPatternOpen = true;
        private static bool isLabelsOpen = true;
        private static bool isTintOpen = true;
        private static bool isLinesOpen = true;
        private static bool isFadeOpen = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null || properties == null)
            {
                Debug.LogWarning(LogPrefix + "ShaderGUI received no editor or properties.");
                return;
            }

            EditorGUI.BeginChangeCheck();

            isSurfaceOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isSurfaceOpen, "Surface");
            if (isSurfaceOpen)
            {
                DrawProperties(materialEditor, properties, "_BaseColor", "_Metallic", "_Smoothness");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            isProjectionOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isProjectionOpen, "Projection");
            if (isProjectionOpen)
            {
                DrawProperties(materialEditor, properties, "_Projection");
                if (IsPropertyOn(materialEditor, "_Projection"))
                {
                    DrawProperties(materialEditor, properties, "_ObjectUVTiling");
                }
                else
                {
                    DrawProperties(materialEditor, properties, "_CellSize", "_GlobalScaleMode", "_TriplanarSharpness");
                    if (GetFloat(materialEditor, "_GlobalScaleMode") > 0.5f && Shader.GetGlobalFloat(GlobalScaleId) <= 0f)
                    {
                        EditorGUILayout.HelpBox("No active Grid Master Globals in the scene, so this material uses its own Cell Size.", MessageType.Info);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            int patternMode = Mathf.RoundToInt(GetFloat(materialEditor, "_PatternMode"));
            bool isDotsMode = patternMode == 2;

            isPatternOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isPatternOpen, "Pattern");
            if (isPatternOpen)
            {
                DrawProperties(materialEditor, properties, "_PatternMode");
                if (patternMode == 1)
                {
                    DrawProperties(materialEditor, properties, "_CheckerColor", "_CheckerLines");
                }
                else if (isDotsMode)
                {
                    DrawProperties(materialEditor, properties, "_MinorDotSize", "_MajorDotSize");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            isLabelsOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isLabelsOpen, "Labels");
            if (isLabelsOpen)
            {
                DrawProperties(materialEditor, properties, "_LabelsEnabled");
                if (IsPropertyOn(materialEditor, "_LabelsEnabled"))
                {
                    MaterialProperty atlasProperty = FindProperty("_LabelAtlas", properties, false);
                    if (atlasProperty != null)
                    {
                        materialEditor.TexturePropertySingleLine(new GUIContent(atlasProperty.displayName), atlasProperty);
                        if (atlasProperty.textureValue == null)
                        {
                            EditorGUILayout.HelpBox("No label atlas assigned, so no text will draw. Use GridMasterLabelAtlas.png from GridMaster/Textures.", MessageType.Warning);
                        }
                    }
                    DrawProperties(materialEditor, properties, "_LabelColor", "_LabelSize", "_LabelOffset",
                        "_LabelUnit", "_LabelSoftness", "_LabelMinPixelHeight");
                    EditorGUILayout.HelpBox("Each major cell shows the world coordinate of its left major line. In Object UV mode it counts cells x Cell Size.", MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            isTintOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isTintOpen, "Axis and Face Tint");
            if (isTintOpen)
            {
                DrawProperties(materialEditor, properties, "_TintMode");
                int tintMode = Mathf.RoundToInt(GetFloat(materialEditor, "_TintMode"));
                if (tintMode > 0)
                {
                    DrawProperties(materialEditor, properties, "_TintStrength", "_TintSharpness");
                }
                if (tintMode == 1)
                {
                    DrawLabeledProperty(materialEditor, properties, "_TintPosX", "X");
                    DrawLabeledProperty(materialEditor, properties, "_TintPosY", "Y");
                    DrawLabeledProperty(materialEditor, properties, "_TintPosZ", "Z");
                }
                else if (tintMode == 2)
                {
                    DrawProperties(materialEditor, properties, "_TintPosX", "_TintNegX",
                        "_TintPosY", "_TintNegY", "_TintPosZ", "_TintNegZ");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            isLinesOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isLinesOpen, "Grid Lines / Dots");
            if (isLinesOpen)
            {
                DrawLabeledProperty(materialEditor, properties, "_LineColor", isDotsMode ? "Minor Dot Color" : "Minor Line Color");
                DrawLabeledProperty(materialEditor, properties, "_MajorLineColor", isDotsMode ? "Major Dot Color" : "Major Line Color");
                if (!isDotsMode)
                {
                    DrawProperties(materialEditor, properties, "_MinorLineWidth", "_MajorLineWidth");
                }
                DrawProperties(materialEditor, properties, "_MajorInterval", "_LineSoftness");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            isFadeOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isFadeOpen, "Distance Fade");
            if (isFadeOpen)
            {
                DrawProperties(materialEditor, properties, "_DistanceFade");
                EditorGUI.BeginDisabledGroup(!IsPropertyOn(materialEditor, "_DistanceFade"));
                DrawProperties(materialEditor, properties, "_FadeStart", "_FadeEnd", "_MajorFadeMultiplier");
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    Material material = target as Material;
                    ApplyGridKeywords(material);
                    TryAssignDefaultAtlas(material);
                }
            }

            EditorGUILayout.Space();
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ApplyGridKeywords(material);
            TryAssignDefaultAtlas(material);
        }

        // Syncs keywords with property values. Public so the apply tool and presets can reuse it.
        public static void ApplyGridKeywords(Material material)
        {
            if (material == null)
            {
                return;
            }

            bool isObjectUv = material.HasProperty(ProjectionId) && material.GetFloat(ProjectionId) > 0.5f;
            int tintMode = material.HasProperty(TintModeId) ? Mathf.RoundToInt(material.GetFloat(TintModeId)) : 0;
            int patternMode = material.HasProperty(PatternModeId) ? Mathf.RoundToInt(material.GetFloat(PatternModeId)) : 0;

            SetKeyword(material, ProjectionObjectKeyword, isObjectUv);
            SetKeyword(material, LabelsKeyword, material.HasProperty(LabelsEnabledId) && material.GetFloat(LabelsEnabledId) > 0.5f);
            SetKeyword(material, PatternCheckerKeyword, patternMode == 1);
            SetKeyword(material, PatternDotsKeyword, patternMode == 2);
            SetKeyword(material, TintAxisKeyword, tintMode == 1);
            SetKeyword(material, TintFaceKeyword, tintMode == 2);
        }

        // Assigns GridMasterLabelAtlas.png to materials that have none. Public so presets/tools can reuse it.
        public static void TryAssignDefaultAtlas(Material material)
        {
            if (material == null || !material.HasProperty(LabelAtlasId) || material.GetTexture(LabelAtlasId) != null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(GridMasterAtlasImporter.AtlasName + " t:Texture2D");
            if (guids.Length == 0)
            {
                return;
            }

            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (atlas != null)
            {
                material.SetTexture(LabelAtlasId, atlas);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool isEnabled)
        {
            if (isEnabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static float GetFloat(MaterialEditor materialEditor, string propertyName)
        {
            Material material = materialEditor.target as Material;
            return material != null && material.HasProperty(propertyName) ? material.GetFloat(propertyName) : 0f;
        }

        private static bool IsPropertyOn(MaterialEditor materialEditor, string propertyName)
        {
            return GetFloat(materialEditor, propertyName) > 0.5f;
        }

        private static void DrawProperties(MaterialEditor materialEditor, MaterialProperty[] properties, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                MaterialProperty property = FindProperty(propertyName, properties, false);
                if (property == null)
                {
                    Debug.LogWarning(LogPrefix + "Missing shader property " + propertyName);
                    continue;
                }
                materialEditor.ShaderProperty(property, property.displayName);
            }
        }

        private static void DrawLabeledProperty(MaterialEditor materialEditor, MaterialProperty[] properties, string propertyName, string label)
        {
            MaterialProperty property = FindProperty(propertyName, properties, false);
            if (property == null)
            {
                Debug.LogWarning(LogPrefix + "Missing shader property " + propertyName);
                return;
            }
            materialEditor.ShaderProperty(property, label);
        }
    }
}
