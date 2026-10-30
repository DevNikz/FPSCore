Shader "GridMaster/Lit"
{
    Properties
    {
        [Header(Surface)]
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2

        [Header(Projection)]
        [Enum(WorldTriplanar, 0, ObjectUV, 1)] _Projection ("Projection", Float) = 0
        _CellSize ("Cell Size (world units)", Float) = 1
        _ObjectUVTiling ("Object UV Tiling", Float) = 1
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 16)) = 4

        [Header(Axis and Face Tint)]
        [Enum(Off, 0, PerAxis, 1, PerFace, 2)] _TintMode ("Tint Mode", Float) = 0
        _TintStrength ("Tint Strength", Range(0, 1)) = 1
        _TintSharpness ("Tint Blend Sharpness", Range(1, 32)) = 8
        _TintPosX ("Tint +X", Color) = (0.95, 0.6, 0.6, 1)
        _TintNegX ("Tint -X", Color) = (0.75, 0.45, 0.45, 1)
        _TintPosY ("Tint +Y", Color) = (0.65, 0.95, 0.65, 1)
        _TintNegY ("Tint -Y", Color) = (0.45, 0.75, 0.45, 1)
        _TintPosZ ("Tint +Z", Color) = (0.6, 0.7, 1.0, 1)
        _TintNegZ ("Tint -Z", Color) = (0.45, 0.55, 0.8, 1)

        [Header(Grid Lines)]
        _LineColor ("Minor Line Color", Color) = (0.15, 0.15, 0.15, 1)
        _MajorLineColor ("Major Line Color", Color) = (0.05, 0.05, 0.05, 1)
        _MinorLineWidth ("Minor Line Width (fraction of cell)", Range(0.001, 0.25)) = 0.02
        _MajorLineWidth ("Major Line Width (fraction of cell)", Range(0.001, 0.5)) = 0.05
        [IntRange] _MajorInterval ("Major Line Every N Cells", Range(1, 20)) = 5
        _LineSoftness ("Line Softness (AA)", Range(0.5, 3)) = 1

        [Header(Distance Fade)]
        [Toggle] _DistanceFade ("Enable Distance Fade", Float) = 1
        _FadeStart ("Minor Fade Start (m)", Float) = 30
        _FadeEnd ("Minor Fade End (m)", Float) = 100
        _MajorFadeMultiplier ("Major Fade Distance Multiplier", Range(1, 10)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex ForwardPassVertex
            #pragma fragment ForwardPassFragment

            // Material keywords
            #pragma shader_feature_local _PROJECTION_OBJECT
            #pragma shader_feature_local _ _TINT_AXIS _TINT_FACE

            // URP keywords (Forward+ uses _CLUSTER_LIGHT_LOOP; _FORWARD_PLUS is deprecated since 6.1)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Includes/GridMasterForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Includes/GridMasterShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing

            #include "Includes/GridMasterDepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Includes/GridMasterDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "GridMaster.EditorTools.GridMasterShaderGUI"
}
