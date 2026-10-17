Shader "CivilGameJam/GasFlame"
{
    Properties
    {
        _FireTime ("Flow time", Float) = 0
        _Opacity ("Gas density", Range(0, 1)) = 0.7
        _DepthFade ("Soft intersection", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "FireFlowNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FireTime;
                float _Opacity;
                float _DepthFade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float4 uvAgeSeed : TEXCOORD0; float3 center : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float4 uvAgeSeed : TEXCOORD0; float4 screenPosition : TEXCOORD1; float eyeDepth : TEXCOORD2; float phase : TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world); output.screenPosition = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(world).z;
                output.color = input.color; output.uvAgeSeed = input.uvAgeSeed;
                output.phase = input.uvAgeSeed.w * 37.0 + dot(input.center, float3(0.73, 1.31, 0.91));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uvAgeSeed.xy * 2.0 - 1.0;
                float age = saturate(input.uvAgeSeed.z); float time = _FireTime * 1.25 + input.phase;
                float2 flow = FireRollUV(uv, float2(0.24, 0.12), sin(time * 0.8) * 2.6 + 2.2);
                flow = FireRollUV(flow, float2(-0.24, 0.32), -cos(time * 0.67) * 2.1 - 1.7);
                float2 advected = flow * float2(2.5, 2.0) + float2(input.phase, -_FireTime * 1.8 - age * 2.0);
                float noise = FireFbm(advected + (FireFbm(advected * 0.7 + time * 0.1) - 0.5) * 1.3);
                float envelope = pow(saturate(1.0 - dot(uv * float2(0.96, 0.86), uv * float2(0.96, 0.86))), 1.5);
                float density = envelope * smoothstep(0.24, 0.7, noise);
                float intersection = 1.0;
                float rawDepth = SampleSceneDepth(input.screenPosition.xy / input.screenPosition.w);
                if (rawDepth > 0.00001 && _DepthFade > 0.001)
                {
                    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    #if UNITY_REVERSED_Z
                    float orthoDepth = lerp(_ProjectionParams.z, _ProjectionParams.y, rawDepth);
                    #else
                    float orthoDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                    #endif
                    sceneDepth = lerp(sceneDepth, orthoDepth, unity_OrthoParams.w);
                    intersection = saturate((sceneDepth - input.eyeDepth) / _DepthFade);
                }
                float alpha = density * input.color.a * _Opacity * intersection;
                half3 color = lerp(half3(1.0, 0.14, 0.008), half3(1.16, 0.84, 0.2), saturate(noise * 1.25));
                color *= lerp(half3(1, 0.72, 0.48), half3(1, 1, 1), input.color.g);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
