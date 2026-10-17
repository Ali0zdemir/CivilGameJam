Shader "CivilGameJam/SprinklerWater"
{
    Properties
    {
        _DeepColor ("Water tint", Color) = (0.09, 0.3, 0.38, 1)
        _ShallowColor ("Water highlights", Color) = (0.42, 0.8, 0.92, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.88
        _FlowTime ("Flow time", Float) = 0
        _SurfaceMode ("Surface film", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                float _Opacity;
                float _FlowTime;
                float _SurfaceMode;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS; output.positionWS = vertex.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color; output.uv = input.uv;
                output.fog = ComputeFogFactor(vertex.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flow = float2(_SurfaceMode > .5 ? 0.0 : .25, 2.8);
                float2 p = input.uv * float2(19.0, 15.0) - flow * _FlowTime;
                float noise = Noise(p + (Noise(p * .43) - .5) * 2.0);
                float2 slope = float2(Noise(p + float2(.2, 0)) - Noise(p - float2(.2, 0)),
                    Noise(p + float2(0, .2)) - Noise(p - float2(0, .2)));
                float3 normal = normalize(input.normalWS);
                float3 tangent = abs(normal.y) > .8 ? float3(1, 0, 0) : normalize(cross(float3(0, 1, 0), normal));
                float3 bitangent = normalize(cross(normal, tangent));
                normal = normalize(normal + (tangent * slope.x + bitangent * slope.y) * .28);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                float fresnel = pow(1.0 - saturate(dot(normal, view)), 4.0);
                float sparkle = pow(saturate(dot(normal, normalize(view + sun.direction))), 48.0);
                float diffuse = .76 + .24 * saturate(dot(normal, sun.direction));
                float3 tint = lerp(_DeepColor.rgb, _ShallowColor.rgb, .35 + fresnel * .6);
                float alpha;
                if (_SurfaceMode > .5)
                {
                    float fill = saturate(input.color.r);
                    float coverage = smoothstep(.015, .07, fill * (.72 + .28 * noise));
                    float foam = smoothstep(.81, .97, noise) * saturate(input.color.b) * .18;
                    float sheet = smoothstep(.18, .8, noise);
                    float curl = pow(saturate(1.0 - abs(noise * 2.0 - 1.0)), 12.0);
                    alpha = _Opacity * (coverage * (.075 + .32 * sheet) + input.color.g * .025);
                    alpha += coverage * (curl * .12 + sparkle * .16 + fresnel * .08);
                    tint = lerp(_DeepColor.rgb, _ShallowColor.rgb, .3 + sheet * .45 + fresnel * .25) * diffuse;
                    tint += coverage * (curl * .42 + sparkle * .7 + fresnel * .18 + foam);
                }
                else
                {
                    alpha = input.color.a * _Opacity * (.72 + .28 * noise);
                    tint = tint * diffuse + sparkle * .6 + fresnel * .22;
                }
                clip(alpha - .004);
                return half4(MixFog(tint, input.fog), min(alpha, .7));
            }
            ENDHLSL
        }
    }
}
