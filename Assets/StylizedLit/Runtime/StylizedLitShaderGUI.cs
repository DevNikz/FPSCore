// StylizedLitShaderGUI.cs  (Phase 1)
// Lives in Runtime/, so the whole file is wrapped in #if UNITY_EDITOR to keep player builds compiling.
// Subclasses URP's public BaseShaderGUI and reuses the public LitGUI helpers, mirroring URP's internal LitShader GUI.
// Phases 1-2 draw: Surface Options (+ Workflow Mode), Surface Inputs (base, metallic/specular, smoothness, normal,
//                height, occlusion, emission, tiling/offset) and Advanced Options (highlights, reflections, base advanced).
// Phase 2 adds the Detail Inputs foldout; Clear Coat comes from LitGUI.Inputs once the shader declares its properties.
// Phase 3+ adds stylized foldouts through FillAdditionalFoldouts.
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace StylizedLit.Editor
{
    public class StylizedLitShaderGUI : BaseShaderGUI
    {
        static readonly string[] WorkflowModeNames = Enum.GetNames(typeof(LitGUI.WorkflowMode));

        LitGUI.LitProperties _litProperties;
        MaterialProperty _detailMask, _detailAlbedoMapScale, _detailAlbedoMap, _detailNormalMapScale, _detailNormalMap;

        // Collect properties from the material properties.
        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            _litProperties = new LitGUI.LitProperties(properties);
            _detailMask = FindProperty("_DetailMask", properties, false);
            _detailAlbedoMapScale = FindProperty("_DetailAlbedoMapScale", properties, false);
            _detailAlbedoMap = FindProperty("_DetailAlbedoMap", properties, false);
            _detailNormalMapScale = FindProperty("_DetailNormalMapScale", properties, false);
            _detailNormalMap = FindProperty("_DetailNormalMap", properties, false);
        }

        // Adds the "Detail Inputs" foldout (Expandable.Details), same slot and order as URP Lit.
        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            materialScopesList.RegisterHeaderScope(DetailStyles.DetailInputs, Expandable.Details, _ => DrawDetailInputs());
        }

        // Material changed check. Keywords must stay in sync with the shader_feature list in StylizedLit.shader.
        public override void ValidateMaterial(Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));

            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, SetDetailKeywords);
        }

        // Material main surface options.
        public override void DrawSurfaceOptions(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            if (_litProperties.workflowMode != null)
                DoPopup(LitGUI.Styles.workflowModeText, _litProperties.workflowMode, WorkflowModeNames);

            base.DrawSurfaceOptions(material);
        }

        // Material main surface inputs.
        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            LitGUI.Inputs(_litProperties, materialEditor, material);
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        // Material main advanced options.
        public override void DrawAdvancedOptions(Material material)
        {
            if (_litProperties.reflections != null && _litProperties.highlights != null)
            {
                materialEditor.ShaderProperty(_litProperties.highlights, LitGUI.Styles.highlightsText);
                materialEditor.ShaderProperty(_litProperties.reflections, LitGUI.Styles.reflectionsText);
            }

            base.DrawAdvancedOptions(material);
        }

        // Switching a material from URP/Lit (or Legacy shaders) to this shader keeps shared property names.
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));

            // _Emission property is lost after assigning Standard shader to the material,
            // thus transfer it before assigning the new shader.
            if (material.HasProperty("_Emission"))
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));

            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            SetupMaterialBlendMode(material);
        }

        // URP's LitDetailGUI is internal, so its drawing and keyword rules are reproduced here.
        void DrawDetailInputs()
        {
            if (_detailMask == null || _detailAlbedoMap == null || _detailAlbedoMapScale == null ||
                _detailNormalMap == null || _detailNormalMapScale == null)
                return;

            materialEditor.TexturePropertySingleLine(DetailStyles.Mask, _detailMask);
            materialEditor.TexturePropertySingleLine(DetailStyles.AlbedoMap, _detailAlbedoMap,
                _detailAlbedoMap.textureValue != null ? _detailAlbedoMapScale : null);

            if (_detailAlbedoMapScale.floatValue != 1.0f)
                EditorGUILayout.HelpBox(DetailStyles.AlbedoScaleInfo.text, MessageType.Info, true);

            var detailAlbedoTexture = _detailAlbedoMap.textureValue as Texture2D;
            if (detailAlbedoTexture != null && GraphicsFormatUtility.IsSRGBFormat(detailAlbedoTexture.graphicsFormat))
                EditorGUILayout.HelpBox(DetailStyles.AlbedoFormatError.text, MessageType.Warning, true);

            materialEditor.TexturePropertySingleLine(DetailStyles.NormalMap, _detailNormalMap,
                _detailNormalMap.textureValue != null ? _detailNormalMapScale : null);
            materialEditor.TextureScaleOffsetProperty(_detailAlbedoMap);
        }

        static void SetDetailKeywords(Material material)
        {
            if (!material.HasProperty("_DetailAlbedoMap") ||
                !material.HasProperty("_DetailNormalMap") ||
                !material.HasProperty("_DetailAlbedoMapScale"))
                return;

            bool isScaled = material.GetFloat("_DetailAlbedoMapScale") != 1.0f;
            bool hasDetailMap = material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap");
            CoreUtils.SetKeyword(material, "_DETAIL_MULX2", !isScaled && hasDetailMap);
            CoreUtils.SetKeyword(material, "_DETAIL_SCALED", isScaled && hasDetailMap);
        }

        static class DetailStyles
        {
            public static readonly GUIContent DetailInputs = EditorGUIUtility.TrTextContent("Detail Inputs",
                "These settings define the surface details by tiling and overlaying additional maps on the surface.");

            public static readonly GUIContent Mask = EditorGUIUtility.TrTextContent("Mask",
                "Select a mask for the Detail map. The mask uses the alpha channel of the selected texture. The Tiling and Offset settings have no effect on the mask.");

            public static readonly GUIContent AlbedoMap = EditorGUIUtility.TrTextContent("Base Map",
                "Select the surface detail texture. The alpha of your texture determines surface hue and intensity.");

            public static readonly GUIContent NormalMap = EditorGUIUtility.TrTextContent("Normal Map",
                "Designates a Normal Map to create the illusion of bumps and dents in the details of this Material's surface.");

            public static readonly GUIContent AlbedoScaleInfo = EditorGUIUtility.TrTextContent(
                "Setting the scaling factor to a value other than 1 results in a less performant shader variant.");

            public static readonly GUIContent AlbedoFormatError = EditorGUIUtility.TrTextContent(
                "This texture is not in linear space.");
        }
    }
}
#endif
