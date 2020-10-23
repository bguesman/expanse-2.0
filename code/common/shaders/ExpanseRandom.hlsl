#ifndef EXPANSE_SKY_RANDOM_INCLUDED
#define EXPANSE_SKY_RANDOM_INCLUDED

float random_1_1(float u) {
  return frac(sin(u * 12.9898)*43758.5453123);
}

float random_2_1(float2 uv) {
  return frac(sin(dot(uv,float2(12.9898, 78.233)))*43758.5453123);
}

float2 random_2_2(float2 uv) {
  return frac(sin(float2(dot(uv,float2(127.1, 103.953)),
    dot(uv,float2(269.5, 183.3))))*43758.5453);
}

float3 random_2_3(float2 uv) {
  return frac(sin(float3(dot(uv,float2(127.1, 103.953)),
    dot(uv,float2(269.5, 183.3)),
    dot(uv,float2(165.932, 53.209))))*43758.5453);
}

float random_3_1(float3 uv) {
  return frac(sin(dot(uv,float3(12.9898, 78.233, 103.953)))*43758.5453123);
}

float random_3_1_seeded(float3 uv, float3 seed) {
  return frac(sin(dot(uv,seed))*43758.5453123);
}

float3 random_3_3(float3 uv) {
  return frac(sin(float3(dot(uv,float3(127.1, 311.7, 103.953)),
    dot(uv,float3(269.5, 183.3, 119.234)),
    dot(uv,float3(453.1293, 165.932, 53.209))))*43758.5453);
}

float3 random_3_3_seeded(float3 uv, float3 seed1, float3 seed2, float3 seed3) {
  return frac(sin(float3(dot(uv,seed1),
    dot(uv,seed2),
    dot(uv,seed3)))*43758.5453);
}

#endif  // EXPANSE_SKY_RANDOM_INCLUDED
