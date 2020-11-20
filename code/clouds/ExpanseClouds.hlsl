#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseNoise.hlsl"
#include "../sky/ExpanseSkyMapping.hlsl"
#include "ExpanseCloudsGeometry.hlsl"
#include "ExpanseCloudsGeometry.hlsl"



/******************************************************************************/
/************************************ 2D **************************************/
/******************************************************************************/

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
    int numSteps = 3;
    for (int i = 0; i < numSteps; i++) {
      float shadowSample = (pow(3, i) * 0.01);
      selfShadowDensity += shadowSample * computeDensity2DHighLOD(frac(uv + shadowSample * lightProjected), (i+1)*3);
    }
    /* HACK: 8 is just a magic number here. Should really tie these to actual distances
     * instead of just marching along UV's. */
    float distance = (1-abs(projLY)) * thickness * 800;
    selfShadowDensity = (density * selfShadowDensity) / numSteps;
    float3 selfShadow = max(exp(-absorptionCoefficients*distance*selfShadowDensity), exp(-absorptionCoefficients*distance*selfShadowDensity * _cloudMSBias) * msAmount);

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
    result.color = noise * 10000;
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

/******************************************************************************/
/********************************** END 2D ************************************/
/******************************************************************************/







/******************************************************************************/
/************************************ 3D **************************************/
/******************************************************************************/

float3 positionToUVBoxVolume(float3 position, float2 xExtent, float2 yExtent,
  float2 zExtent) {
  // y uv coordinate spans distance of x extent, so we can store textures
  // as a cube
  float3 minimum = float3(xExtent.x, yExtent.x, zExtent.x);
  float3 maximum = float3(xExtent.y, yExtent.x + (xExtent.y - xExtent.x), zExtent.y);
  return (position - minimum) / (maximum - minimum);
}

float3 computeMSModifiedTransmittance(float3 absorptionCoefficients,
  float opticalDepth, float MSAmount, float MSBias) {
  return max(exp(-absorptionCoefficients * opticalDepth), exp(-absorptionCoefficients * opticalDepth * MSBias) * MSAmount);
}

float computeDensity3DHighLOD(float3 uv, int mipLevel) {
  /* NOTE/TODO: we HAVE to use the linear repeat sampler to avoid seams in
   * the tileable textures! */

  /* Get the warp noises, use them to advect the texture coordinates. */
  float3 baseWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseWarpNoise3D, s_linear_repeat_sampler,
    frac(uv * _cloudBaseWarpTile), mipLevel).xyz;
  float3 detailWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailWarpNoise3D, s_linear_repeat_sampler,
    frac(uv * _cloudDetailWarpTile), mipLevel).xyz;
  float3 baseUV = frac(frac(uv * _cloudBaseTile) - baseWarpNoise * _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX);
  float3 detailUV = frac(frac(uv*_cloudDetailTile) - detailWarpNoise * _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX);

  /* Remap the base noise according to coverage. */
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
    frac(uv.xz * _cloudCoverageTile), mipLevel).x;
  float baseNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseNoise3D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;
  float noise = max(0, remap(saturate(baseNoise), min(0.99, max(0.0, _cloudCoverageIntensity * coverageNoise * 2)), 1.0, 0.0, 1.0));

  /* Compute the height gradient and remap accordingly. HACK: should be based on x/y ratio */
  float heightGradient = saturate(remap(uv.y, 0, 0.01, 0, 1)) * saturate(remap(uv.y, 0.01, 0.2, 1, 0));
  noise *= heightGradient;

  /* Remap that result using the tiled structure noise. */
  float structureNoise = SAMPLE_TEXTURE3D_LOD(_cloudStructureNoise3D, s_linear_repeat_sampler,
    frac(uv * _cloudStructureTile), mipLevel).x;
  noise = max(0, remap(noise, _cloudStructureIntensity * structureNoise, 1.0, 0.0, 1.0));

  /* Finally, remap that result using the tiled detail noise. */
  float detailNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailNoise3D, s_linear_repeat_sampler,
    detailUV, mipLevel).x;
  noise = max(0, remap(noise, _cloudDetailIntensity * detailNoise, 1.0, 0.0, 1.0));

  noise = max(0, remap(noise, _cloudBaseWarpIntensity * baseWarpNoise, 1.0, 0.0, 1.0));

  return noise;
}

