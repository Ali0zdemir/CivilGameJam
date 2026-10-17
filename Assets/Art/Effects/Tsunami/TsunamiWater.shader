Shader "CivilGameJam/TsunamiWater"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.045, 0.31, 0.44, 1)
        _ShallowColor ("Lit water", Color) = (0.12, 0.62, 0.72, 1)
        _FoamColor ("Foam", Color) = (0.88, 0.97, 0.95, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Fade ("Visibility", Range(0, 1)) = 1
        _FacetedLighting ("Faceted lighting", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "TsunamiWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _Opacity;
                float _Fade;
                float _FacetedLighting;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS;
                output.positionWS = vertex.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(vertex.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                Light sun = GetMainLight();
                float lightAmount = saturate(dot(normal, sun.direction));
                float diffuse = 0.65 + lerp(lightAmount * 0.35, floor(lightAmount * 4.0) * 0.0875, _FacetedLighting);
                half3 water = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(input.color.g * 0.8 + normal.y * 0.2));
                water *= diffuse;
                float foam = step(0.72, input.color.r);
                half3 color = lerp(water, _FoamColor.rgb * (0.82 + diffuse * 0.18), foam);
                color = MixFog(color, input.fog);
                clip(input.color.b - 0.15);
                half alpha = saturate(_Opacity * _Fade * input.color.a);
                clip(alpha - 0.003);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
