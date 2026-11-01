Shader "EchoSound/EnvironmentSurface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo / roughness", 2D) = "white" {}
        [Normal] _BumpMap ("Surface normal", 2D) = "bump" {}
        _BumpStrength ("Normal strength", Range(0,1)) = 0.3
        _WorldScale ("Repeats per metre", Float) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color;
        half _Metallic, _Smoothness, _BumpStrength;
        float _WorldScale;
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 normal = normalize(WorldNormalVector(IN,float3(0,0,1)));
            float3 weights = pow(abs(normal),8);
            weights /= max(weights.x+weights.y+weights.z,0.001);
            float3 p = IN.worldPos * max(0.01,_WorldScale);
            // World-space projection keeps the texel density consistent on stretched room primitives.
            fixed4 albedo = tex2D(_MainTex,p.zy)*weights.x
                + tex2D(_MainTex,p.xz)*weights.y + tex2D(_MainTex,p.xy)*weights.z;
            float2 uv = weights.y > 0.5 ? p.xz : (weights.x > weights.z ? p.zy : p.xy);
            half3 detailNormal = UnpackNormal(tex2D(_BumpMap,uv));
            o.Normal = normalize(lerp(half3(0,0,1),detailNormal,_BumpStrength));
            o.Albedo = albedo.rgb * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness * (1-albedo.a*0.65);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
