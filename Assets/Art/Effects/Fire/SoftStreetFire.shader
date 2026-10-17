Shader "CivilGameJam/SoftStreetFire"
{
    Properties { _FireTime ("Flow time", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireFlowNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FireTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz * 2.7;
                float2 center = floor(p / 1.5) * 1.5 + 0.75;
                float phase = FireHash(center) * 6.28;
                float2 flow = FireRollUV(p, center, sin(_FireTime * 1.3 + phase) * 2.8 + 1.4);
                float drift = FireFbm(flow + float2(_FireTime * 0.3, -_FireTime * 0.7));
                float edge = 1.0 - smoothstep(0.15, 1.0, length((input.uv - 0.5) * 2.0));
                half3 color = lerp(half3(1.0, 0.23, 0.015), half3(1.0, 0.77, 0.16), drift);
                return half4(color, input.color.a * edge * smoothstep(0.25, 0.7, drift));
            }
            ENDHLSL
        }
    }
}
