Shader "Hidden/EchoSound/AcousticResponse"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _AcousticDrawResponse;
            float4 _EchoAbyssFade; // rim height, start depth, end depth, enabled
            struct v2f { float4 vertex : SV_POSITION; float4 screen : TEXCOORD0; float eye : TEXCOORD1; float3 world:TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.vertex);
                o.eye = -UnityObjectToViewPos(v.vertex).z;
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                // Only the visible surface contributes; do not paint profiles through walls.
                clip(max(0.02, depth * 0.002) - abs(depth - i.eye));
                if (_EchoAbyssFade.w > 0.5)
                {
                    // Small world-anchored variation avoids a perfectly straight extinction contour.
                    float variation = sin(i.world.x*0.37+sin(i.world.z*0.21))*0.2;
                    float depth = max(0,_EchoAbyssFade.x-i.world.y+variation);
                    float t = saturate((depth-_EchoAbyssFade.y)/max(0.1,_EchoAbyssFade.z-_EchoAbyssFade.y));
                    float visibility = exp(-4*t)*(1-smoothstep(0.65,1,t));
                    return float4(1,1,-1,visibility);
                }
                return _AcousticDrawResponse;
            }
            ENDCG
        }
    }
}
