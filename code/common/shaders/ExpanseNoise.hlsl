#ifndef EXPANSE_SKY_NOISE_INCLUDED
#define EXPANSE_SKY_NOISE_INCLUDED

#include "../../common/shaders/ExpanseRandom.hlsl"

struct NoiseResultAndCoordinate {
  float3 result;
  float3 coordinate;
};

float3 voronoi2D(float2 uv, float2 cells) {
  /* Point on our grid. */
  float2 p = uv * cells;

  /* Generate the cell point. */
  float2 tl = floor(uv * cells);
  float2 o = tl + random_2_2(tl);

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
        float2 o_neighbor = tl_neighbor + random_2_2(tl_neighbor);
        float d_neighbor = length(p - o_neighbor);
        minD = min(minD, d_neighbor);
      }
    }
  }

  /* Normalize by max distance. */
  minD /= sqrt(8);
  return float3(minD, minD, minD);
}

float3 worley2D(float2 uv, float2 cells) {
  return 1 - voronoi2D(uv, cells);
}

NoiseResultAndCoordinate voronoi3D(float3 uv, float3 cells) {
  /* Final result. */
  NoiseResultAndCoordinate result;

  /* Point on our grid. */
  float3 p = uv * cells;

  /* Generate the cell point. */
  float3 tl = floor(uv * cells);
  float3 o = tl + random_3_3(tl);

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
          float3 o_neighbor = tl_neighbor + random_3_3(tl_neighbor);
          float d_neighbor = length(p - o_neighbor);
          if (d_neighbor < minD) {
            minD = d_neighbor;
            minPoint = tl_neighbor;
          }
        }
      }
    }
  }

  result.result = float3(minD, minD, minD);
  /* Normalize by max distance. */
  result.result /= sqrt(12);
  result.coordinate = minPoint;
  return result;
}

NoiseResultAndCoordinate worley3D(float3 uv, float3 cells) {
  NoiseResultAndCoordinate result = voronoi3D(uv, cells);
  result.result = 1 - result.result;
  return result;
}

#endif  // EXPANSE_SKY_RANDOM_INCLUDED
