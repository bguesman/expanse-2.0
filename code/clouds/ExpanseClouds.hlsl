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

float henyeyGreensteinPhase(float dLd, float e) {
  return ((1 - e * e) / pow(1 + e * e - 2 * e * dLd, 3.0/2.0)) / (4 * PI);
}

float cloudPhaseFunction(float dot_L_d, float cloudAnisotropy, float cloudSilverIntensity, float cloudSilverSpread) {
  return max(henyeyGreensteinPhase(dot_L_d, cloudAnisotropy), cloudSilverIntensity * henyeyGreensteinPhase(dot_L_d, 0.99 - cloudSilverSpread));
}









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
    int numSteps = 2;
    for (int i = 0; i < numSteps; i++) {
      selfShadowDensity += computeDensity2DLowLOD(frac(uv + pow(4, i) * 0.01 * lightProjected), 0);
    }
    /* HACK: 2 is just a magic number here. Should really tie these to actual distances
     * instead of just marching along UV's. */
    float distance = (1-abs(projLY)) * thickness * 2;
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

/******************************************************************************/
/********************************** END 2D ************************************/
/******************************************************************************/







/******************************************************************************/
/************************************ 3D **************************************/
/******************************************************************************/

float3 positionToUVBoxVolume(float3 position, float2 xExtent, float2 yExtent,
  float2 zExtent) {
  float3 minimum = float3(xExtent.x, yExtent.x, zExtent.x);
  float3 maximum = float3(xExtent.y, yExtent.y, zExtent.y);
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
  float3 detailUV = frac(frac(uv * _cloudDetailTile) - detailWarpNoise * _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX);

  /* Remap the base noise according to coverage. */
  float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
    frac(uv.xz * _cloudCoverageTile), mipLevel).x;
  float baseNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseNoise3D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;
  float noise = max(0, remap(saturate(baseNoise), _cloudCoverageIntensity * saturate(coverageNoise), 1.0, 0.0, 1.0));

  /* Compute the height gradient and remap accordingly. TODO: doesn't seem quite right. */
  float heightGradient = saturate(remap(uv.y, 0, 0.1, 0, 1)) * saturate(remap(uv.y, 0.25, 1, 1, 0));
  noise *= heightGradient;

  /* Remap that result using the tiled structure noise. */
  float structureNoise = SAMPLE_TEXTURE3D_LOD(_cloudStructureNoise3D, s_linear_repeat_sampler,
    frac(uv * _cloudStructureTile), mipLevel).x;
  noise = max(0, remap(noise, _cloudStructureIntensity * structureNoise, 1.0, 0.0, 1.0));

  /* Finally, remap that result using the tiled detail noise. */
  float detailNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailNoise3D, s_linear_repeat_sampler,
    detailUV, mipLevel).x;
  noise = max(0, remap(noise, _cloudDetailIntensity * detailNoise, 1.0, 0.0, 1.0));

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
  float noise = max(0, remap(saturate(baseNoise), _cloudCoverageIntensity * saturate(coverageNoise), 1.0, 0.0, 1.0));

  /* Compute the height gradient and remap accordingly. TODO: doesn't seem quite right. */
  float heightGradient = saturate(remap(uv.y, 0, 0.1, 0, 1)) * saturate(remap(uv.y, 0.25, 1, 1, 0));
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
  float density, float3 absorptionCoefficients, float3 uvw, float2 xExtent, float2 yExtent,
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
  float shadowBlur = computeShadowBlur(r, mu, 0.002, 0.1);

  /* Use the "powdered sugar" hack to get some detail around the edges. */
  float powderedSugar = saturate(0.05 + pow(density/(0.1*densityModifier), remap(uvw.y, 0.2, 0.8, 0.5, 2)));

  /* Finally, raymarch toward the light and accumulate self-shadowing. */
  const int numShadowSamples = 4;
  float marchedDist = 0;
  float opticalDepth = 0;
  for (int i = 0; i < numShadowSamples; i++) {
    float t = pow(5, i);
    float ds = t + marchedDist;
    float3 shadowSamplePoint = samplePoint + t;
    if (boundsCheck(shadowSamplePoint.x, xExtent) &&
        boundsCheck(shadowSamplePoint.y, yExtent) &&
        boundsCheck(shadowSamplePoint.z, zExtent)) {
      float3 uvwShadow = positionToUVBoxVolume(shadowSamplePoint,
        xExtent, yExtent, zExtent);
      float shadowSample = computeDensity3DHighLOD(uvwShadow, 0);
      opticalDepth += t * shadowSample;
    }
  }
  opticalDepth *= densityModifier;
  float3 shadowT = computeMSModifiedTransmittance(absorptionCoefficients,
    opticalDepth, _cloudMSAmount, _cloudMSBias);

  return shadowT * T_sampleL * shadowBlur * powderedSugar;
}

