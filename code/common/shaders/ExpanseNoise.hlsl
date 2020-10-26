#ifndef EXPANSE_SKY_NOISE_INCLUDED
#define EXPANSE_SKY_NOISE_INCLUDED

#include "../../common/shaders/ExpanseRandom.hlsl"
#include "../../common/shaders/ExpanseSkyCommon.hlsl"

struct NoiseResultAndCoordinate {
  float result;
  float3 coordinate;
};

/******************************************************************************/
/***************************** UTILITY FUNCTIONS ******************************/
/******************************************************************************/

float remap(float value, float original_min, float original_max, float new_min, float new_max) {
  return new_min + ((value - original_min) / (original_max - original_min)) * (new_max - new_min);
}

/******************************************************************************/
/*************************** END UTILITY FUNCTIONS ****************************/
/******************************************************************************/



/******************************************************************************/
/********************************* CELL NOISE *********************************/
/******************************************************************************/

float voronoi2DSeeded(float2 uv, float2 cells, float2 seed_x, float2 seed_y) {
  /* Point on our grid. */
  float2 p = uv * cells;

  /* Generate the cell point. */
  float2 tl = floor(uv * cells);
  float2 o = tl + random_2_2_seeded(tl, seed_x, seed_y);

  /* Compute distance from p to the cell point. */
  float minD = length(p - o);

  /* Compute the distance to the points in the neighboring cells. */
  for (int x = -1; x < 2; x++) {
    for  (int y = -1; y < 2; y++) {
      if (!(x == 0 && y == 0)) {
        float2 offset = float2(x, y);
        float2 tl_neighbor = tl + offset;
        /* Wraparound to make tileable. */
        tl_neighbor = tl_neighbor - cells * floor(tl_neighbor / cells);
        float2 o_neighbor = tl_neighbor + random_2_2_seeded(tl_neighbor, seed_x, seed_y);
        float d_neighbor = length(p - o_neighbor);
        minD = min(minD, d_neighbor);
      }
    }
  }

  /* Normalize by max distance. */
  minD /= sqrt(8);
  return minD;
}

float voronoi2D(float2 uv, float2 cells) {
  voronoi2DSeeded(uv, cells,
    float2(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2),
    float2(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2));
}

float worley2DSeeded(float2 uv, float2 cells, float2 seed_x, float2 seed_y) {
  return 1 - voronoi2DSeeded(uv, cells, seed_x, seed_y);
}

float worley2D(float2 uv, float2 cells) {
  return worley2DSeeded(uv, cells,
    float2(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2),
    float2(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2));
}

NoiseResultAndCoordinate voronoi3DSeeded(float3 uv, float3 cells,
  float3 seed_x, float3 seed_y, float3 seed_z) {
  /* Final result. */
  NoiseResultAndCoordinate result;

  /* Point on our grid. */
  float3 p = uv * cells;

  /* Generate the cell point. */
  float3 tl = floor(uv * cells);
  float3 o = tl + random_3_3_seeded(tl, seed_x, seed_y, seed_z);

  /* Compute distance from p to the cell point. */
  float minD = length(p - o);
  float3 minPoint = tl;

  /* Compute the distance to the points in the neighboring cells. */
  for (int x = -1; x < 2; x++) {
    for  (int y = -1; y < 2; y++) {
      for  (int z = -1; z < 2; z++) {
        if (!(x == 0 && y == 0 && z == 0)) {
          float3 offset = float3(x, y, z);
          float3 tl_neighbor = tl + offset;
          /* Wraparound to make tileable. */
          tl_neighbor = tl_neighbor - cells * floor(tl_neighbor / cells);
          float3 o_neighbor = tl_neighbor + random_3_3_seeded(tl_neighbor, seed_x, seed_y, seed_z);
          float d_neighbor = length(p - o_neighbor);
          if (d_neighbor < minD) {
            minD = d_neighbor;
            minPoint = tl_neighbor;
          }
        }
      }
    }
  }

  result.result = minD;
  /* Normalize by max distance. */
  result.result /= sqrt(12);
  result.coordinate = minPoint;
  return result;
}

NoiseResultAndCoordinate voronoi3D(float3 uv, float3 cells) {
  return voronoi3DSeeded(uv, cells,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3),
    float3(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2, EXPANSE_DEFAULT_SEED_Y_3),
    float3(EXPANSE_DEFAULT_SEED_Z_1, EXPANSE_DEFAULT_SEED_Z_2, EXPANSE_DEFAULT_SEED_Z_3));
}

