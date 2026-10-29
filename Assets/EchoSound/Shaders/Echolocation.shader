Shader "EchoSound/Echolocation"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _MasterBrightness ("Master Brightness", Range(0.1, 3.0)) = 1.0
        _RevealTexture ("Reveal Original Texture", Range(0.0, 1.0)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthTexture;
            sampler2D _CameraDepthNormalsTexture;

            // Camera frustum parameters
            float4x4 _FrustumCornersWS;
            float4 _CameraWS;
            float _MasterBrightness;
            float _RevealTexture;

            // Global Echolocation parameters from EcholocationManager
            int _EchoPulseCount;
            float4 _EchoPulseOrigins[16]; // xyz: origin, w: maxRadius
            float4 _EchoPulseParams[16];  // x: startTime, y: speed, z: intensity, w: decayDuration
            float4 _EchoPulseColors[16];  // rgb: color, a: waveWidth

            float _EchoWaveWidth;
            float _EchoGridScale;
            float _EchoGridIntensity;
            float _EchoEdgeIntensity;
            float _EchoAmbientBrightness;
            float4 _EchoVoidColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 interpolatedRay : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                // Bilinear interpolation across the 4 camera frustum corners
                float3 top = lerp(_FrustumCornersWS[0].xyz, _FrustumCornersWS[1].xyz, v.uv.x);
                float3 bottom = lerp(_FrustumCornersWS[3].xyz, _FrustumCornersWS[2].xyz, v.uv.x);
                o.interpolatedRay = lerp(bottom, top, v.uv.y);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float eyeDepth = LinearEyeDepth(rawDepth);

                // If background void / sky
                if (eyeDepth >= _ProjectionParams.z * 0.99)
                {
                    return _EchoVoidColor;
                }

                // Reconstruct world space position from camera ray and depth
                float3 worldPos = _CameraWS.xyz + i.interpolatedRay * eyeDepth;

                // 1. Edge detection using depth cross-sampling
                float2 texel = _MainTex_TexelSize.xy * 1.5;
                float dR = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv + float2(texel.x, 0)));
                float dL = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv - float2(texel.x, 0)));
                float dU = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv + float2(0, texel.y)));
                float dD = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv - float2(0, texel.y)));

                float depthDiff = abs(dR - dL) + abs(dU - dD);
                float edge = saturate(depthDiff / (eyeDepth * 0.04 + 0.08));

                // 2. World-space 3D grid lines (1m intervals)
                float3 gridCoord = abs(worldPos * _EchoGridScale);
                float3 gridDeriv = fwidth(gridCoord);
                float3 gridLines = abs(frac(gridCoord - 0.5) - 0.5) / (gridDeriv + 0.001);
                float minGridDist = min(min(gridLines.x, gridLines.y), gridLines.z);
                float gridMask = 1.0 - saturate(minGridDist);

                // 3. Multi-pulse wave evaluation
                float3 echoColorAcc = float3(0, 0, 0);
                float echoIntensityAcc = 0.0;

                int count = min(_EchoPulseCount, 16);
                for (int p = 0; p < count; p++)
                {
                    float4 originData = _EchoPulseOrigins[p];
                    float4 paramData = _EchoPulseParams[p];
                    float4 colorData = _EchoPulseColors[p];

                    float3 pulseOrigin = originData.xyz;
                    float maxRadius = originData.w;
                    float startTime = paramData.x;
                    float speed = paramData.y;
                    float intensity = paramData.z;
                    float decayDuration = paramData.w;
                    float waveWidth = max(0.05, colorData.a);

                    float dist = distance(worldPos, pulseOrigin);
                    if (dist > maxRadius) continue;

                    float elapsed = _Time.y - startTime;
                    if (elapsed <= 0.0) continue;

                    float currentRadius = speed * elapsed;
                    if (currentRadius - waveWidth > maxRadius && dist > currentRadius) continue;

                    // Glowing wavefront edge
                    float edgeDelta = abs(dist - currentRadius);
                    float waveFront = saturate(1.0 - (edgeDelta / waveWidth));
                    waveFront = waveFront * waveFront * (3.0 - 2.0 * waveFront);

                    // Fading decay trail behind the wave
                    float trail = 0.0;
                    if (dist < currentRadius)
                    {
                        float trailElapsed = (currentRadius - dist) / speed;
                        float trailProgress = saturate(trailElapsed / decayDuration);
                        trail = saturate(1.0 - trailProgress);
                        trail = trail * trail;
                    }

                    // Distance falloff from origin
                    float distanceAtten = saturate(1.0 - (dist / maxRadius));

                    float pulseContribution = (waveFront * 2.5 + trail * 0.8) * intensity * distanceAtten;

                    echoColorAcc += colorData.rgb * pulseContribution;
                    echoIntensityAcc += pulseContribution;
                }

                // 4. Combine illumination and overlays
                float totalIllum = saturate((echoIntensityAcc + _EchoAmbientBrightness) * _MasterBrightness);

                fixed4 sceneCol = tex2D(_MainTex, i.uv);

                float3 tintColor = (echoIntensityAcc > 0.02)
                    ? (echoColorAcc / max(0.001, echoIntensityAcc))
                    : float3(0.2, 0.4, 0.6);

                float3 gridOverlay = tintColor * (gridMask * _EchoGridIntensity * totalIllum);
                float3 edgeOverlay = tintColor * (edge * _EchoEdgeIntensity * totalIllum);
                float3 revealedScene = sceneCol.rgb * totalIllum * _RevealTexture;
                float3 pulseGlow = echoColorAcc * 0.75 * _MasterBrightness;

                float3 finalColor = _EchoVoidColor.rgb * (1.0 - totalIllum)
                                  + revealedScene
                                  + pulseGlow
                                  + gridOverlay
                                  + edgeOverlay;

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