float computeDensity3DLowLOD(float3 uv, int mipLevel) {
  /* Get the warp noises, use them to advect the texture coordinates. */
  float3 baseWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseWarpNoise3D, s_linear_repeat_sampler,
    frac(uv * _cloudBaseWarpTile), mipLevel).xyz;
  float3 detailWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailWarpNoise3D, s_linear_repeat_sampler,
    frac(uv * _cloudDetailWarpTile), mipLevel).xyz;
  float3 baseUV = frac(frac(uv * _cloudBaseTile) - baseWarpNoise * _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX);
  float3 detailUV = frac(frac(uv * _cloudDetailTile) - detailWarpNoise * _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX);

  /* Remap the base noise according to coverage. */
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
    frac(uv.xz * _cloudCoverageTile), mipLevel).x;
  float baseNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseNoise3D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;
  float noise = max(0, remap(saturate(baseNoise), min(0.99, max(0.0, _cloudCoverageIntensity * coverageNoise * 2)), 1.0, 0.0, 1.0));

  /* Compute the height gradient and remap accordingly. HACK: should be based on x/y ratio */
  float heightGradient = saturate(remap(uv.y, 0, 0.01, 0, 1)) * saturate(remap(uv.y, 0.01, 0.2, 1, 0));
  noise *= heightGradient;
  return noise;
}

float computeShadowBlur(float r, float mu, float thickness, float sharpness) {
  float h = r - _planetRadius;
  float cos_h = -safeSqrt(h * (2 * _planetRadius + h)) / (_planetRadius + h);
  return 1 - pow(saturate(thickness / abs(cos_h - mu)), sharpness);
}

float3 getVolumetricShadowBoxVolumeGeometry(float3 samplePoint, float3 d,
  float3 L, SkyIntersectionData lightIntersection, float densityModifier,
  float density, float noiseVal, float3 absorptionCoefficients, float3 uvw, float2 xExtent, float2 yExtent,
  float2 zExtent) {
  /* Compute the transmittance through the atmosphere. */
  float r = length(samplePoint);
  float mu = dot(normalize(samplePoint), L);
  float t_lightHit = lightIntersection.endT - lightIntersection.startT;
  float2 lightTransmittanceCoord = mapSky2DCoord(r, mu,
    _atmosphereRadius, _planetRadius, t_lightHit,
    false, _resT.y);
  float3 T_sampleL = exp(sampleSkyTTextureRaw(lightTransmittanceCoord));

  /* To soften the shadow due to the horizon line, blur the occlusion. */
  float shadowBlur = computeShadowBlur(r, mu, 0.001, 0.25);

  /* Use the "powdered sugar" hack to get some detail around the edges.
   * TODO: probably tweakable. */
  /* HACK: both should be based on x/y ratio instead of raw uvw.y */
  float depthProbability = saturate(0.05 + pow(saturate(noiseVal * 3), remap(clamp(uvw.y*3+0.2, 0.0, 0.5), 0.0, 0.5, 0.5, 2)));
  float verticalProbability = pow(remap(clamp(uvw.y*3, 0.0, 0.5), 0.0, 0.5, 0.1, 1.0), 0.8);

  /* Finally, raymarch toward the light and accumulate self-shadowing. */
  const int numShadowSamples = 5;
  float marchedDist = 0;
  float opticalDepth = 0;
  float randomOffset = 0.5;//random_3_1(d + _tick * 0.1);
  for (int i = 0; i < numShadowSamples; i++) {
    float t = pow(6, i);
    float ds = t + marchedDist;
    float3 shadowSamplePoint = samplePoint + L * (t * randomOffset);
    if (boundsCheck(shadowSamplePoint.x, xExtent) &&
        boundsCheck(shadowSamplePoint.y, yExtent) &&
        boundsCheck(shadowSamplePoint.z, zExtent)) {
      float3 uvwShadow = positionToUVBoxVolume(shadowSamplePoint,
        xExtent, yExtent, zExtent);
      float shadowSample = computeDensity3DHighLOD(uvwShadow, 0); // for some reason, can't sample mipmaps
      opticalDepth += t * shadowSample;
    }
  }
  opticalDepth *= densityModifier;
  float3 shadowT = computeMSModifiedTransmittance(absorptionCoefficients,
    opticalDepth, _cloudMSAmount, _cloudMSBias);

  return T_sampleL * shadowBlur * shadowT * depthProbability * verticalProbability;
}