NoiseResultAndCoordinate worley3DSeeded(float3 uv, float3 cells,
  float3 seed_x, float3 seed_y, float3 seed_z) {
  NoiseResultAndCoordinate result = voronoi3DSeeded(uv, cells, seed_x, seed_y, seed_z);
  result.result = 1 - result.result;
  return result;
}

NoiseResultAndCoordinate worley3D(float3 uv, float3 cells) {
  return worley3DSeeded(uv, cells,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3),
    float3(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2, EXPANSE_DEFAULT_SEED_Y_3),
    float3(EXPANSE_DEFAULT_SEED_Z_1, EXPANSE_DEFAULT_SEED_Z_2, EXPANSE_DEFAULT_SEED_Z_3));
}

float voronoi3DLayered(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = voronoi3D(uv,
      startingGrid);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

float voronoi3DLayeredSeeded(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers,
  float3 seed_x, float3 seed_y, float3 seed_z) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = voronoi3DSeeded(uv,
      startingGrid, seed_x, seed_y, seed_z);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

float worley3DLayered(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = worley3D(uv,
      startingGrid);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

float worley3DLayeredSeeded(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers,
  float3 seed_x, float3 seed_y, float3 seed_z) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = worley3DSeeded(uv,
      startingGrid, seed_x, seed_y, seed_z);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

/******************************************************************************/
/******************************* END CELL NOISE *******************************/
/******************************************************************************/



/******************************************************************************/
/****************************** TRADITIONAL NOISE *****************************/
/******************************************************************************/

NoiseResultAndCoordinate value3DSeeded(float3 uv, float3 cells, float3 seed) {
  /* Final result. */
  NoiseResultAndCoordinate result;

  /* Point on our grid. */
  float3 p = uv * cells;

  /* Generate the top left point of the cell. */
  float3 tl = floor(uv * cells);

  /* Grid points. */
  float3 grid_000 = tl;
  float3 grid_001 = tl + float3(0, 0, 1);
  float3 grid_010 = tl + float3(0, 1, 0);
  float3 grid_011 = tl + float3(0, 1, 1);
  float3 grid_100 = tl + float3(1, 0, 0);
  float3 grid_101 = tl + float3(1, 0, 1);
  float3 grid_110 = tl + float3(1, 1, 0);
  float3 grid_111 = tl + float3(1, 1, 1);

  /* Wraparound. */
  grid_000 -= cells * floor(grid_000 / cells);
  grid_001 -= cells * floor(grid_001 / cells);
  grid_010 -= cells * floor(grid_010 / cells);
  grid_011 -= cells * floor(grid_011 / cells);
  grid_100 -= cells * floor(grid_100 / cells);
  grid_101 -= cells * floor(grid_101 / cells);
  grid_110 -= cells * floor(grid_110 / cells);
  grid_111 -= cells * floor(grid_111 / cells);


  /* Noise values. */
  float noise_000 = random_3_1_seeded(grid_000, seed);
  float noise_001 = random_3_1_seeded(grid_001, seed);
  float noise_010 = random_3_1_seeded(grid_010, seed);
  float noise_011 = random_3_1_seeded(grid_011, seed);
  float noise_100 = random_3_1_seeded(grid_100, seed);
  float noise_101 = random_3_1_seeded(grid_101, seed);
  float noise_110 = random_3_1_seeded(grid_110, seed);
  float noise_111 = random_3_1_seeded(grid_111, seed);

  /* Lerp. */
  float3 a = frac(p);
  /* z. */
  float noise_00 = lerp(noise_000, noise_001, smoothstep(0, 1, a.z));
  float noise_01 = lerp(noise_010, noise_011, smoothstep(0, 1, a.z));
  float noise_10 = lerp(noise_100, noise_101, smoothstep(0, 1, a.z));
  float noise_11 = lerp(noise_110, noise_111, smoothstep(0, 1, a.z));
  /* y. */
  float noise_0 = lerp(noise_00, noise_01, smoothstep(0, 1, a.y));
  float noise_1 = lerp(noise_10, noise_11, smoothstep(0, 1, a.y));
  /* x. */
  float noise = lerp(noise_0, noise_1, smoothstep(0, 1, a.x));

  result.result = noise;
  result.coordinate = p;
  return result;
}

NoiseResultAndCoordinate value3D(float3 uv, float3 cells) {
  return value3DSeeded(uv, cells,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3));
}

float value3DLayered(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = value3D(uv,
      startingGrid);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

float value3DLayeredSeeded(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers, float3 seed) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = value3DSeeded(uv,
      startingGrid, seed);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

// TODO HACK: fails for grid size of less than 3,3,3. TODO: also fails
// when being warped. Something is wrong with this implementation...
// default z seed maybe? probably not since it works fine for voronoi
NoiseResultAndCoordinate perlin3DSeeded(float3 uv, float3 cells, float3 seed_x,
  float3 seed_y, float3 seed_z) {
  /* Final result. */
  NoiseResultAndCoordinate result;

  /* Point on our grid. */
  float3 p = uv * cells;

  /* Generate the top left point of the cell. */
  float3 tl = max(0, min(cells, floor(uv * cells)));

  /* Grid points. */
  float3 grid_000 = tl;
  float3 grid_001 = tl + float3(0, 0, 1);
  float3 grid_010 = tl + float3(0, 1, 0);
  float3 grid_011 = tl + float3(0, 1, 1);
  float3 grid_100 = tl + float3(1, 0, 0);
  float3 grid_101 = tl + float3(1, 0, 1);
  float3 grid_110 = tl + float3(1, 1, 0);
  float3 grid_111 = tl + float3(1, 1, 1);

  /* Offset vectors---important to compute before wraparound. */
  float3 offset_000 = (grid_000 - p);
  float3 offset_001 = (grid_001 - p);
  float3 offset_010 = (grid_010 - p);
  float3 offset_011 = (grid_011 - p);
  float3 offset_100 = (grid_100 - p);
  float3 offset_101 = (grid_101 - p);
  float3 offset_110 = (grid_110 - p);
  float3 offset_111 = (grid_111 - p);

  /* Wraparound. */
  grid_000 -= cells * floor(grid_000 / cells);
  grid_001 -= cells * floor(grid_001 / cells);
  grid_010 -= cells * floor(grid_010 / cells);
  grid_011 -= cells * floor(grid_011 / cells);
  grid_100 -= cells * floor(grid_100 / cells);
  grid_101 -= cells * floor(grid_101 / cells);
  grid_110 -= cells * floor(grid_110 / cells);
  grid_111 -= cells * floor(grid_111 / cells);


  /* Gradient vectors. */
  float3 gradient_000 = normalize(random_3_3_seeded(grid_000, seed_x, seed_y, seed_z));
  float3 gradient_001 = normalize(random_3_3_seeded(grid_001, seed_x, seed_y, seed_z));
  float3 gradient_010 = normalize(random_3_3_seeded(grid_010, seed_x, seed_y, seed_z));
  float3 gradient_011 = normalize(random_3_3_seeded(grid_011, seed_x, seed_y, seed_z));
  float3 gradient_100 = normalize(random_3_3_seeded(grid_100, seed_x, seed_y, seed_z));
  float3 gradient_101 = normalize(random_3_3_seeded(grid_101, seed_x, seed_y, seed_z));
  float3 gradient_110 = normalize(random_3_3_seeded(grid_110, seed_x, seed_y, seed_z));
  float3 gradient_111 = normalize(random_3_3_seeded(grid_111, seed_x, seed_y, seed_z));

  /* Noise values. */
  float3 noise_000 = dot(gradient_000, offset_000);
  float3 noise_001 = dot(gradient_001, offset_001);
  float3 noise_010 = dot(gradient_010, offset_010);
  float3 noise_011 = dot(gradient_011, offset_011);
  float3 noise_100 = dot(gradient_100, offset_100);
  float3 noise_101 = dot(gradient_101, offset_101);
  float3 noise_110 = dot(gradient_110, offset_110);
  float3 noise_111 = dot(gradient_111, offset_111);

  /* Lerp. */
  float3 a = saturate(frac(p));
  /* z. */
  float noise_00 = lerp(noise_000, noise_001, a.z);// smoothstep(0, 1, a.z));
  float noise_01 = lerp(noise_010, noise_011, a.z);//smoothstep(0, 1, a.z));
  float noise_10 = lerp(noise_100, noise_101, a.z);//smoothstep(0, 1, a.z));
  float noise_11 = lerp(noise_110, noise_111, a.z);//smoothstep(0, 1, a.z));
  /* y. */
  float noise_0 = lerp(noise_00, noise_01, a.y);//smoothstep(0, 1, a.y));
  float noise_1 = lerp(noise_10, noise_11, a.y);//smoothstep(0, 1, a.y));
  /* x. */
  float noise = lerp(noise_0, noise_1, a.x);//smoothstep(0, 1, a.x));

  result.result = (noise+1)/2;
  result.coordinate = p;
  return result;
}

NoiseResultAndCoordinate perlin3D(float3 uv, float3 cells) {
  return perlin3DSeeded(uv, cells,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3),
    float3(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2, EXPANSE_DEFAULT_SEED_Y_3),
    float3(EXPANSE_DEFAULT_SEED_Z_1, EXPANSE_DEFAULT_SEED_Z_2, EXPANSE_DEFAULT_SEED_Z_3));
}

float perlin3DLayered(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = perlin3D(uv,
      startingGrid);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

float perlin3DLayeredSeeded(float3 uv, float3 startingGrid,
  float gridScaleFactor, float amplitudeFactor, int layers, float3 seed_x,
  float3 seed_y, float3 seed_z) {
  float maxValue = 0.0;
  float amplitude = 1.0;
  float noise = 0.0;
  for (int i = 0; i < layers; i++) {
    NoiseResultAndCoordinate layerNoise = perlin3DSeeded(uv,
      startingGrid, seed_x, seed_y, seed_z);
    noise += layerNoise.result * amplitude;
    maxValue += amplitudeFactor;
    amplitude *= amplitudeFactor;
    startingGrid *= gridScaleFactor;
  }
  return noise / maxValue;
}

/******************************************************************************/
/**************************** END TRADITIONAL NOISE ***************************/
/******************************************************************************/



/******************************************************************************/
/****************************** SIMULATION NOISE ******************************/
/******************************************************************************/

float3 curlNoise3DSeeded(float3 uv, float3 cells, float3 seed_x, float3 seed_y,
  float3 seed_z) {
  float epsilon = 0.00001;

  /* Compute offset uv coordinates, taking into account wraparound. */
  float3 uvx0 = float3(uv.x - epsilon - floor(uv.x - epsilon), uv.y, uv.z);
  float3 uvxf = float3(uv.x + epsilon - floor(uv.x + epsilon), uv.y, uv.z);
  float3 uvy0 = float3(uv.x, uv.y - epsilon - floor(uv.y - epsilon), uv.z);
  float3 uvyf = float3(uv.x, uv.y + epsilon - floor(uv.y + epsilon), uv.z);
  float3 uvz0 = float3(uv.x, uv.y, uv.z - epsilon - floor(uv.z - epsilon));
  float3 uvzf = float3(uv.x, uv.y, uv.z + epsilon - floor(uv.z + epsilon));

  /* Compute noise values for finite differencing. */
  // float x0 = perlin3DSeeded(uvx0, cells, seed_x, seed_y, seed_z).result;
  // float xf = perlin3DSeeded(uvxf, cells, seed_x, seed_y, seed_z).result;
  // float y0 = perlin3DSeeded(uvy0, cells, seed_x, seed_y, seed_z).result;
  // float yf = perlin3DSeeded(uvyf, cells, seed_x, seed_y, seed_z).result;
  // float z0 = perlin3DSeeded(uvz0, cells, seed_x, seed_y, seed_z).result;
  // float zf = perlin3DSeeded(uvzf, cells, seed_x, seed_y, seed_z).result;
  float x0 = value3DSeeded(uvx0, cells, seed_x).result;
  float xf = value3DSeeded(uvxf, cells, seed_x).result;
  float y0 = value3DSeeded(uvy0, cells, seed_x).result;
  float yf = value3DSeeded(uvyf, cells, seed_x).result;
  float z0 = value3DSeeded(uvz0, cells, seed_x).result;
  float zf = value3DSeeded(uvzf, cells, seed_x).result;

  /* Compute the derivatives via finite differencing. */
  float dx = (xf - x0) / (2 * epsilon);
  float dy = (yf - y0) / (2 * epsilon);
  float dz = (zf - z0) / (2 * epsilon);

  /* Return the curl. */
  return float3(dz - dy, dx - dz, dy - dx);
}

float3 curlNoise3D(float3 uv, float3 cells) {
  return curlNoise3DSeeded(uv, cells,
    float3(EXPANSE_DEFAULT_SEED_X_1, EXPANSE_DEFAULT_SEED_X_2, EXPANSE_DEFAULT_SEED_X_3),
    float3(EXPANSE_DEFAULT_SEED_Y_1, EXPANSE_DEFAULT_SEED_Y_2, EXPANSE_DEFAULT_SEED_Y_3),
    float3(EXPANSE_DEFAULT_SEED_Z_1, EXPANSE_DEFAULT_SEED_Z_2, EXPANSE_DEFAULT_SEED_Z_3));
}

/******************************************************************************/
/**************************** END SIMULATION NOISE ****************************/
/******************************************************************************/

#endif  // EXPANSE_SKY_RANDOM_INCLUDED
