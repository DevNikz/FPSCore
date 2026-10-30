using UnityEditor;
using UnityEngine;

namespace GridMaster.EditorTools
{
    public class GridMasterShaderGUI : ShaderGUI
    {
        private const string LogPrefix = "[GridMaster] ";
        private const string ProjectionObjectKeyword = "_PROJECTION_OBJECT";
        private const string TintAxisKeyword = "_TINT_AXIS";
        private const string TintFaceKeyword = "_TINT_FACE";
        private static readonly int ProjectionId = Shader.PropertyToID("_Projection");
        private static readonly int TintModeId = Shader.PropertyToID("_TintMode");

        private static bool isSurfaceOpen = true;
        private static bool isProjectionOpen = true;
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
                    DrawProperties(materialEditor, properties, "_CellSize", "_TriplanarSharpness");
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

            isLinesOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isLinesOpen, "Grid Lines");
            if (isLinesOpen)
            {
                DrawProperties(materialEditor, properties, "_LineColor", "_MajorLineColor",
                    "_MinorLineWidth", "_MajorLineWidth", "_MajorInterval", "_LineSoftness");
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
                    ApplyGridKeywords(target as Material);
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

            SetKeyword(material, ProjectionObjectKeyword, isObjectUv);
            SetKeyword(material, TintAxisKeyword, tintMode == 1);
            SetKeyword(material, TintFaceKeyword, tintMode == 2);
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
