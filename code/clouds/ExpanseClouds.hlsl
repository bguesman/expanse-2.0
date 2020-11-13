#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseNoise.hlsl"
#include "../sky/ExpanseSkyMapping.hlsl"
#include "ExpanseCloudsCommon.hlsl"

/* As a note---all cloud volume computations are done still done in
 * planet space. */

struct CloudShadingResult {
  float3 color;             /* Color result. */
  float3 transmittance;     /* Transmittance of the clouds. */
  float blend;              /* Transmittance to the clouds. */
  bool hit;                 /* Whether or not we intersected the clouds. */
  float t_hit;              /* Hit point to use in sorting. */
};

CloudShadingResult cloudNoIntersectionResult() {
  CloudShadingResult result;
  result.color = float3(0, 0, 0);
  result.transmittance = float3(1, 1, 1);
  result.blend = 1;
  result.hit = false;
  result.t_hit = -1;
  return result;
}

float computeDensity2DHighLOD(float2 uv) {
  /* NOTE/TODO: we HAVE to use the linear repeat sampler to avoid seams in
   * the tileable textures! */

  /* Remap the base noise according to coverage. */
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
    uv, 0).x;
  float baseNoise = SAMPLE_TEXTURE2D_LOD(_cloudBaseNoise2D, s_linear_repeat_sampler,
    frac(uv * 4), 0).x;

  float noise = saturate(remap(baseNoise, 0.25*coverageNoise, 1.0, 0.0, 1.0));

  /* Remap that result using the tiled structure noise. */
  float structureNoise = SAMPLE_TEXTURE2D_LOD(_cloudStructureNoise2D, s_linear_repeat_sampler,
    frac(uv * 8), 0).x;
  noise = saturate(remap(noise, structureNoise * 0.45, 1.0, 0.0, 1.0));

  /* Finally, remap that result using the tiled detail noise. */
  float detailNoise = SAMPLE_TEXTURE2D_LOD(_cloudDetailNoise2D, s_linear_repeat_sampler,
    frac(uv * 16), 0).x;
  noise = saturate(remap(noise, detailNoise * 0.15, 1.0, 0.0, 1.0));

  return noise;
}

float computeDensity2DLowLOD(float2 uv) {
  // TODO
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_clamp_sampler,
    uv, 0).x;
  float baseNoise = SAMPLE_TEXTURE2D_LOD(_cloudBaseNoise2D, s_linear_clamp_sampler,
    uv, 0).x;
  float noise = saturate(remap(baseNoise, coverageNoise, 1.0, 0.0, 1.0));
  return noise;
}

float3 lightCloudLayerPlaneGeometryBody(float3 samplePoint, float thickness,
  float density, float3 absorptionCoefficients, float3 scatteringCoefficients,
  float3 L, float3 lightColor) {
  /* Do an occlusion check for lighting. */
  SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint, L,
    _planetRadius, _atmosphereRadius);
  float3 result = float3(0, 0, 0);
  /* TODO: we need to fudge this because it looks bad having a
   * flat line between shadow and no shadow. */
  if (!lightIntersection.groundHit) {
    /* HACK: to get rid of the super intense shadow/lit split we get from the
     * intersection, test how close we are to the horizon line. It may
     * be better to do this the other way around---push more light into
     * clouds that are technically occluded. */
    float r = length(samplePoint);
    float mu = dot(normalize(samplePoint), L);
    float h = r - _planetRadius;
    float cos_h = -safeSqrt(h * (2 * _planetRadius + h)) / (_planetRadius + h);
    float shadowBlur = 1 - pow(saturate(0.001 / abs(cos_h - mu)), 0.25);

    float t_lightHit = lightIntersection.endT - lightIntersection.startT;
    /* Compute in-scattering via a hack. We will assume
     * 1. That there is no volumetric shadowing for 2d clouds like this.
     * 2. That the single scattering is integrated directly upward along
     * the implicit cloud volume.
     *
     * This lets us integrate in-scattering analytically. */
    float2 lightTransmittanceCoord = mapSky2DCoord(r, mu,
      _atmosphereRadius, _planetRadius, t_lightHit,
      false, _resT.y);
    /* TODO: probably unnecessary to compute analytical transmittance here, since
     * we will unlikely be in a height fog layer and also in a cloud. */
    float3 T_sampleL = shadowBlur * exp(sampleSkyTTextureRaw(lightTransmittanceCoord));

    result = (1 - exp(-thickness * density * absorptionCoefficients))
      * (T_sampleL * lightColor * scatteringCoefficients/absorptionCoefficients);
  }
  return result;
}

float3 lightCloudLayerPlaneGeometry(float3 samplePoint, float thickness,
  float density, float3 absorptionCoefficients, float3 scatteringCoefficients) {
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    float3 lightColor = _bodyLightColor[i].xyz;
    color += lightCloudLayerPlaneGeometryBody(samplePoint, thickness,
      density, absorptionCoefficients, scatteringCoefficients, L, lightColor);
  }
  return color;
}

