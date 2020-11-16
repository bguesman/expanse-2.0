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

float cloudPhaseFunction(float dot_L_d, float _cloudAnisotropy, float _cloudSilverIntensity, float _cloudSilverSpread) {
  return max(miePhase(dot_L_d, _cloudAnisotropy), _cloudSilverIntensity * miePhase(dot_L_d, 0.99 - _cloudSilverSpread));
}

float computeDensity2DHighLOD(float2 uv, int mipLevel) {
  /* NOTE/TODO: we HAVE to use the linear repeat sampler to avoid seams in
   * the tileable textures! */

  /* Get the warp noises, use them to advect the texture coordinates. */
  float2 baseWarpNoise = SAMPLE_TEXTURE2D_LOD(_cloudBaseWarpNoise2D, s_linear_repeat_sampler,
    frac(uv * _cloudBaseWarpTile), mipLevel).xy;
  float2 detailWarpNoise = SAMPLE_TEXTURE2D_LOD(_cloudDetailWarpNoise2D, s_linear_repeat_sampler,
    frac(uv * _cloudDetailWarpTile), mipLevel).xy;
  float2 baseUV = frac(frac(uv * _cloudBaseTile) - baseWarpNoise * _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX);
  float2 detailUV = frac(frac(uv * _cloudDetailTile) - detailWarpNoise * _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX);

  /* Remap the base noise according to coverage. */
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
    frac(uv * _cloudCoverageTile), mipLevel).x;
  float baseNoise = SAMPLE_TEXTURE2D_LOD(_cloudBaseNoise2D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;
  float noise = remap(baseNoise, min(0.99, max(0.0, _cloudCoverageIntensity * coverageNoise * 2)), 1.0, 0.0, 1.0);

  /* Remap that result using the tiled structure noise. */
  float structureNoise = SAMPLE_TEXTURE2D_LOD(_cloudStructureNoise2D, s_linear_repeat_sampler,
    frac(uv * _cloudStructureTile), mipLevel).x;
  noise = remap(noise, _cloudStructureIntensity * structureNoise, 1.0, 0.0, 1.0);

  /* Finally, remap that result using the tiled detail noise. */
  float detailNoise = SAMPLE_TEXTURE2D_LOD(_cloudDetailNoise2D, s_linear_repeat_sampler,
    detailUV, mipLevel).x;
  noise = remap(noise, _cloudDetailIntensity * detailNoise, 1.0, 0.0, 1.0);

  return noise;
}

float computeDensity2DLowLOD(float2 uv, int mipLevel) {
  /* Get the warp noises, use them to advect the texture coordinates. */
  float2 baseWarpNoise = SAMPLE_TEXTURE2D_LOD(_cloudBaseWarpNoise2D, s_linear_repeat_sampler,
    frac(uv * _cloudBaseWarpTile), mipLevel).xy;
  float2 baseUV = frac(frac(uv * _cloudBaseTile) - baseWarpNoise * _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX);

  /* Remap the base noise according to coverage. */
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
    frac(uv * _cloudCoverageTile), mipLevel).x;
  float baseNoise = SAMPLE_TEXTURE2D_LOD(_cloudBaseNoise2D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;
  float noise = remap(baseNoise, min(0.99, max(0.0, _cloudCoverageIntensity * coverageNoise * 2)), 1.0, 0.0, 1.0);

  return noise;
}