float3 lightCloudLayerBoxVolumeGeometry(float3 samplePoint, float3 d, float3
  transmittance, float3 totalTransmittance, float densityModifier, float density,
  float3 absorptionCoefficients, float3 scatteringCoefficients, float3 uvw, float2 xExtent,
  float2 yExtent, float2 zExtent) {
  int i; // Loop variable

  float3 color = float3(0, 0, 0); // Final result.

  /* Precompute the phase function for all bodies. */
  float lightPhases[MAX_BODIES];
  for (i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    lightPhases[i] = cloudPhaseFunction(dot(L, d), _cloudAnisotropy,
      _cloudSilverIntensity, _cloudSilverSpread);
  }

  /* Light the clouds according to each body. */
  for (i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    /* Check occlusion. */
    SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint, L,
      _planetRadius, _atmosphereRadius);
    if (!lightIntersection.groundHit) {
      /* Get the body luminance. */
      float3 luminance = _bodyLightColor[i].xyz
        * getVolumetricShadowBoxVolumeGeometry(samplePoint, d, L, lightIntersection,
          densityModifier, density, absorptionCoefficients, uvw, xExtent, yExtent, zExtent);

      /* Integrate the in-scattered luminance. */
      float3 inScatter = scatteringCoefficients * density
        * (luminance - luminance * transmittance)
        / max(0.000001, density * absorptionCoefficients);

      color += totalTransmittance * inScatter * lightPhases[i];
    }
  }

  return color;
}

CloudShadingResult raymarchCloudLayerBoxVolumeGeometry(float3 startPoint, float3 d, float dist,
  float2 xExtent, float2 yExtent, float2 zExtent, float density,
  float3 absorptionCoefficients, float3 scatteringCoefficients) {

  /* Final result. */
  CloudShadingResult result = cloudNoIntersectionResult();
  result.transmittance = float3(1, 1, 1);
  result.color = float3(0, 0, 0);
  result.t_hit = dist; /* Initialize at dist and decrease if necessary. */
  result.hit = true;
  float3 totalLightingTransmittance = float3(1, 1, 1);

  /* Constants that could be tweakable. */
  const float detailStep = max(1.0/256.0, 1.0/(256.0 * (dist/20000)));
  const float coarseStep = 1.0/32.0;

  /* Marching state. */
  float marchedFraction = 0;
  float stepSize = coarseStep;
  int consecutiveZeroSamples = 0;

  while (marchedFraction < 1 && averageFloat3(result.transmittance) > 0.0001) {

    /* Switch back to coarse marching if we've taken enough zero samples. */
    if (consecutiveZeroSamples > 10) {
      consecutiveZeroSamples = 0;
      stepSize = coarseStep;
    }

    /* March coarse. */
    if (floatEq(stepSize, coarseStep)) {
      /* Sample low LOD density. */
      float t = (marchedFraction + stepSize/2.0) * dist;
      float3 samplePoint = startPoint + d * t;
      float3 sampleUVW = positionToUVBoxVolume(samplePoint, xExtent, yExtent, zExtent);
      float coarseDensity = computeDensity3DLowLOD(sampleUVW, 0);
      if (coarseDensity < 0.00000001) {
        /* Keep marching coarse. */
        marchedFraction += stepSize;
        continue;
      }
      /* Switch to detail march. */
      stepSize = detailStep;
    }

    /* Otherwise, march detail. */
    float t = (marchedFraction + (stepSize/2.0)) * dist;
    float3 samplePoint = startPoint + d * t;
    float3 sampleUVW = positionToUVBoxVolume(samplePoint, xExtent, yExtent, zExtent);
    float detailDensity = computeDensity3DHighLOD(sampleUVW, 0);

    if (detailDensity == 0) {
      /* Skip and note that we did. */
      marchedFraction += stepSize;
      consecutiveZeroSamples++;
    }
    consecutiveZeroSamples = 0;

    /* Compute the optical depth. */
    detailDensity *= density;
    float opticalDepth = detailDensity * stepSize * dist;

    /* Compute the transmittance and accumulate. */
    float3 sampleTransmittance = exp(-absorptionCoefficients * opticalDepth);
    float3 lightingTransmittance = computeMSModifiedTransmittance(absorptionCoefficients,
      opticalDepth, _cloudMSAmount, _cloudMSBias);
    result.transmittance *= sampleTransmittance;
    totalLightingTransmittance *= lightingTransmittance;

    /* Light the clouds. */
    result.color += lightCloudLayerBoxVolumeGeometry(samplePoint, d, lightingTransmittance,
      totalLightingTransmittance, density, detailDensity, absorptionCoefficients,
      scatteringCoefficients, sampleUVW, xExtent, yExtent, zExtent); // TODO: extinction needs to be cased out according to ms approximation

    /* If transmittance is less than 0.5, write t_hit for the blend. */
    if (result.t_hit > dist-0.01 && averageFloat3(result.transmittance) < 0.5) {
      result.t_hit = t;
    }

    marchedFraction += stepSize;
    consecutiveZeroSamples++;
  }

  return result;
}