float3 lightCloudLayerBoxVolumeGeometry(float3 samplePoint, float3 d, float3
  transmittance, float3 totalTransmittance, float densityModifier, float density,
  float noiseVal, float3 absorptionCoefficients, float3 scatteringCoefficients, float3 uvw, float2 xExtent,
  float2 yExtent, float2 zExtent) {
  int i; // Loop variable

  float3 color = float3(0, 0, 0); // Final result.

  d = normalize(d);

  /* Precompute the phase function for all bodies. */
  float lightPhases[MAX_BODIES];
  for (i = 0; i < _numActiveBodies; i++) {
    float3 L = normalize(_bodyDirection[i]);
    lightPhases[i] = cloudPhaseFunction(clampCosine(dot(L, d)), _cloudAnisotropy,
      _cloudSilverIntensity, _cloudSilverSpread);
  }

  /* Light the clouds according to each body. */
  for (i = 0; i < _numActiveBodies; i++) {
    float3 L = normalize(_bodyDirection[i]);
    /* Check occlusion. */
    SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint, L,
      _planetRadius, _atmosphereRadius);
    if (!lightIntersection.groundHit) {
      /* Get the body luminance. */
      float3 luminance = _bodyLightColor[i].xyz
        * getVolumetricShadowBoxVolumeGeometry(samplePoint, d, L, lightIntersection,
          densityModifier, density, noiseVal, absorptionCoefficients, uvw, xExtent, yExtent, zExtent);

      /* Integrate the in-scattered luminance. */
      float3 inScatter = scatteringCoefficients * density
        * (luminance - luminance * transmittance)
        / max(0.000001, density * absorptionCoefficients);

      color += totalTransmittance * inScatter * 5 * lightPhases[i];
    }
  }

  /* HACK: ambient */
  // color += totalTransmittance * scatteringCoefficients * density * 40000 * float3(0.5, 0.7, 0.9);

  return color;
}

