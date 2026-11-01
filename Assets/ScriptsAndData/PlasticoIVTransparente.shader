Shader "Clinica/PlasticoIVTransparente"
{
    Properties
    {
        _Color ("Plástico", Color) = (0.90, 0.97, 1.0, 0.055)
        _Borde ("Visibilidad del borde", Range(0, 0.6)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardPlasticoIV"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewDirectionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Borde;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs posicion =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = posicion.positionCS;
                output.normalWS = NormalizeNormalPerVertex(normal.normalWS);
                output.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(posicion.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal = normalize(input.normalWS);
                half3 vista = normalize(input.viewDirectionWS);
                half fresnel = pow(1.0h - saturate(dot(normal, vista)), 3.5h);
                half4 color = _Color;
                color.rgb += fresnel * _Borde;
                color.a = saturate(_Color.a + fresnel * _Borde);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
