#ifndef GRIDMASTER_FORWARD_PASS_INCLUDED
#define GRIDMASTER_FORWARD_PASS_INCLUDED

#include "GridMasterLighting.hlsl"
#include "GridMasterProjection.hlsl"
#include "GridMasterPattern.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half fogFactor : TEXCOORD3;
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLight : TEXCOORD4;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ForwardPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.uv = input.texcoord;
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
#endif
    return output;
}

half4 ForwardPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    GridUV grid = GetGridUV(input.positionWS, normalWS, input.uv);

    half3 albedo = GetGridAlbedo(grid, input.positionWS, normalWS);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLight = input.vertexLight;
#else
    half3 vertexLight = half3(0, 0, 0);
#endif

    InputData inputData;
    InitializeGridInputData(input.positionWS, normalWS, input.positionCS, input.fogFactor, vertexLight, inputData);
    SurfaceData surfaceData = GetGridSurfaceData(albedo);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0h;
    return color;
}

#endif
