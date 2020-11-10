#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseNoise.hlsl"
#include "ExpanseCloudsCommon.hlsl"

/* As a note---all cloud volume computations are done still done in
 * planet space. */

struct CloudShadingResult {
  float3 color;
  float4 tAndBlend;
};

CloudShadingResult shadeCloudLayerPlaneGeometry(float3 O, float3 d, int i) {
  /* Final result. */
  CloudShadingResult result;

  /* Transform the cloud plane to planet space. */
  float2 xExtent = float2(transformXToPlanetSpace(_cloudGeometryXMin[i]),
    transformXToPlanetSpace(_cloudGeometryXMax[i]));
  float2 zExtent = float2(transformZToPlanetSpace(_cloudGeometryZMin[i]),
    transformZToPlanetSpace(_cloudGeometryZMax[i]));
  float height = transformYToPlanetSpace(_cloudGeometryHeight[i]);
  /* Intersect it. */
  float t_hit = intersectXZAlignedPlane(O, d, xExtent, zExtent, height);

  /* We hit nothing. */
  if (t_hit < 0) {
    result.color = float3(0, 0, 0);
    result.tAndBlend = float4(1, 1, 1, 0);
    return result;
  }

  /* Compute the UV coordinate from the intersection point. */
  float3 sample = O + t_hit * d;
  float u = (sample.x - xExtent.x) / (xExtent.y - xExtent.x);
  float v = (sample.z - zExtent.x) / (zExtent.y - zExtent.x);

  /* For now, compute some noise. */
  float noise = 3000 * worley2D(float2(u, v), float2(128, 128));

  result.color = float3(noise, noise, noise);
  result.tAndBlend = float4(0, 0, 0, 0);
  return result;
}

CloudShadingResult shadeCloudLayerSphereGeometry(float3 O, float3 d, int i) {
  /* TODO */
  CloudShadingResult result;
  result.color = float3(0, 0, 0);
  result.tAndBlend = float4(0, 0, 0, 0);
  return result;
}

CloudShadingResult shadeCloudLayerBoxVolumeGeometry(float3 O, float3 d, int i) {
  /* Final result. */
  CloudShadingResult result;

  /* Transform the cloud plane to planet space. */
  float2 xExtent = float2(transformXToPlanetSpace(_cloudGeometryXMin[i]),
    transformXToPlanetSpace(_cloudGeometryXMax[i]));
  float2 yExtent = float2(transformYToPlanetSpace(_cloudGeometryYMin[i]),
    transformYToPlanetSpace(_cloudGeometryYMax[i]));
  float2 zExtent = float2(transformZToPlanetSpace(_cloudGeometryZMin[i]),
    transformZToPlanetSpace(_cloudGeometryZMax[i]));
  /* Intersect it. */
  float2 t_hit = intersectXZAlignedBoxVolume(O, d, xExtent, yExtent, zExtent);

  /* We hit nothing. */
  if (t_hit.x < 0 && t_hit.y < 0) {
    result.color = float3(0, 0, 0);
    result.tAndBlend = float4(1, 1, 1, 0);
    return result;
  }

  /* TODO: we are inside the cloud volume. */
  if (t_hit.x < 0) {
    result.color = float3(0, 0, 0);
    result.tAndBlend = float4(1, 1, 1, 0);
    return result;
  }

  /* We're outside the cloud volume, and we hit it. */

  /* Compute the UV coordinate from the intersection point. */
  float3 sample = O + t_hit.x * d;
  float u = (sample.x - xExtent.x) / (xExtent.y - xExtent.x);
  float v = (sample.y - yExtent.x) / (yExtent.y - yExtent.x);
  float w = (sample.z - zExtent.x) / (zExtent.y - zExtent.x);

  /* For now, compute some noise. */
  float noise = 1000 * worley3D(float3(u, v, w), float3(128, 128, 128)).result;

  result.color = float3(noise, noise, noise);
  result.tAndBlend = float4(0, 0, 0, 0);
  return result;
}

CloudShadingResult shadeCloudLayer(float3 O, float3 d, int i) {
  switch (_cloudGeometryType[i]) {
    case 0:
      return shadeCloudLayerPlaneGeometry(O, d, i);
    case 1:
      return shadeCloudLayerSphereGeometry(O, d, i);
    case 2:
      return shadeCloudLayerBoxVolumeGeometry(O, d, i);
    default:
      return shadeCloudLayerPlaneGeometry(O, d, i);
  }
}

CloudShadingResult shadeClouds(float3 O, float3 d) {
  /* Final result. */
  /* TODO: blending decisions? */
  return shadeCloudLayer(O, d, 0);
}