float3 lightCloudLayerPlaneGeometryBody(float3 samplePoint, float2 uv, float3 d, float thickness,
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

    /* To mimick multiple scattering, tweak the integration along the
     * view ray. */
    float dot_L_d = dot(L, d);
    float msRampdown = saturate(1 - dot_L_d);
    float msAmount = (1-_cloudMSAmount) * msRampdown;
    float3 T_cloud = max(1 - exp(-thickness * density * absorptionCoefficients), (1 - exp(-thickness * density * absorptionCoefficients * _cloudMSBias) * msAmount));

    /* We can allow ourselves an extremely limited sort of attenuation due to
     * self-shadowing. */
    float projLY = dot(L, float3(0, 1, 0));
    float3 LYComponent = projLY * float3(0, 1, 0);
    float2 lightProjected = (L - LYComponent).xz;
    float selfShadowDensity = 0;
    int numSteps = 2;
    for (int i = 0; i < numSteps; i++) {
      selfShadowDensity += computeDensity2DLowLOD(frac(uv + pow(4, i) * 0.01 * lightProjected), 0);
    }
    /* HACK: 2 is just a magic number here. Should really tie these to actual distances
     * instead of just marching along UV's. */
    float distance = (1-abs(projLY)) * thickness * 2;
    selfShadowDensity = (density * selfShadowDensity) / numSteps;
    float3 selfShadow = exp(-absorptionCoefficients*distance*selfShadowDensity);

    result = selfShadow * T_cloud
      * (T_sampleL * lightColor * scatteringCoefficients/absorptionCoefficients);

    /* Apply the phase function. */
    float phase = cloudPhaseFunction(dot_L_d, _cloudAnisotropy, _cloudSilverIntensity, _cloudSilverSpread);
    result *= phase;
  }
  return result;
}

float3 lightCloudLayerPlaneGeometry(float3 samplePoint, float2 uv, float3 d, float thickness,
  float density, float3 absorptionCoefficients, float3 scatteringCoefficients) {
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    float3 lightColor = _bodyLightColor[i].xyz;
    color += lightCloudLayerPlaneGeometryBody(samplePoint, uv, d, thickness,
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
  float2 uv = float2(u, v);

  /* Sample the density. */
  float noise = computeDensity2DHighLOD(uv, 0);

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
    result.color = lightCloudLayerPlaneGeometry(samplePoint, uv, d, thickness, density,
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

CloudShadingResult lightCloudLayerBoxVolumeGeometry(float3 startPoint, float3 d, float dist,
  float2 xExtent, float2 yExtent, float2 zExtent) {
  CloudShadingResult result = cloudNoIntersectionResult();
  result.t_hit = dist; /* Initialize at dist and decrease if necessary. */
  result.transmittance = float3(1, 1, 1);
  result.color = float3(0, 0, 0);
  /* HACK: for now, fixed number of samples. */
  int numSamples = 8;
  for (int i = 0; i < numSamples; i++) {
    float t = ((i + 0.5) / numSamples) * dist;
    float3 sample = startPoint + d * t;
    float u = (sample.x - xExtent.x) / (xExtent.y - xExtent.x);
    float v = (sample.y - yExtent.x) / (yExtent.y - yExtent.x);
    float w = (sample.z - zExtent.x) / (zExtent.y - zExtent.x);
    /* HACK: for now, compute some noise. */
    float density = 1050 * worley3D(float3(u, v, w), float3(64, 4, 64)).result;
    /* Compute optical depth for this sample. */
    float opticalDepth = density * (dist/numSamples);
    /* TODO: sebh better integration? */
    result.transmittance *= exp(8e-6 * opticalDepth);
    /* Compute lighting thru transmittance. */
    result.color += result.transmittance * 10000 * 4e-6 * opticalDepth;
  }
  return result;
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
  result.hit = true;

  /* Light the clouds. HACK */
  bool debug = false;
  if (debug) {
    result.color = 1000;
    result.transmittance = float3(0, 0, 0);
    result.t_hit = t_hit.x;
  } else {
    CloudShadingResult litResult = lightCloudLayerBoxVolumeGeometry(O + d * t_hit.x, d,
      t_hit.y - t_hit.x, xExtent, yExtent, zExtent);
    result.color = litResult.color;
    result.transmittance = litResult.transmittance;
    result.t_hit = litResult.t_hit + t_hit.x;
  }

  /* TODO: compute the blend to t_hit. */
  result.blend = 1;

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
