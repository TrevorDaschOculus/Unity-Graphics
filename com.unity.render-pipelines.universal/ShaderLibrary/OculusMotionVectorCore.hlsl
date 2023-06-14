#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 previousPositionOS : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 curPositionCS : TEXCOORD8;
    float4 prevPositionCS : TEXCOORD9;
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 TransformPrevWorldToObject(float3 positionWS)
{
    #if !defined(SHADER_STAGE_RAY_TRACING)
    return mul(GetPrevWorldToObjectMatrix(), float4(positionWS, 1.0)).xyz;
    #else
    return (float3)0;
    #endif
}

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vertexInput.positionCS;

    float3 curWS = TransformObjectToWorld(input.positionOS.xyz);
    output.curPositionCS = TransformWorldToHClip(curWS);
    if (unity_MotionVectorsParams.y == 0.0)
    {
        output.prevPositionCS = output.curPositionCS;
    }
    else
    {
        bool hasDeformation = unity_MotionVectorsParams.x > 0.0;        
        // interpolate to our next deformed position
        float3 effectivePositionOS = (hasDeformation ? (2.0 * input.positionOS.xyz - input.previousPositionOS) : input.positionOS.xyz);
        // transform to our next world position
        float3 nextWS = TransformObjectToWorld(TransformPrevWorldToObject(TransformObjectToWorld(effectivePositionOS)));
        // interpolate back to our new 'previous' position
        float3 previousWS = 2.0 * curWS - nextWS;
        output.prevPositionCS = TransformWorldToPrevHClip(previousWS);
    }

    return output;
}

half4 frag(Varyings i) : SV_Target
{
    float3 screenPos = i.curPositionCS.xyz / i.curPositionCS.w;
    float3 screenPosPrev = i.prevPositionCS.xyz / i.prevPositionCS.w;
    half4 color = (1);
    color.xyz = screenPos - screenPosPrev;
    return color;
}
