Shader "EchoSound/EcholocationSurface"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission Intensity", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Emission;

            // Global Echolocation parameters from EcholocationManager
            int _EchoPulseCount;
            float4 _EchoPulseOrigins[16];
            float4 _EchoPulseParams[16];
            float4 _EchoPulseColors[16];
            float _EchoGridScale;
            float _EchoGridIntensity;
            float _EchoAmbientBrightness;
            float4 _EchoVoidColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                // 3D world grid overlay
                float3 gridCoord = abs(i.worldPos * _EchoGridScale);
                float3 gridDeriv = fwidth(gridCoord);
                float3 gridLines = abs(frac(gridCoord - 0.5) - 0.5) / (gridDeriv + 0.001);
                float gridMask = 1.0 - saturate(min(min(gridLines.x, gridLines.y), gridLines.z));

                // Multi-pulse wave evaluation
                float3 echoColorAcc = float3(0, 0, 0);
                float echoIntensityAcc = 0.0;

                int count = min(_EchoPulseCount, 16);
                for (int p = 0; p < count; p++)
                {
                    float3 pulseOrigin = _EchoPulseOrigins[p].xyz;
                    float maxRadius = _EchoPulseOrigins[p].w;
                    float startTime = _EchoPulseParams[p].x;
                    float speed = _EchoPulseParams[p].y;
                    float intensity = _EchoPulseParams[p].z;
                    float decayDuration = _EchoPulseParams[p].w;
                    float waveWidth = max(0.05, _EchoPulseColors[p].a);

                    float dist = distance(i.worldPos, pulseOrigin);
                    if (dist > maxRadius) continue;

                    float elapsed = _Time.y - startTime;
                    if (elapsed <= 0.0) continue;

                    float currentRadius = speed * elapsed;
                    if (currentRadius - waveWidth > maxRadius && dist > currentRadius) continue;

                    float edgeDelta = abs(dist - currentRadius);
                    float waveFront = saturate(1.0 - (edgeDelta / waveWidth));
                    waveFront = waveFront * waveFront * (3.0 - 2.0 * waveFront);

                    float trail = 0.0;
                    if (dist < currentRadius)
                    {
                        float trailElapsed = (currentRadius - dist) / speed;
                        trail = saturate(1.0 - (trailElapsed / decayDuration));
                        trail = trail * trail;
                    }

                    float distAtten = saturate(1.0 - (dist / maxRadius));
                    float contrib = (waveFront * 2.5 + trail * 0.8) * intensity * distAtten;

                    echoColorAcc += _EchoPulseColors[p].rgb * contrib;
                    echoIntensityAcc += contrib;
                }

                float totalIllum = saturate(echoIntensityAcc + _EchoAmbientBrightness);
                float3 tint = (echoIntensityAcc > 0.02)
                    ? (echoColorAcc / max(0.001, echoIntensityAcc))
                    : float3(0.2, 0.4, 0.6);

                float3 litAlbedo = albedo.rgb * totalIllum;
                float3 gridGlow = tint * (gridMask * _EchoGridIntensity * totalIllum);
                float3 waveGlow = echoColorAcc * _Emission;

                float3 finalColor = litAlbedo + gridGlow + waveGlow;
                return fixed4(finalColor, albedo.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
