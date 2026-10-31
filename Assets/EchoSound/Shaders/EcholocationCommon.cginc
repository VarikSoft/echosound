#ifndef ECHOSOUND_COMMON
#define ECHOSOUND_COMMON
int _EchoPulseCount;
float4 _EchoPulseOrigins[16];
float4 _EchoPulseParams[16]; // start, visual speed, loudness, decay
float4 _EchoPulseColors[16]; // emissive RGB, front width
float _EchoTime;
float _EchoGridScale, _EchoGridIntensity, _EchoEdgeIntensity;
float _EchoSurfaceTint, _EchoFresnelIntensity;

float EchoGrid(float3 position, float3 normal)
{
    float3 coord = position / max(0.2, _EchoGridScale);
    float3 lines = 1 - saturate(abs(frac(coord - 0.5) - 0.5) / max(fwidth(coord), 0.001));
    float3 weights = pow(abs(normal), 8);
    weights /= max(dot(weights, float3(1,1,1)), 0.001);
    return dot(float3(max(lines.y,lines.z), max(lines.x,lines.z), max(lines.x,lines.y)), weights);
}

void EvaluateEcho(float3 position, float3 normal, out float3 color, out float illumination)
{
    color = 0;
    illumination = 0;
    [loop] for (int p = 0; p < min(_EchoPulseCount,16); p++)
    {
        float4 origin = _EchoPulseOrigins[p], param = _EchoPulseParams[p], tint = _EchoPulseColors[p];
        float dist = distance(position,origin.xyz);
        if (dist >= origin.w) continue;
        // A true world-space sphere: surfaces respond when the outgoing front reaches them.
        float age = _EchoTime - param.x - dist / max(1,param.y);
        if (age < 0 || age > param.w) continue;
        float width = max(0.05,tint.w);
        float behind = age * param.y;
        float onset = smoothstep(0,width * 0.16,behind);
        float front = exp(-pow(behind / width,2)) * onset;
        float progress = saturate(age / param.w);
        float trail = pow(1-progress,2) * exp(-progress * 1.5) * onset;
        float attenuation = (0.5 + 0.5 / (1 + dist * 0.08)) * (1-smoothstep(origin.w*0.8,origin.w,dist));
        float contribution = (front * 2.8 + trail * 0.65) * param.z * attenuation;
        // Add radiance before exposure so simultaneous sources mix their colors.
        color += tint.rgb * contribution;
        illumination += contribution;
    }
}
float EchoFresnel(float3 position, float3 normal)
{
    float3 view = normalize(_WorldSpaceCameraPos.xyz-position);
    return pow(1-saturate(dot(normal,view)),3);
}
#endif