CloudShadingResult raymarchCloudLayerBoxVolumeGeometry(float3 startPoint, float3 d, float dist,
  float2 xExtent, float2 yExtent, float2 zExtent, float density,
  float3 absorptionCoefficients, float3 scatteringCoefficients, float distToStart) {

  /* Final result. */
  CloudShadingResult result = cloudNoIntersectionResult();
  result.transmittance = float3(1, 1, 1);
  result.color = float3(0, 0, 0);
  result.t_hit = dist; /* Initialize at dist and decrease if necessary. */
  result.hit = true;
  float3 totalLightingTransmittance = float3(1, 1, 1);

  /* Constants that could be tweakable. */
  const float detailStep = 50/dist;// max(10, 10 * distToStart/2000)/dist;
  const float coarseStep = 200/dist;//max(200, 200 * distToStart/5000)/dist;

  /* Marching state. */
  float marchedFraction = 0;
  float stepSize = coarseStep;
  int consecutiveZeroSamples = 0;

  int samples = 0;
  const int maxNumSamples = 256;

  float randomOffset = random_3_1(d * + 0.001 * _tick);

  while (marchedFraction < 1 && averageFloat3(result.transmittance) > 0.001 && samples < maxNumSamples) {

    /* Switch back to coarse marching if we've taken enough zero samples. */
    if (consecutiveZeroSamples > 10) {
      consecutiveZeroSamples = 0;
      stepSize = coarseStep;
    }

    /* March coarse. */
    if (floatEq(stepSize, coarseStep)) {
      /* Sample low LOD density. */
      float t = (marchedFraction + stepSize * randomOffset) * dist;
      float3 samplePoint = startPoint + d * t;
      float3 sampleUVW = positionToUVBoxVolume(samplePoint, xExtent, yExtent, zExtent);
      float coarseDensity = computeDensity3DLowLOD(sampleUVW, 0);
      if (coarseDensity < 0.00000001) {
        /* Keep marching coarse. */
        marchedFraction += stepSize;
        samples++;
        continue;
      }
      /* Switch to detail march, backtracking a step. */
      stepSize = detailStep;
    }

    /* Otherwise, march detail. */
    float t = (marchedFraction + stepSize * randomOffset) * dist;
    float3 samplePoint = startPoint + d * t;
    float3 sampleUVW = positionToUVBoxVolume(samplePoint, xExtent, yExtent, zExtent);
    float detailDensityNoise = computeDensity3DHighLOD(sampleUVW, 0);

    if (detailDensityNoise == 0) {
      /* Skip and note that we did. */
      marchedFraction += stepSize;
      consecutiveZeroSamples++;
      samples++;
    }
    consecutiveZeroSamples = 0;

    /* Compute the optical depth. */
    float detailDensity = density * detailDensityNoise;
    float opticalDepth = detailDensity * stepSize * dist;

    /* Compute the transmittance and accumulate. */
    float3 sampleTransmittance = exp(-absorptionCoefficients * opticalDepth);
    // float3 lightingTransmittance = computeMSModifiedTransmittance(absorptionCoefficients,
      // opticalDepth, _cloudMSAmount, _cloudMSBias);
    result.transmittance *= sampleTransmittance;
    // totalLightingTransmittance *= lightingTransmittance;

    /* Light the clouds. */
    result.color += lightCloudLayerBoxVolumeGeometry(samplePoint, d, sampleTransmittance,
      result.transmittance, density, detailDensity, detailDensityNoise, absorptionCoefficients,
      scatteringCoefficients, sampleUVW, xExtent, yExtent, zExtent); // TODO: extinction needs to be cased out according to ms approximation

    /* If transmittance is less than 0.5, write t_hit for the blend. */
    if (result.t_hit > dist-0.01 && averageFloat3(result.transmittance) < 0.5) {
      result.t_hit = t;
    }

    marchedFraction += stepSize;
    samples++;
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
  float2 t_hit = intersectAxisAlignedBoxVolume(O, d, xExtent, yExtent, zExtent);

  /* We hit nothing. */
  if (t_hit.x < 0 && t_hit.y < 0) {
    return cloudNoIntersectionResult();
  }

  /* If we're inside the cloud volume, just set the start t to zero. */
  t_hit.x = max(0, t_hit.x);

  result.hit = true;

  /* Light the clouds. */
  CloudShadingResult litResult = raymarchCloudLayerBoxVolumeGeometry(O + d * t_hit.x, d,
    t_hit.y - t_hit.x, xExtent, yExtent, zExtent, _cloudDensity[i],
    _cloudAbsorptionCoefficients[i].xyz, _cloudScatteringCoefficients[i].xyz,
    t_hit.x);
  result.color = litResult.color;
  result.transmittance = litResult.transmittance;
  result.t_hit = litResult.t_hit + t_hit.x;

  /* TODO: compute the blend to t_hit. */
  float2 oToSample = mapSky2DCoord(length(O), dot(normalize(O), d),
    _atmosphereRadius, _planetRadius, skyIntersection.endT,
    skyIntersection.groundHit, _resT.y);
  float3 samplePoint = O + d * result.t_hit;
  float2 sampleOut = mapSky2DCoord(length(samplePoint), dot(normalize(samplePoint), d),
    _atmosphereRadius, _planetRadius, skyIntersection.endT - result.t_hit,
    skyIntersection.groundHit, _resT.y);
  float3 t_oToSample = sampleSkyTTextureRaw(oToSample);
  float3 t_sampleOut = sampleSkyTTextureRaw(sampleOut);
  float3 blendTransmittanceColor = exp(t_oToSample - max(t_oToSample, t_sampleOut)
   + computeTransmittanceDensityAttenuation(O, d, t_hit));
  result.blend = dot(blendTransmittanceColor, float3(1, 1, 1) / 3.0); // TODO: put back

  return result;
}

/******************************************************************************/
/********************************** END 3D ************************************/
/******************************************************************************/









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
