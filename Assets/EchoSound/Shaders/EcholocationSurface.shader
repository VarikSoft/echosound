Shader "EchoSound/EcholocationSurface"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1,1,1,1)
        _Emission ("Emission Intensity", Range(0,2)) = 0.5
        _AcousticSurfaceResponse ("Acoustic reflection / persistence", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "EcholocationCommon.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST, _Color;
            float _Emission;
            float4 _AcousticSurfaceResponse;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float3 normal:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv,_MainTex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float3 normal = normalize(i.normal), echo;
                float3 contact;
                EvaluateEchoResponse(i.world,normal,_AcousticSurfaceResponse.xy,echo,contact);
                float grid = EchoGrid(i.world,normal);
                float3 baseColor = tex2D(_MainTex,i.uv).rgb * _Color.rgb;
                float materialDetail = 0.55 + dot(baseColor, float3(0.2126,0.7152,0.0722)) * 0.45;
                float fresnel = EchoFresnel(i.world,normal);
                float corners = saturate(length(fwidth(normal)) * 2);
                float3 result = echo * (materialDetail * _EchoSurfaceTint + fresnel * _EchoFresnelIntensity
                    + corners * _EchoEdgeIntensity + grid * _EchoGridIntensity) * EchoFloorVisibility(i.world,normal);
                result += contact * (0.1+corners*2.5+fresnel*0.65) * lerp(1,0.08,EchoFloorMask(i.world,normal));
                result = EchoMaterialCue(result,i.world,normal,_AcousticSurfaceResponse);
                result *= EchoCeilingVisibility(i.world,normal);
                result *= _Emission * 2;
                return half4(1 - exp(-result * 1.6),1);
            }
            ENDCG
        }
        // Needed by camera depth and depth-normal textures.
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
