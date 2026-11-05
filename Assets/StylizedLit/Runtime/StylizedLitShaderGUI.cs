// StylizedLitShaderGUI.cs  (Phase 1)
// Lives in Runtime/, so the whole file is wrapped in #if UNITY_EDITOR to keep player builds compiling.
// Subclasses URP's public BaseShaderGUI and reuses the public LitGUI helpers, mirroring URP's internal LitShader GUI.
// Phases 1-4 draw: Surface Options (+ Workflow Mode), Surface Inputs (base, metallic/specular, smoothness, normal,
//                height, occlusion, emission, tiling/offset) and Advanced Options (highlights, reflections, base advanced).
// Phase 2 adds the Detail Inputs foldout; Clear Coat comes from LitGUI.Inputs once the shader declares its properties.
// Phase 3 adds the Toon Lighting foldout and the _TOON_ON / _TOON_RAMPMAP keyword rules.
// Phase 4 adds Rim Light, Matcap and Outline foldouts; Outline works by enabling/disabling the "SRPDefaultUnlit" pass.
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

        // Custom foldout bit; BaseShaderGUI.Expandable uses bits 0-3.
        const uint ToonFoldoutBit = 1u << 4;
        const uint RimFoldoutBit = 1u << 5;
        const uint MatcapFoldoutBit = 1u << 6;
        const uint OutlineFoldoutBit = 1u << 7;
        const string OutlinePassName = "SRPDefaultUnlit";

        LitGUI.LitProperties _litProperties;
        MaterialProperty[] _properties;
        MaterialProperty _detailMask, _detailAlbedoMapScale, _detailAlbedoMap, _detailNormalMapScale, _detailNormalMap;

        // Collect properties from the material properties.
        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            _properties = properties;
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
            materialScopesList.RegisterHeaderScope(ToonStyles.Header, ToonFoldoutBit, _ => DrawToonInputs());
            materialScopesList.RegisterHeaderScope(new GUIContent("Rim Light"), RimFoldoutBit, _ => DrawRimInputs());
            materialScopesList.RegisterHeaderScope(new GUIContent("Matcap"), MatcapFoldoutBit, _ => DrawMatcapInputs());
            materialScopesList.RegisterHeaderScope(new GUIContent("Outline"), OutlineFoldoutBit, _ => DrawOutlineInputs());
        }

        // Material changed check. Keywords must stay in sync with the shader_feature list in StylizedLit.shader.
        public override void ValidateMaterial(Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));

            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, SetStylizedKeywords);
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

        // Draws a property by name if the shader declares it.
        void Draw(string name, GUIContent label)
        {
            var prop = FindProperty(name, _properties, false);
            if (prop != null)
                materialEditor.ShaderProperty(prop, label);
        }

        // Draws an enable toggle followed by rows that are greyed out while the toggle is off.
        void DrawSection(string enableProp, string enableLabel, string enableTip, params (string name, string label, string tip)[] rows)
        {
            var enabled = FindProperty(enableProp, _properties, false);
            if (enabled == null)
                return;

            materialEditor.ShaderProperty(enabled, new GUIContent(enableLabel, enableTip));
            using (new EditorGUI.DisabledScope(enabled.floatValue < 0.5f))
            {
                foreach (var row in rows)
                    Draw(row.name, new GUIContent(row.label, row.tip));
            }
        }

        void DrawRimInputs()
        {
            DrawSection("_RimEnabled", "Enable Rim Light", "Adds a fresnel rim to the lit color.",
                ("_RimColor", "Color", "HDR rim color."),
                ("_RimThreshold", "Threshold", "Fresnel value where the rim starts. Higher values give a thinner rim."),
                ("_RimSoftness", "Softness", "Edge softness. 0 is a hard cel-style edge."),
                ("_RimLightMask", "Light Direction Mask", "0 = rim all around, 1 = rim only on the side facing the main light."),
                ("_RimShadowAware", "Hide In Main Light Shadow", "Fades the rim where the main light's shadow falls."));
        }

        void DrawMatcapInputs()
        {
            DrawSection("_MatcapEnabled", "Enable Matcap", "Adds a view-space material capture on top of the lit color.",
                ("_MatcapMap", "Matcap", "Material capture texture, sampled with the view-space normal."),
                ("_MatcapTint", "Tint", "Color multiplied into the matcap."),
                ("_MatcapStrength", "Strength", "Blend amount."),
                ("_MatcapBlend", "Blend Mode", "Additive, Multiply or Replace."));
        }

        void DrawOutlineInputs()
        {
            DrawSection("_OutlineEnabled", "Enable Outline", "Draws an inverted-hull outline pass (SRPDefaultUnlit). Disabled materials skip the pass entirely.",
                ("_OutlineColor", "Color", "Outline color. Fog is applied."),
                ("_OutlineWidth", "Width", "Centimeters in world space, or pixels when Screen Space Width is 1."),
                ("_OutlineDistanceScaling", "Screen Space Width", "0 = width fixed in world space, 1 = width fixed in screen pixels."));
        }

        void DrawToonInputs()
        {
            var enabled = FindProperty("_ToonEnabled", _properties, false);
            if (enabled == null)
                return;

            materialEditor.ShaderProperty(enabled, ToonStyles.Enable);

            using (new EditorGUI.DisabledScope(enabled.floatValue < 0.5f))
            {
                Draw("_ToonStrength", ToonStyles.Strength);

                var ramp = FindProperty("_ToonRampMap", _properties, false);
                if (ramp != null)
                {
                    materialEditor.TexturePropertySingleLine(ToonStyles.RampMap, ramp);
                    if (ramp.textureValue != null && ramp.textureValue.wrapMode != TextureWrapMode.Clamp)
                        EditorGUILayout.HelpBox(ToonStyles.RampWrapWarning.text, MessageType.Warning, true);
                }

                // Procedural steps are only used when no ramp texture is assigned.
                using (new EditorGUI.DisabledScope(ramp != null && ramp.textureValue != null))
                {
                    Draw("_ToonSteps", ToonStyles.Steps);
                    Draw("_ToonStepSmoothness", ToonStyles.StepSmoothness);
                }

                Draw("_ToonThreshold", ToonStyles.Threshold);
                Draw("_ToonHalfLambert", ToonStyles.HalfLambert);
                Draw("_ToonShadowColor", ToonStyles.ShadowTint);
                Draw("_ToonAdditionalLights", ToonStyles.AdditionalLights);

                EditorGUILayout.Space();
                Draw("_ToonSpecularColor", ToonStyles.SpecularColor);
                Draw("_ToonSpecularStrength", ToonStyles.SpecularStrength);
                Draw("_ToonSpecularSize", ToonStyles.SpecularSize);
                Draw("_ToonSpecularSmoothness", ToonStyles.SpecularSmoothness);
            }
        }

        static void SetStylizedKeywords(Material material)
        {
            SetDetailKeywords(material);
            SetToonKeywords(material);
            SetToggleKeyword(material, "_RimEnabled", "_RIM_ON");
            SetToggleKeyword(material, "_MatcapEnabled", "_MATCAP_ON");
            SetOutlinePass(material);
        }

        static void SetToggleKeyword(Material material, string toggleProperty, string keyword)
        {
            if (!material.HasProperty(toggleProperty))
                return;

            CoreUtils.SetKeyword(material, keyword, material.GetFloat(toggleProperty) > 0.5f);
        }

        // The outline pass is the only SRPDefaultUnlit pass in the shader, so it can be toggled per material.
        static void SetOutlinePass(Material material)
        {
            if (!material.HasProperty("_OutlineEnabled"))
                return;

            bool enabled = material.GetFloat("_OutlineEnabled") > 0.5f;
            if (material.GetShaderPassEnabled(OutlinePassName) != enabled)
                material.SetShaderPassEnabled(OutlinePassName, enabled);
        }

        static void SetToonKeywords(Material material)
        {
            if (!material.HasProperty("_ToonEnabled"))
                return;

            bool toon = material.GetFloat("_ToonEnabled") > 0.5f;
            bool hasRamp = material.HasProperty("_ToonRampMap") && material.GetTexture("_ToonRampMap") != null;
            CoreUtils.SetKeyword(material, "_TOON_ON", toon);
            CoreUtils.SetKeyword(material, "_TOON_RAMPMAP", toon && hasRamp);
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

        static class ToonStyles
        {
            public static readonly GUIContent Header = EditorGUIUtility.TrTextContent("Toon Lighting",
                "Cel-shaded lighting that can be blended with the physically based result.");
            public static readonly GUIContent Enable = EditorGUIUtility.TrTextContent("Enable Toon Lighting",
                "Compiles the toon light path. Disabled materials use plain PBR lighting.");
            public static readonly GUIContent Strength = EditorGUIUtility.TrTextContent("Toon Strength",
                "0 = physically based lighting, 1 = fully toon. Values in between blend per light.");
            public static readonly GUIContent RampMap = EditorGUIUtility.TrTextContent("Ramp Texture",
                "Optional horizontal gradient: left = shadow, right = lit. Overrides Steps and Step Smoothness. Set Wrap Mode to Clamp.");
            public static readonly GUIContent RampWrapWarning = EditorGUIUtility.TrTextContent(
                "Set the ramp texture's Wrap Mode to Clamp to avoid edge bleeding.");
            public static readonly GUIContent Steps = EditorGUIUtility.TrTextContent("Steps",
                "Number of light bands (2 = classic two-tone).");
            public static readonly GUIContent StepSmoothness = EditorGUIUtility.TrTextContent("Step Smoothness",
                "How soft the transition between bands is.");
            public static readonly GUIContent Threshold = EditorGUIUtility.TrTextContent("Shadow Threshold",
                "Moves where the shadow begins. Higher values give more shadow.");
            public static readonly GUIContent HalfLambert = EditorGUIUtility.TrTextContent("Half Lambert",
                "Wraps the light so the unlit side is not fully dark.");
            public static readonly GUIContent ShadowTint = EditorGUIUtility.TrTextContent("Shadow Tint",
                "Color of the main light's shadowed regions.");
            public static readonly GUIContent AdditionalLights = EditorGUIUtility.TrTextContent("Apply To Additional Lights",
                "When off, additional (point/spot) lights keep PBR shading.");
            public static readonly GUIContent SpecularColor = EditorGUIUtility.TrTextContent("Specular Color");
            public static readonly GUIContent SpecularStrength = EditorGUIUtility.TrTextContent("Specular Strength",
                "0 removes the toon highlight.");
            public static readonly GUIContent SpecularSize = EditorGUIUtility.TrTextContent("Specular Size");
            public static readonly GUIContent SpecularSmoothness = EditorGUIUtility.TrTextContent("Specular Smoothness",
                "Edge softness of the toon highlight.");
        }
    }
}
#endif
