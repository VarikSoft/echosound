Shader "EchoSound/Echolocation"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _MasterBrightness ("Master Brightness", Range(0.1,3)) = 1
        _RevealTexture ("Reveal Texture", Range(0,1)) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "EcholocationCommon.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _CameraDepthNormalsTexture;
            float4x4 _FrustumCornersWS;
            float4x4 _EchoViewToWorld;
            float4 _CameraWS;
            float _MasterBrightness, _RevealTexture;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 depthUV : TEXCOORD1; float3 ray : TEXCOORD2; };
            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.depthUV = v.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) o.depthUV.y = 1 - o.depthUV.y;
                #endif
                float3 top = lerp(_FrustumCornersWS[0].xyz, _FrustumCornersWS[1].xyz, o.depthUV.x);
                float3 bottom = lerp(_FrustumCornersWS[3].xyz, _FrustumCornersWS[2].xyz, o.depthUV.x);
                o.ray = lerp(bottom, top, o.depthUV.y);
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV));
                if (depth >= _ProjectionParams.z * 0.99) return half4(0,0,0,1);
                float3 world = _CameraWS.xyz + i.ray * depth;
                float normalDepth;
                float3 viewNormal;
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.depthUV), normalDepth, viewNormal);
                float3 normal = normalize(mul((float3x3)_EchoViewToWorld, viewNormal));
                float2 texel = abs(_MainTex_TexelSize.xy);
                float dr = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV + float2(texel.x,0)));
                float dl = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV - float2(texel.x,0)));
                float du = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV + float2(0,texel.y)));
                float dd = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.depthUV - float2(0,texel.y)));
                float edge = smoothstep(0.015, 0.09, (abs(dr-dl) + abs(du-dd)) / max(1, depth));
                edge = max(edge, saturate(length(fwidth(normal)) * 2));
                float3 echo;
                float light;
                EvaluateEcho(world, normal, echo, light);
                float grid = EchoGrid(world, normal);
                float3 scene = tex2D(_MainTex, i.uv).rgb;
                float luminance = dot(scene, float3(0.2126, 0.7152, 0.0722));
                float fresnel = EchoFresnel(world, normal);
                float surface = _EchoSurfaceTint * lerp(1, 0.6 + luminance * 0.4, _RevealTexture);
                float3 result = echo * (surface + fresnel * _EchoFresnelIntensity + grid * _EchoGridIntensity + edge * _EchoEdgeIntensity);
                // Gentle exposure rolloff preserves color when several steps overlap.
                result = 1 - exp(-result * _MasterBrightness * 2.2);
                float vignette = 1 - smoothstep(0.25, 0.8, length(i.uv - 0.5)) * 0.28;
                return half4(result * vignette, 1);
            }
            ENDCG
        }
        // Small separable glow, restricted to bright echo highlights.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float2 _BlurDirection;
            float _ExtractGlow;
            half3 ReadGlow(float2 uv)
            {
                half3 c = tex2D(_MainTex, uv).rgb;
                return c * lerp(1, smoothstep(0.2,0.65,max(c.r,max(c.g,c.b))), _ExtractGlow);
            }
            half4 frag(v2f_img i) : SV_Target
            {
                float2 stepUV = _MainTex_TexelSize.xy * _BlurDirection;
                half3 color = ReadGlow(i.uv) * 0.4;
                color += (ReadGlow(i.uv + stepUV * 1.5) + ReadGlow(i.uv - stepUV * 1.5)) * 0.24;
                color += (ReadGlow(i.uv + stepUV * 3.5) + ReadGlow(i.uv - stepUV * 3.5)) * 0.06;
                return half4(color,1);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _EchoBloom;
            float _BloomStrength;
            half4 frag(v2f_img i) : SV_Target
            {
                half3 scene = tex2D(_MainTex,i.uv).rgb;
                half3 glow = tex2D(_EchoBloom,i.uv).rgb * _BloomStrength;
                return half4(scene + glow * (1 - scene),1);
            }
            ENDCG
        }
    }
    FallBack Off
}
