Shader "Hidden/PickupOutline"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float4x4 _PickupOutlineView;
    float4x4 _PickupOutlineVP;

    struct MaskAttributes { float3 positionOS : POSITION; };
    struct MaskVaryings
    {
        float4 positionCS : SV_POSITION;
        float eyeDepth : TEXCOORD0;
    };

    MaskVaryings MaskVert(MaskAttributes i)
    {
        MaskVaryings o;
        float3 wp = TransformObjectToWorld(i.positionOS);
        o.positionCS = mul(_PickupOutlineVP, float4(wp, 1.0));
        o.eyeDepth = -mul(_PickupOutlineView, float4(wp, 1.0)).z;
        return o;
    }

    float4 MaskOut(MaskVaryings i, float colorClass)
    {
        return float4(rcp(max(i.eyeDepth, 1e-3)), colorClass, 0, 0);
    }
    float4 MaskFragDefault(MaskVaryings i) : SV_Target { return MaskOut(i, 0); }
    float4 MaskFragValid(MaskVaryings i) : SV_Target { return MaskOut(i, 1); }
    float4 MaskFragInvalid(MaskVaryings i) : SV_Target { return MaskOut(i, 2); }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Mask"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFragDefault
            ENDHLSL
        }

        Pass
        {
            Name "MaskGhostValid"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFragValid
            ENDHLSL
        }

        Pass
        {
            Name "MaskGhostInvalid"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFragInvalid
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;       
            float4 _GhostValidColor;
            float4 _GhostInvalidColor;
            float4 _OutlineParams;      
            float4 _OutlineParams2;     
            float4 _PickupOutlineTexel; 

            static const int DIRS = 16;
            static const int RINGS = 3;

            float EyeDepth(float raw)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            float4 CompositeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                if (SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r > 0.0)
                    return 0;

                float thickness = max(_OutlineParams.x, 0.01);
                float hits = 0.0;
                float nearestInv = 0.0;
                float colorClass = 0.0;

                [unroll] for (int ring = 1; ring <= RINGS; ring++)
                {
                    float r = thickness * ring / RINGS;
                    [unroll] for (int d = 0; d < DIRS; d++)
                    {
                        float a = (d + 0.5 * ring) * (6.2831853 / DIRS);
                        float s, c;
                        sincos(a, s, c);
                        float2 o = float2(c, s) * r * _PickupOutlineTexel.xy;
                        float2 m = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + o, 0).rg;
                        if (m.r > 0.0)
                        {
                            hits += 1.0;
                            nearestInv = max(nearestInv, m.r);
                            colorClass = max(colorClass, m.g);
                        }
                    }
                }

                float coverage = hits / (DIRS * RINGS);
                if (coverage <= 0.0)
                    return 0;

                // threshold: min neighbour coverage to draw. softness: falloff width above it.
                float alpha = smoothstep(_OutlineParams.y, _OutlineParams.y + max(_OutlineParams.z, 1e-3), coverage);

                float4 color = _OutlineColor;
                if (colorClass > 1.5)
                    color = _GhostInvalidColor;
                else if (colorClass > 0.5)
                    color = _GhostValidColor;
                else
                {
                    // Pickups hide behind scene geometry; ghosts always show.
                    float objEye = rcp(nearestInv);
                    float sceneEye = EyeDepth(SampleSceneDepth(uv));
                    float occluded = sceneEye < objEye - _OutlineParams2.x ? 1.0 : 0.0;
                    alpha *= lerp(1.0, _OutlineParams.w, occluded);
                }

                return float4(color.rgb, color.a * alpha);
            }
            ENDHLSL
        }
    }
}
