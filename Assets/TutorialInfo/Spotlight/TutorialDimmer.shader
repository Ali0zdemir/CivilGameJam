Shader "CivilGameJam/UI/Tutorial Dimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _ActiveMask ("Active silhouettes", Float) = 0
        _ShowPeoplePanel ("Highlight authored section or blueprint", Float) = 0
        _VisibleProgress ("Visible working progress bars", Float) = 0
        _Focus0Mask ("Research silhouette", 2D) = "black" {}
        _Focus1Mask ("Factory silhouette", 2D) = "black" {}
        _Focus2Mask ("School silhouette", 2D) = "black" {}
        _Focus3Mask ("AP silhouette", 2D) = "black" {}
        _Focus4Mask ("Person silhouette", 2D) = "black" {}
        _Focus5Mask ("House one silhouette", 2D) = "black" {}
        _Focus6Mask ("House two silhouette", 2D) = "black" {}
        _Focus7Mask ("House three silhouette", 2D) = "black" {}
        _Focus8Mask ("House four silhouette", 2D) = "black" {}
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
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 local:TEXCOORD0; };
            fixed4 _Color;
            float _ActiveMask;
            float _ShowPeoplePanel;
            float _VisibleProgress;
            float4 _Progress0X, _Progress0Y, _Progress1X, _Progress1Y, _Progress2X, _Progress2Y;
            float4 _PeopleX, _PeopleY;
            sampler2D _Focus0Mask, _Focus1Mask, _Focus2Mask, _Focus3Mask, _Focus4Mask;
            sampler2D _Focus5Mask, _Focus6Mask, _Focus7Mask, _Focus8Mask;
            float4 _Focus0X, _Focus0Y, _Focus1X, _Focus1Y, _Focus2X, _Focus2Y;
            float4 _Focus3X, _Focus3Y, _Focus4X, _Focus4Y, _ClipRect;
            float4 _Focus5X, _Focus5Y, _Focus6X, _Focus6Y, _Focus7X, _Focus7Y, _Focus8X, _Focus8Y;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xy; o.color=v.color*_Color; return o;
            }
            float Mask(sampler2D image, float4 x, float4 y, float2 position)
            {
                float2 uv=float2(dot(float3(position,1),x.xyw),dot(float3(position,1),y.xyw));
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                return tex2D(image,uv).a*inside;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float light=0;
                light=max(light,Mask(_Focus0Mask,_Focus0X,_Focus0Y,i.local)*fmod(floor(_ActiveMask),2));
                light=max(light,Mask(_Focus1Mask,_Focus1X,_Focus1Y,i.local)*fmod(floor(_ActiveMask/2),2));
                light=max(light,Mask(_Focus2Mask,_Focus2X,_Focus2Y,i.local)*fmod(floor(_ActiveMask/4),2));
                light=max(light,Mask(_Focus3Mask,_Focus3X,_Focus3Y,i.local)*fmod(floor(_ActiveMask/8),2));
                light=max(light,Mask(_Focus4Mask,_Focus4X,_Focus4Y,i.local)*fmod(floor(_ActiveMask/16),2));
                light=max(light,Mask(_Focus5Mask,_Focus5X,_Focus5Y,i.local)*fmod(floor(_ActiveMask/32),2));
                light=max(light,Mask(_Focus6Mask,_Focus6X,_Focus6Y,i.local)*fmod(floor(_ActiveMask/64),2));
                light=max(light,Mask(_Focus7Mask,_Focus7X,_Focus7Y,i.local)*fmod(floor(_ActiveMask/128),2));
                light=max(light,Mask(_Focus8Mask,_Focus8X,_Focus8Y,i.local)*fmod(floor(_ActiveMask/256),2));
                float2 peopleUV=float2(dot(float3(i.local,1),_PeopleX.xyw),dot(float3(i.local,1),_PeopleY.xyw));
                float insidePeople=step(0,peopleUV.x)*step(peopleUV.x,1)*step(0,peopleUV.y)*step(peopleUV.y,1)*_ShowPeoplePanel;
                light=max(light,insidePeople);
                float2 progress0=float2(dot(float3(i.local,1),_Progress0X.xyw),dot(float3(i.local,1),_Progress0Y.xyw));
                float2 progress1=float2(dot(float3(i.local,1),_Progress1X.xyw),dot(float3(i.local,1),_Progress1Y.xyw));
                float2 progress2=float2(dot(float3(i.local,1),_Progress2X.xyw),dot(float3(i.local,1),_Progress2Y.xyw));
                light=max(light,step(0,progress0.x)*step(progress0.x,1)*step(0,progress0.y)*step(progress0.y,1)*fmod(floor(_VisibleProgress),2));
                light=max(light,step(0,progress1.x)*step(progress1.x,1)*step(0,progress1.y)*step(progress1.y,1)*fmod(floor(_VisibleProgress/2),2));
                light=max(light,step(0,progress2.x)*step(progress2.x,1)*step(0,progress2.y)*step(progress2.y,1)*fmod(floor(_VisibleProgress/4),2));
                fixed4 color=i.color; color.a*=1-saturate(light);
                // Pulse along the current authored section or blueprint boundary.
                float2 edgeUV=max(fwidth(peopleUV)*2,float2(.00001,.00001));
                float2 inner=step(edgeUV,peopleUV)*step(peopleUV,1-edgeUV);
                float rim=insidePeople*(1-inner.x*inner.y);
                color.rgb=lerp(color.rgb,float3(.957,.773,.475),rim);
                color.a=max(color.a,rim*(.72+.28*sin(_Time.y*4))*i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local,_ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