// CloudShadingResult lightCloudLayerBoxVolumeGeometry(float3 startPoint, float3 d, float dist,
//   float2 xExtent, float2 yExtent, float2 zExtent, float density,
//   float3 absorptionCoefficients, float3 scatteringCoefficients) {
//   CloudShadingResult result = cloudNoIntersectionResult();
//   result.t_hit = dist; /* Initialize at dist and decrease if necessary. */
//   result.transmittance = float3(1, 1, 1);
//   result.color = float3(0, 0, 0);
//   /* HACK: for now, fixed number of samples. */
//   bool highLOD = false;
//   const float detailStep = 1.0/64.0;
//   const float coarseStep = 1.0/32.0;
//   float stepSize = coarseStep;
//   float marchedFraction = 0;
//   int numSamples = 0;
//   int maxNumSamples = 512;
//   int consecutiveDetailZeroSamples = 0;
//   float3 totalClampedTransmittance = float3(1, 1, 1);
//   while (marchedFraction < 1 && numSamples < maxNumSamples && dot(result.transmittance, float3(1, 1, 1)/3) > 0.001) {//dot(result.transmittance, float3(1, 1, 1)/3) > 0.001 && numSamples < maxNumSamples && marchedFraction < 1) {
//     if (consecutiveDetailZeroSamples == 10) {
//       consecutiveDetailZeroSamples = 0;
//       highLOD = false;
//       stepSize = coarseStep;
//     }
//     if (!highLOD) {
//       float t = ((marchedFraction + stepSize * random_3_1(d * _tick))) * dist;
//       float3 samplePoint = startPoint + d * t;
//       float u = (samplePoint.x - xExtent.x) / (xExtent.y - xExtent.x);
//       float v = (samplePoint.y - yExtent.x) / (yExtent.y - yExtent.x);
//       float w = (samplePoint.z - zExtent.x) / (zExtent.y - zExtent.x);
//       float3 uvw = float3(u, v, w);
//       float testDensity = max(0, computeDensity3DLowLOD(uvw, 0));
//       if (testDensity == 0) {
//         numSamples++;
//         marchedFraction += stepSize;
//         continue;
//       } else {
//         stepSize = detailStep;
//       }
//     }
//     float t = (marchedFraction + (stepSize * random_3_1(d * _tick))) * dist;
//     float3 samplePoint = startPoint + d * t;
//     float u = (samplePoint.x - xExtent.x) / (xExtent.y - xExtent.x);
//     float v = (samplePoint.y - yExtent.x) / (yExtent.y - yExtent.x);
//     float w = (samplePoint.z - zExtent.x) / (zExtent.y - zExtent.x);
//     float3 uvw = float3(u, v, w);
//     float fullDensity = density * max(0, computeDensity3DHighLOD(uvw, 0));
//     if (fullDensity < 0.001) {
//       consecutiveDetailZeroSamples++;
//     } else {
//       consecutiveDetailZeroSamples = 0;
//     }
//     /* Compute optical depth for this sample. */
//     float opticalDepth = fullDensity * (dist * stepSize);
//     /* TODO: sebh better integration? */
//     // float dot_L_d = dot(L, d);
//     // float msRampdown = saturate(1 - dot_L_d);
//     // float msAmount = (_cloudMSAmount) * msRampdown;
//     float3 regularTransmittance = exp(-absorptionCoefficients * opticalDepth);
//     float3 clampedTransmittance = max(regularTransmittance,  exp(-absorptionCoefficients * opticalDepth * _cloudMSBias) * _cloudMSAmount);
//     result.transmittance *= regularTransmittance;
//     totalClampedTransmittance *= clampedTransmittance;
//     /* Compute lighting thru transmittance. */
//     for (int j = 0; j < _numActiveBodies; j++) {
//       float3 L = _bodyDirection[j];
//       float3 lightColor = _bodyLightColor[j].xyz;
//
//       /* Do an occlusion check for lighting. */
//       SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint, L,
//         _planetRadius, _atmosphereRadius);
//       /* TODO: we need to fudge this because it looks bad having a
//        * flat line between shadow and no shadow. */
//       if (!lightIntersection.groundHit) {
//         /* HACK: to get rid of the super intense shadow/lit split we get from the
//          * intersection, test how close we are to the horizon line. It may
//          * be better to do this the other way around---push more light into
//          * clouds that are technically occluded. */
//         float r = length(samplePoint);
//         float mu = dot(normalize(samplePoint), L);
//         float h = r - _planetRadius;
//         float cos_h = -safeSqrt(h * (2 * _planetRadius + h)) / (_planetRadius + h);
//         float shadowBlur = 1 - pow(saturate(0.001 / abs(cos_h - mu)), 0.25);
//
//         float t_lightHit = lightIntersection.endT - lightIntersection.startT;
//         float2 lightTransmittanceCoord = mapSky2DCoord(r, mu,
//           _atmosphereRadius, _planetRadius, t_lightHit,
//           false, _resT.y);
//
//         /* TODO: probably unnecessary to compute analytical transmittance here, since
//          * we will unlikely be in a height fog layer and also in a cloud. */
//         float3 T_sampleL = shadowBlur * exp(sampleSkyTTextureRaw(lightTransmittanceCoord));
//
//         /* Perform volumetric shadow traces. */
//         float shadowOpticalDepth = 0;
//         int numShadowSamples = 15;
//         float shadowMarchedDist = 0;
//         for (int k = 0; k < numShadowSamples; k++) {
//           float stepSize = pow(2, k);
//           float3 volumetricShadowPoint = samplePoint + (shadowMarchedDist + stepSize * random_3_1(_tick * d)) * L;
//           if (boundsCheck(volumetricShadowPoint.x, xExtent) &&
//               boundsCheck(volumetricShadowPoint.y, yExtent) &&
//               boundsCheck(volumetricShadowPoint.z, zExtent)) {
//             float uShadow = (volumetricShadowPoint.x - xExtent.x) / (xExtent.y - xExtent.x);
//             float vShadow = (volumetricShadowPoint.y - yExtent.x) / (yExtent.y - yExtent.x);
//             float wShadow = (volumetricShadowPoint.z - zExtent.x) / (zExtent.y - zExtent.x);
//             float3 uvwShadow = float3(uShadow, vShadow, wShadow);
//
//             shadowOpticalDepth += stepSize * max(0, computeDensity3DHighLOD(uvwShadow, 0));
//           }
//           shadowMarchedDist += stepSize;
//         }
//         shadowOpticalDepth *= density;
//         float3 shadowT = max(exp(-absorptionCoefficients * shadowOpticalDepth), exp(-absorptionCoefficients * shadowOpticalDepth * _cloudMSBias) * _cloudMSAmount);
//
//         float3 powderedSugar = saturate(0.05 + pow(computeDensity3DHighLOD(uvw, 0)*5, remap(1-uvw.y, 0.15, 1, 0.5, 2)));
//
//         /* TODO: Can compute outside if we store color for each light. */
//         float phase = cloudPhaseFunction(dot(L, d), _cloudAnisotropy, _cloudSilverIntensity, _cloudSilverSpread);
//
//         float3 luminance = powderedSugar * shadowT * T_sampleL * phase * shadowBlur * lightColor;
//         float3 integratedScattering = scatteringCoefficients * opticalDepth * (luminance - luminance * clampedTransmittance) / (max(0.00001, absorptionCoefficients * opticalDepth * _cloudMSBias));
//         result.color += totalClampedTransmittance * integratedScattering;
//       }
//     }
//
//     /* Set t_hit once monochrome transmittance is less than 0.5. */
//     if (floatGT(result.t_hit, dist - 0.001) && dot(result.transmittance, float3(1, 1, 1)/3) < 0.5) {
//       result.t_hit = t;
//     }
//
//     numSamples++;
//     marchedFraction += stepSize;
//   }
//   return result;
// }

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

  /* If we're inside the cloud volume, just set the start t to zero. */
  t_hit.x = max(0, t_hit.x);

  result.hit = true;

  /* Light the clouds. */
  CloudShadingResult litResult = raymarchCloudLayerBoxVolumeGeometry(O + d * t_hit.x, d,
    t_hit.y - t_hit.x, xExtent, yExtent, zExtent, _cloudDensity[i],
    _cloudAbsorptionCoefficients[i].xyz, _cloudScatteringCoefficients[i].xyz);
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
