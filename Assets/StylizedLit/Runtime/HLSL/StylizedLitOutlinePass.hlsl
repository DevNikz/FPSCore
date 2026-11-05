// StylizedLitOutlinePass.hlsl  (Phase 4)
// Inverted-hull outline: back faces extruded along the vertex normal.
// Width: _OutlineDistanceScaling 0 = centimeters in world space, 1 = pixels on screen (blend in between).
// A disabled outline (_OutlineEnabled = 0) is clipped in the vertex stage; the GUI also turns the pass off.
#ifndef STYLIZED_LIT_OUTLINE_PASS_INCLUDED
#define STYLIZED_LIT_OUTLINE_PASS_INCLUDED

#include "Assets/StylizedLit/Runtime/HLSL/StylizedCommon.hlsl"

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
#if defined(STYLIZED_CLIP_REQUIRES_UV)
    float2 uv         : TEXCOORD0;
#endif
    half fogFactor    : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings StylizedOutlineVertex(OutlineAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // World-space size of one screen pixel at this vertex.
    float worldPerPixel = (unity_OrthoParams.w > 0.5)
        ? 2.0 * unity_OrthoParams.y / _ScreenParams.y
        : 2.0 * distance(GetCameraPositionWS(), positionWS) / (unity_CameraProjection._m11 * _ScreenParams.y);

    float width = _OutlineWidth * lerp(0.01, worldPerPixel, _OutlineDistanceScaling);
    positionWS += normalWS * width;

    output.positionCS = TransformWorldToHClip(positionWS);
    if (_OutlineEnabled < 0.5h)
        output.positionCS = float4(2.0, 2.0, 2.0, 1.0); // outside the clip volume

#if defined(STYLIZED_CLIP_REQUIRES_UV)
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

half4 StylizedOutlineFragment(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(STYLIZED_CLIP_REQUIRES_UV)
    ApplyStylizedClip(input.uv);
#endif

    half3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
    return half4(color, 1.0h);
}

#endif // STYLIZED_LIT_OUTLINE_PASS_INCLUDED
