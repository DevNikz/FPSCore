// Stylized/Master Lit  (Phase 4: Lit parity + toon + rim + matcap + outline, Forward+ only)
// Mirrors Unity 6.3 (6000.3, URP 17.3) Lit / ComplexLit: property names, keywords and pass layout.
// Cluster (Forward+) light loop keyword is _CLUSTER_LIGHT_LOOP (replaced _FORWARD_PLUS from Unity 6.1).
// Forward-only: no GBuffer pass. Deferred renders this via the UniversalForwardOnly pass.
Shader "Stylized/Master Lit"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // Clear coat (ComplexLit parity). Mask/Smoothness are already in the CBUFFER (layout locked in Phase 1).
        // LitGUI.Inputs draws the section when all four properties exist.
        [ToggleUI] _ClearCoat("Clear Coat", Float) = 0.0
        _ClearCoatMap("Clear Coat Map", 2D) = "white" {}
        _ClearCoatMask("Clear Coat Mask", Range(0.0, 1.0)) = 0.0
        _ClearCoatSmoothness("Clear Coat Smoothness", Range(0.0, 1.0)) = 1.0

        // Toon lighting (Phase 3). _ToonEnabled drives the _TOON_ON keyword from the GUI.
        [ToggleUI] _ToonEnabled("Enable Toon Lighting", Float) = 0.0
        _ToonStrength("Toon Strength", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset] _ToonRampMap("Toon Ramp", 2D) = "white" {}
        _ToonSteps("Steps", Range(2, 8)) = 3
        _ToonStepSmoothness("Step Smoothness", Range(0.0, 1.0)) = 0.05
        _ToonThreshold("Shadow Threshold", Range(-1.0, 1.0)) = 0.0
        [ToggleUI] _ToonHalfLambert("Half Lambert", Float) = 0.0
        _ToonShadowColor("Shadow Tint", Color) = (0.35, 0.35, 0.45, 1)
        [ToggleUI] _ToonAdditionalLights("Apply To Additional Lights", Float) = 1.0
        [HDR] _ToonSpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _ToonSpecularStrength("Specular Strength", Range(0.0, 1.0)) = 0.5
        _ToonSpecularSize("Specular Size", Range(0.0, 1.0)) = 0.12
        _ToonSpecularSmoothness("Specular Smoothness", Range(0.0, 1.0)) = 0.02

        // Rim light (Phase 4). _RimEnabled drives _RIM_ON.
        [ToggleUI] _RimEnabled("Enable Rim Light", Float) = 0.0
        [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimThreshold("Rim Threshold", Range(0.0, 1.0)) = 0.6
        _RimSoftness("Rim Softness", Range(0.0, 1.0)) = 0.1
        _RimLightMask("Light Direction Mask", Range(0.0, 1.0)) = 0.0
        [ToggleUI] _RimShadowAware("Hide In Main Light Shadow", Float) = 0.0

        // Matcap (Phase 4). _MatcapEnabled drives _MATCAP_ON.
        [ToggleUI] _MatcapEnabled("Enable Matcap", Float) = 0.0
        [NoScaleOffset] _MatcapMap("Matcap", 2D) = "white" {}
        _MatcapTint("Matcap Tint", Color) = (1, 1, 1, 1)
        _MatcapStrength("Matcap Strength", Range(0.0, 1.0)) = 1.0
        [Enum(Additive, 0, Multiply, 1, Replace, 2)] _MatcapBlend("Blend Mode", Float) = 0.0

        // Outline (Phase 4). _OutlineEnabled turns the "SRPDefaultUnlit" pass on and off from the GUI.
        [ToggleUI] _OutlineEnabled("Enable Outline", Float) = 0.0
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Range(0.0, 10.0)) = 2.0
        _OutlineDistanceScaling("Screen Space Width", Range(0.0, 1.0)) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // Obsolete properties (lightmapper / legacy upgrade path)
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "ComplexLit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward-only pass. Shades all lights in a single pass. GI + emission + fog.
        //  Also acts as the opaque forward fallback when the renderer is Deferred.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex StylizedLitPassVertex
            #pragma fragment StylizedLitPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _ _CLEARCOAT _CLEARCOATMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _TOON_ON
            #pragma shader_feature_local_fragment _TOON_RAMPMAP
            #pragma shader_feature_local_fragment _RIM_ON
            #pragma shader_feature_local_fragment _MATCAP_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitForwardPass.hlsl"
            ENDHLSL
        }

        // Inverted-hull outline. Drawn by the renderer's unlit pass list (SRPDefaultUnlit) in addition to ForwardLit.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex StylizedOutlineVertex
            #pragma fragment StylizedOutlineFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitOutlinePass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex StylizedShadowPassVertex
            #pragma fragment StylizedShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // Differentiates directional vs punctual shadow Normal Bias during shadow map generation
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex StylizedDepthOnlyVertex
            #pragma fragment StylizedDepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitDepthOnlyPass.hlsl"
            ENDHLSL
        }

        // Used for _CameraNormalsTexture (Forward+/Forward) and the depth-normals prepass (Deferred).
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex StylizedDepthNormalsVertex
            #pragma fragment StylizedDepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // Lightmap baking only.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex UniversalVertexMeta
            #pragma fragment StylizedFragmentMeta

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitMetaPass.hlsl"
            ENDHLSL
        }

        // Object motion vectors (TAA, motion blur, STP). Phase 5 replaces ObjectMotionVectors.hlsl with a copy
        // that also applies dither/dissolve clipping. XRMotionVectors and Universal2D are intentionally not included.
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Assets/StylizedLit/Runtime/HLSL/StylizedLitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "StylizedLit.Editor.StylizedLitShaderGUI"
}
