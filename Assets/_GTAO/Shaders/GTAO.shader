Shader "Hidden/GTAO_URP/GTAO"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        Pass // 0
        {
            Name "GTAO_Estimate"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex Vert
            #pragma fragment FragEstimate
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_local_fragment _ _SOURCE_DEPTH_NORMALS
            #pragma multi_compile_local_fragment _ _ORTHOGRAPHIC
            #include "GTAO.hlsl"
            ENDHLSL
        }

        Pass // 1
        {
            Name "GTAO_Temporal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex Vert
            #pragma fragment FragTemporal
            #pragma multi_compile_local_fragment _ _ORTHOGRAPHIC
            #pragma multi_compile_local_fragment _ _MOTION_VECTORS
            #include "GTAO.hlsl"
            ENDHLSL
        }

        Pass // 2
        {
            Name "GTAO_BlurH"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex Vert
            #pragma fragment FragBlurH
            #include "GTAO.hlsl"
            ENDHLSL
        }

        Pass // 3
        {
            Name "GTAO_BlurV_Resolve"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex Vert
            #pragma fragment FragResolve
            #pragma multi_compile_local_fragment _ _ORTHOGRAPHIC
            #include "GTAO.hlsl"
            ENDHLSL
        }

        Pass // 4
        {
            Name "GTAO_Composite_AfterOpaque"
            Blend Zero SrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma multi_compile_local_fragment _ _ORTHOGRAPHIC
            #include "GTAO.hlsl"
            ENDHLSL
        }

        Pass // 5
        {
            Name "GTAO_Debug"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex Vert
            #pragma fragment FragDebug
            #pragma multi_compile_local_fragment _ _ORTHOGRAPHIC
            #include "GTAO.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