CloudShadingResult shadeCloudLayerPlaneGeometry(float3 O, float3 d, int i,
  float depth, bool geoHit, SkyIntersectionData skyIntersection) {
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
  if (t_hit < 0 || (geoHit && depth < t_hit)) {
    return cloudNoIntersectionResult();
  }

  result.hit = true;
  result.t_hit = t_hit;

  /* Compute the UV coordinate from the intersection point. */
  float3 samplePoint = O + t_hit * d;
  float u = (samplePoint.x - xExtent.x) / (xExtent.y - xExtent.x);
  float v = (samplePoint.z - zExtent.x) / (zExtent.y - zExtent.x);

  /* Sample the density. */
  float noise = computeDensity2DHighLOD(float2(u, v));

  /* Compute the thickness and density from the noise. */
  float thickness = noise * _cloudThickness[i];
  float density = _cloudDensity[i];

  /* Compute the density attenuation factor as a function of distance from
   * the cloud layer's origin. */
  float2 layerOrigin = float2(dot(xExtent, float2(1, 1))/2, dot(zExtent, float2(1, 1))/2);
  float distFromOrigin = length(layerOrigin - samplePoint.xz);
  float distanceAttenuation = saturate(exp(-(distFromOrigin-_cloudDensityAttenuationBias[i])/_cloudDensityAttenuationDistance[i]));
  density *= distanceAttenuation;

  /* Optical depth is just density times thickness, since density is constant. */
  float opticalDepth = density * thickness;

  /* Compute cloud transmittance from optical depth and absorption
   * coefficients. TODO: Maybe some kind of thickness hack based on light sample
   * direction also? */
  result.transmittance = exp(-_cloudAbsorptionCoefficients[i].xyz * opticalDepth);

  /* Light the clouds. HACK */
  bool debug = false;
  if (debug) {
    result.color = noise * 1000;
  } else {
    result.color = lightCloudLayerPlaneGeometry(samplePoint, thickness, density,
      _cloudAbsorptionCoefficients[i].xyz, _cloudScatteringCoefficients[i].xyz);
  }

  /* Compute the blend transmittance by sampling the transmittance to the
   * sample point. */
  /* TODO: analytical transmittance too? Probably
   * not since it's attenuated. I don't know. */
  float2 oToSample = mapSky2DCoord(length(O), dot(normalize(O), d),
    _atmosphereRadius, _planetRadius, skyIntersection.endT,
    skyIntersection.groundHit, _resT.y);
  float2 sampleOut = mapSky2DCoord(length(samplePoint), dot(normalize(samplePoint), d),
    _atmosphereRadius, _planetRadius, skyIntersection.endT - t_hit,
    skyIntersection.groundHit, _resT.y);
  float3 t_oToSample = sampleSkyTTextureRaw(oToSample);
  float3 t_sampleOut = sampleSkyTTextureRaw(sampleOut);
  float3 blendTransmittanceColor = exp(t_oToSample - max(t_oToSample, t_sampleOut)
   + computeTransmittanceDensityAttenuation(O, d, t_hit));
  result.blend = dot(blendTransmittanceColor, float3(1, 1, 1) / 3.0);

  return result;
}

CloudShadingResult shadeCloudLayerSphereGeometry(float3 O, float3 d, int i,
  float depth, bool geoHit, SkyIntersectionData skyIntersection) {
  /* TODO */
  return cloudNoIntersectionResult();
}

CloudShadingResult shadeCloudLayerBoxVolumeGeometry(float3 O, float3 d, int i,
  float depth, bool geoHit, SkyIntersectionData skyIntersection) {
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
    return cloudNoIntersectionResult();
  }

  /* TODO: we are inside the cloud volume. */
  if (t_hit.x < 0) {
    return cloudNoIntersectionResult();
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
  result.transmittance = float3(0, 0, 0);
  result.blend = 0;
  result.hit = true;
  result.t_hit = t_hit.x;
  return result;
}

CloudShadingResult shadeCloudLayer(float3 O, float3 d, int i, float depth,
  bool geoHit) {
  SkyIntersectionData skyIntersection = traceSkyVolume(O, d,
   _planetRadius, _atmosphereRadius);
  switch (_cloudGeometryType[i]) {
    case 0:
      return shadeCloudLayerPlaneGeometry(O, d, i, depth, geoHit, skyIntersection);
    case 1:
      return shadeCloudLayerSphereGeometry(O, d, i, depth, geoHit, skyIntersection);
    case 2:
      return shadeCloudLayerBoxVolumeGeometry(O, d, i, depth, geoHit, skyIntersection);
    default:
      return cloudNoIntersectionResult();
  }
}
