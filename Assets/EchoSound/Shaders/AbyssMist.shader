Shader "EchoSound/AbyssMist"
{
    Properties
    {
        _Tint ("Editor preview tint", Color) = (0.22,0.28,0.3,1)
        _CloudTex ("Soft cloud density", 2D) = "white" {}
        _Opacity ("Opacity", Range(0,1)) = 0.6
        _InspectionPreview ("Editor preview", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "EcholocationCommon.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Tint;
            sampler2D _CloudTex;
            float _Opacity, _InspectionPreview;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float4 screen:TEXCOORD2; float eye:TEXCOORD3; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.vertex);
                o.eye=-UnityObjectToViewPos(v.vertex).z;return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float2 uv=(i.uv-0.5)*2;
                float radial=saturate(1-dot(uv,uv));
                float3 p=i.world*0.4+float3(_Time.y*0.025,0,_Time.y*0.018);
                float noise=EchoNoise(p)*0.7+EchoNoise(p*2.07)*0.3;
                float cloud=tex2D(_CloudTex,i.uv).r*lerp(0.5,1,noise)*smoothstep(0,0.2,radial);
                float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float soft=saturate((depth-i.eye)*1.5);
                if (_InspectionPreview > 0) soft=1;
                float3 memory,contact;
                EvaluateEchoResponse(i.world,float3(0,1,0),float2(1,1),memory,contact);
                float3 echo=memory+contact*0.15;
                float strength=max(echo.r,max(echo.g,echo.b));
                float reveal=max(saturate(strength*3),_InspectionPreview);
                // Preserve the local pulse color, including overlapping echoes, without adding a neutral tint.
                float3 tint=echo/max(strength,0.001)*0.22;
                if (_InspectionPreview > 0)
                    tint=lerp(_Tint.rgb,tint,saturate(strength*3/_InspectionPreview));
                return half4(tint, min(0.3,cloud*soft*i.color.a*_Opacity*reveal));
            }
            ENDCG
        }
    }
}
