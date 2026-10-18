Shader "CivilGameJam/UI/Tutorial Contour"
{
    Properties
    {
        [PerRendererData] _MainTex ("Silhouette", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ThicknessUV ("Contour thickness", Vector) = (.004,.004,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ThicknessUV, _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.local=v.vertex.xy; o.color=v.color*_Color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 d=_ThicknessUV.xy;
                float center=step(.1,tex2D(_MainTex,i.uv).a), edge=0;
                edge=max(edge,tex2D(_MainTex,i.uv+float2(d.x,0)).a);
                edge=max(edge,tex2D(_MainTex,i.uv-float2(d.x,0)).a);
                edge=max(edge,tex2D(_MainTex,i.uv+float2(0,d.y)).a);
                edge=max(edge,tex2D(_MainTex,i.uv-float2(0,d.y)).a);
                edge=max(edge,tex2D(_MainTex,i.uv+d*.7071).a);
                edge=max(edge,tex2D(_MainTex,i.uv-d*.7071).a);
                edge=max(edge,tex2D(_MainTex,i.uv+float2(d.x,-d.y)*.7071).a);
                edge=max(edge,tex2D(_MainTex,i.uv+float2(-d.x,d.y)*.7071).a);
                fixed4 color=i.color; color.a*=step(.1,edge)*(1-center);
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local,_ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
