#ifndef ECHOSOUND_COMMON
#define ECHOSOUND_COMMON
int _EchoPulseCount;
float4 _EchoPulseOrigins[16];
float4 _EchoPulseParams[16]; // start, visual speed, loudness, decay
float4 _EchoPulseColors[16]; // emissive RGB, front width
float _EchoTime;
float _EchoGridScale, _EchoGridIntensity, _EchoEdgeIntensity;
float _EchoSurfaceTint, _EchoFresnelIntensity;
float _EchoCeilingVisibility;
float4 _EchoContactStyle; // floor memory, near-floor memory, contact strength, surface variation

float EchoGrid(float3 position, float3 normal)
{
    float3 coord = position / max(0.2, _EchoGridScale);
    float3 lines = 1 - saturate(abs(frac(coord - 0.5) - 0.5) / max(fwidth(coord), 0.001));
    float3 weights = pow(abs(normal), 8);
    weights /= max(dot(weights, float3(1,1,1)), 0.001);
    return dot(float3(max(lines.y,lines.z), max(lines.x,lines.z), max(lines.x,lines.y)), weights);
}

float EchoNoise(float3 p)
{
    float3 cell = floor(p), f = frac(p);
    f = f*f*(3-2*f);
    float value = 0;
    [unroll] for (int z=0; z<2; z++)
    [unroll] for (int y=0; y<2; y++)
    [unroll] for (int x=0; x<2; x++)
    {
        float3 offset = float3(x,y,z);
        float3 seed = frac((cell+offset)*0.1031);
        seed += dot(seed,seed.yzx+33.33);
        float hash = frac((seed.x+seed.y)*seed.z);
        float3 w = lerp(1-f,f,offset);
        value += hash*w.x*w.y*w.z;
    }
    return value;
}
float EchoFloorMask(float3 position, float3 normal)
{
    // Keep tabletops and ceilings responsive; suppress upward-facing ground below the receiver.
    return saturate(normal.y)*smoothstep(0.8,1.5,_WorldSpaceCameraPos.y-position.y);
}
float EchoFloorVisibility(float3 position, float3 normal)
{
    float nearWeight = smoothstep(1.5,7,distance(position.xz,_WorldSpaceCameraPos.xz));
    float floorWeight = lerp(_EchoContactStyle.y,_EchoContactStyle.x,nearWeight);
    return lerp(1,floorWeight,EchoFloorMask(position,normal));
}
float EchoCeilingVisibility(float3 position, float3 normal)
{
    float ceiling = smoothstep(0.5,0.9,-normal.y)
        * smoothstep(0.6,1.4,position.y-_WorldSpaceCameraPos.y);
    return lerp(1,saturate(_EchoCeilingVisibility),ceiling);
}
void EvaluateEchoResponse(float3 position, float3 normal, float2 response, out float3 memory, out float3 contact)
{
    memory = 0;
    contact = 0;
    if (_EchoPulseCount <= 0) return;
    // Stable world-space variation belongs to the surface, not to the screen or the sphere.
    float variation = EchoNoise(position*3.5);
    [loop] for (int p = 0; p < min(_EchoPulseCount,16); p++)
    {
        float4 origin = _EchoPulseOrigins[p], param = _EchoPulseParams[p], tint = _EchoPulseColors[p];
        float dist = distance(position,origin.xyz);
        if (dist >= origin.w) continue;
        // A true world-space sphere: surfaces respond when the outgoing front reaches them.
        float age = _EchoTime - param.x - dist / max(1,param.y);
        // A quiet/absorbing surface still needs enough visual memory to read its shape.
        float duration = param.w * clamp(response.y,0.35,1);
        if (age < 0 || age > duration) continue;
        float width = max(0.05,tint.w);
        float behind = age * param.y;
        float onset = smoothstep(0,width * 0.16,behind);
        float progress = saturate(age / duration);
        float trail = pow(1-progress,2) * exp(-progress * 1.5) * onset;
        float attenuation = (0.5 + 0.5 / (1 + dist * 0.08)) * (1-smoothstep(origin.w*0.8,origin.w,dist));
        float visualReflection = response.x <= 0 ? 0 : lerp(0.25,1,saturate(response.x));
        float3 radiance = tint.rgb * param.z * attenuation * visualReflection;
        memory += radiance * trail * 0.65;
        // A short surface response replaces the broad, uniformly filled spherical shell.
        float hitDuration = max(0.07,width / max(1,param.y)*2) * clamp(response.y,0.05,1);
        float responseAge = age / hitDuration;
        float hit = (1-exp(-responseAge*12))*exp(-responseAge*2.3);
        float surfaceResponse = lerp(1,0.25+variation*1.5,_EchoContactStyle.w);
        contact += radiance * hit * surfaceResponse * _EchoContactStyle.z * 2.5;
    }
}
void EvaluateEcho(float3 position, float3 normal, out float3 memory, out float3 contact)
{
    EvaluateEchoResponse(position,normal,float2(1,1),memory,contact);
}
float EchoFresnel(float3 position, float3 normal)
{
    float3 view = normalize(_WorldSpaceCameraPos.xyz-position);
    return pow(1-saturate(dot(normal,view)),3);
}
// Material markings only recolor existing echo radiance. Zero input stays completely black.
float3 EchoMaterialCue(float3 radiance, float3 position, float3 normal, float4 response)
{
    int kind = (int)round(response.z * 10);
    if (kind < 1 || kind > 4 || response.w <= 0) return radiance;
    // Schematic material markings belong to the floor, not walls, ceilings or props.
    if (EchoFloorMask(position,normal) < 0.5) return radiance;
    float3 axis = abs(normal);
    float2 uv = axis.y > 0.5 ? position.xz : (axis.x > axis.z ? position.zy : position.xy);
    float pattern;
    float3 tint;
    if (kind == 1)
    {
        pattern = 1 - smoothstep(0.06,0.17,abs(frac((uv.x+uv.y)*3)-0.5));
        tint = float3(0.25,0.8,1);
    }
    else if (kind == 2)
    {
        pattern = smoothstep(0.4,0.7,EchoNoise(position*6));
        tint = float3(0.8,0.82,0.85);
    }
    else if (kind == 3)
    {
        pattern = 1 - smoothstep(0.08,0.2,abs(frac(uv.y*5+sin(uv.x*2)*0.15)-0.5));
        tint = float3(1,0.6,0.22);
    }
    else
    {
        float2 weave = 1-smoothstep(0.05,0.18,abs(frac(uv*5)-0.5));
        pattern = max(weave.x,weave.y);
        tint = float3(0.35,1,0.5);
    }
    float brightness = max(radiance.r,max(radiance.g,radiance.b));
    return lerp(radiance,tint*brightness,saturate(response.w)*pattern*0.5);
}
#endif
