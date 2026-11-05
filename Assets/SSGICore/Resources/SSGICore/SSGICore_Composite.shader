Shader "Hidden/SSGICore/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

        SamplerState sampler_linear_clamp;
        Texture2D<float4> _SSGICore_GI;
        Texture2D<float4> _SSGICore_Opaque;
        Texture2D<float>  _SSGICore_Depth;
        Texture2D<float4> _SSGICore_Raw;
        Texture2D<float4> _SSGICore_Age;
        float4 _SSGICore_Params; // x intensity, y albedo level, z debug mode, w downsample
        float4 _SSGICore_Size;   // w, h, 1/w, 1/h

        struct Varyings { float4 positionCS : SV_POSITION; };

        Varyings Vert(uint vertexID : SV_VertexID)
        {
            Varyings o;
            float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
            o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
            return o;
        }

        bool IsSky(int2 px)
        {
            float d = _SSGICore_Depth.Load(int3(px, 0));
#if UNITY_REVERSED_Z
            return d <= 0.0;
#else
            return d >= 1.0;
#endif
        }
        ENDHLSL

        // Pass 0: additive indirect light. Albedo is approximated from scene colour chroma because Forward+
        // has no albedo buffer. Custom shaders can instead read _SSGICore_GITexture (see SSGICoreSampling.hlsl).
        Pass
        {
            Name "SSGICoreAdditive"
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings i) : SV_Target
            {
                int2 px = (int2)i.positionCS.xy;
                if (IsSky(px)) return 0;

                float3 gi = _SSGICore_GI.Load(int3(px, 0)).rgb;
                float2 uv = (px + 0.5) * _SSGICore_Size.zw;
                float3 col = _SSGICore_Opaque.SampleLevel(sampler_linear_clamp, uv, 0).rgb;
                float m = max(max(col.r, col.g), col.b);
                float3 albedo = saturate(col / max(m, 1e-3)) * _SSGICore_Params.y;
                return float4(gi * albedo * _SSGICore_Params.x, 0.0);
            }
            ENDHLSL
        }

        // Pass 1: debug views (replace the camera colour).
        Pass
        {
            Name "SSGICoreDebug"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings i) : SV_Target
            {
                int2 px = (int2)i.positionCS.xy;
                int mode = (int)(_SSGICore_Params.z + 0.5);
                int ds = max(1, (int)(_SSGICore_Params.w + 0.5));

                float3 c = 0;
                if (mode == 1)
                    c = _SSGICore_GI.Load(int3(px, 0)).rgb * _SSGICore_Params.x;
                else if (mode == 2)
                    c = _SSGICore_Raw.Load(int3(px / ds, 0)).rgb * _SSGICore_Params.x;
                else if (mode == 3)
                    c = saturate(_SSGICore_Age.Load(int3(px / ds, 0)).a / 32.0).xxx;
                return float4(c, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
