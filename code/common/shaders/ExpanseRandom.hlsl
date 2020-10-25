#ifndef EXPANSE_SKY_RANDOM_INCLUDED
#define EXPANSE_SKY_RANDOM_INCLUDED

#define EXPANSE_DEFAULT_SEED_X_1 12.9898
#define EXPANSE_DEFAULT_SEED_X_2 78.233
#define EXPANSE_DEFAULT_SEED_X_3 103.953
#define EXPANSE_DEFAULT_SEED_Y_1 269.5
#define EXPANSE_DEFAULT_SEED_Y_2 183.3
#define EXPANSE_DEFAULT_SEED_Y_3 119.234
#define EXPANSE_DEFAULT_SEED_Z_1 453.1293
#define EXPANSE_DEFAULT_SEED_Z_2 165.932
#define EXPANSE_DEFAULT_SEED_Z_3 53.209

float random_1_1_seeded(float u, float seed) {
  return frac(sin(u * seed)*43758.5453123);
}
float random_1_1(float u) {
  return random_1_1_seeded(u, EXPANSE_DEFAULT_SEED_X_1);
}

float random_2_1_seeded(float2 uv, float2 seed) {
  return frac(sin(dot(uv, seed))*43758.5453123);
}
float random_2_1(float2 uv) {
  return random_2_1_seeded(uv,
    float2(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2));
}

float2 random_2_2_seeded(float2 uv, float2 seed_x, float2 seed_y) {
  return frac(sin(float2(dot(uv, seed_x),
    dot(uv, seed_y)))*43758.5453);
}
float2 random_2_2(float2 uv) {
  return random_2_2_seeded(uv,
    float2(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2),
    float2(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2));
}

float3 random_2_3_seeded(float2 uv, float2 seed_x, float2 seed_y, float2 seed_z) {
  return frac(sin(float3(dot(uv, seed_x),
    dot(uv, seed_y), dot(uv, seed_z)))*43758.5453);
}
float3 random_2_3(float2 uv) {
  return random_2_3_seeded(uv,
    float2(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2),
    float2(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2),
    float2(EXPANSE_DEFAULT_SEED_Z_1, EXPANSE_DEFAULT_SEED_Z_2));
}

float random_3_1_seeded(float3 uv, float3 seed) {
  return frac(sin(dot(uv,seed))*43758.5453123);
}
float random_3_1(float3 uv) {
  return random_3_1_seeded(uv,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3));
}

float3 random_3_3_seeded(float3 uv, float3 seed_x, float3 seed_y, float3 seed_z) {
  return frac(sin(float3(dot(uv,seed_x),
    dot(uv,seed_y),
    dot(uv,seed_z)))*43758.5453);
}
float3 random_3_3(float3 uv) {
  return random_3_3_seeded(uv,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3),
    float3(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2, EXPANSE_DEFAULT_SEED_Y_3),
    float3(EXPANSE_DEFAULT_SEED_Z_1, EXPANSE_DEFAULT_SEED_Z_2, EXPANSE_DEFAULT_SEED_Z_3));
}



#endif  // EXPANSE_SKY_RANDOM_INCLUDED
