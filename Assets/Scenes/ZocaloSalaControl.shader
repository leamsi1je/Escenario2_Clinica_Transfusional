Shader "Transfusion/Sala de control/Zocalo"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0, 0.387, 0.526, 1)
        _DoorCenter ("Centro del hueco", Vector) = (-6.6604, 0, -2.1406, 0)
        _DoorHalfSize ("Mitad del hueco en X y Z", Vector) = (0.32, 0, 0.57, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _DoorCenter;
                float4 _DoorHalfSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 distanceToEdge = abs(input.positionWS.xz - _DoorCenter.xz) - _DoorHalfSize.xz;
                clip(max(distanceToEdge.x, distanceToEdge.y));

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 light = SampleSH(normalWS) +
                    LightingLambert(mainLight.color, mainLight.direction, normalWS) * mainLight.distanceAttenuation;
                return half4(_BaseColor.rgb * light, 1);
            }
            ENDHLSL
        }
    }
}
