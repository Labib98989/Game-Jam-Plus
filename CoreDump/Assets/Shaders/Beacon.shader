// A soft glow drawn on top of everything, walls included: brightest where the sphere faces the
// camera, fading to nothing at its edges. Additive, so it lights up whatever is behind it. The
// lost head wears one so you can find it from anywhere in the room.
Shader "CoreDump/Beacon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.45, 0.15, 1)
        _Falloff ("Edge falloff", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beacon"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Always
            ZWrite Off
            Cull Back
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 toCamera = normalize(GetCameraPositionWS() - input.positionWS);
                half facing = saturate(dot(normalize(input.normalWS), toCamera));
                return half4(_BaseColor.rgb * pow(facing, _Falloff) * _BaseColor.a, 1);
            }
            ENDHLSL
        }
    }
}
