#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseNoise.hlsl"
#include "../sky/ExpanseSkyMapping.hlsl"
#include "ExpanseCloudsGeometry.hlsl"
#include "ExpanseCloudsCommon.hlsl"



/******************************************************************************/
/************************************ 2D **************************************/
/******************************************************************************/

float3 lightCloudLayer2D(float3 p, float3 d, ICloudGeometry geometry, float density, int i) {
  float3 cumulativeLighting = float3(0, 0, 0);
  /* Loop over all celestial bodies. */
  for (int l = 0; l < _numActiveBodies; l++) {
    float3 L = _bodyDirection[l];
    float3 lightColor = _bodyLightColor[l].xyz;

    /* Skip this body if it's occluded by the planet. */
    SkyIntersectionData lightIntersection = traceSkyVolume(p, L,
        _planetRadius, _atmosphereRadius);
    if (lightIntersection.groundHit) {
      continue;
    }

    /* For shadow blur and sky transmittance. */
    float r = length(p);
    float mu = dot(normalize(p), L);

    /* Planet shadow blur to smooth out the transition from day to night.
     * TODO: tweakable, not necessary when using self-shadowing. */
    const bool _useShadowBlur = false;
    float shadowBlur = 1;
    if (_useShadowBlur) {
      const float blurOffset = 0.1;
      const float blurDistance = 0.001;
      shadowBlur = computeShadowBlur(r, mu, blurOffset, blurDistance);
    }

    /* Transmittance through the atmosphere to the sample point. */
    float2 lightTransmittanceCoord = mapSky2DCoord(r, mu,
      _atmosphereRadius, _planetRadius, lightIntersection.endT - lightIntersection.startT,
      false, _resT.y);
    float3 skyTransmittance = exp(sampleSkyTTextureRaw(lightTransmittanceCoord));

    /* To mimick multiple scattering, tweak the integration along the view ray. */
    float dot_L_d = dot(L, d);
    float msAmount = (1 - _cloudMSAmount) * saturate(1 - dot_L_d);
    float3 cloudTransmittance = max(1 - exp(-_cloudThickness[i] * density * _cloudAbsorptionCoefficients[i].xyz),
      (1 - exp(-_cloudThickness[i] * density * _cloudAbsorptionCoefficients[i].xyz * _cloudMSBias) * msAmount));
    cloudTransmittance *= _cloudScatteringCoefficients[i].xyz/clampAboveZero(_cloudAbsorptionCoefficients[i].xyz);

    /* If we are permitted, take a few samples at increasing mip levels to get
     * some self shadowing. TODO: tweakable. */
    const bool _useSelfShadowing = true;
    float3 selfShadow = float3(1, 1, 1);
    if (_useSelfShadowing) {
      /* Figure out where the light direction would leave the
       * cloud volume. */
      float2 lightExit = geometry.intersect3D(p, L);
      // TODO: probably tweakable.
      const int numShadowSamples = 4;
      const float shadowDistance = min(1500, lightExit.y);
      for (int j = 0; j < numShadowSamples; j++) {
        float2 t_ds = generateNthPowerSampleFromIndex(j, numShadowSamples, 5);
        float3 shadowSamplePoint = p + L * t_ds.x * shadowDistance;
        if (geometry.inBounds(shadowSamplePoint)) {
          float shadowSample = takeMediaSample2DHighLOD(shadowSamplePoint, geometry, (j+1)*2);
          float opticalDepth = _cloudDensity[i] * shadowSample * t_ds.y * shadowDistance;
          float3 shadowTransmittance = computeMSModifiedTransmittance(_cloudAbsorptionCoefficients[i].xyz, opticalDepth);
          selfShadow *= shadowTransmittance;
        }
      }
    }

    /* Finally, compute the phase function. */
    float phase = cloudPhaseFunction(dot_L_d, _cloudAnisotropy, _cloudSilverIntensity, _cloudSilverSpread);

    cumulativeLighting += phase * cloudTransmittance * skyTransmittance * shadowBlur * lightColor * selfShadow;
  }
  return cumulativeLighting;
}

/**
 * Shades 2D cloud layer. Params are:
 * O: origin of the ray.
 * d: direction of the ray.
 * geometry: cloud volume geometry.
 * geoHit: whether or not there is rendered geometry where we are shading.
 * depth: how far away said rendered geometry is.
 */
CloudShadingResult shadeCloudLayer2D(float3 O, float3 d, ICloudGeometry geometry,
  SkyIntersectionData skyIntersection, bool geoHit, float depth, int i) {
  /* Intersect the cloud plane. */
  float t_hit = geometry.intersect(O, d).x;

  /* If we didn't hit the clouds, or the clouds are behind scene geometry. */
  if (t_hit < 0 || (geoHit && depth < t_hit)) {
    return cloudNoIntersectionResult();
  }

  CloudShadingResult result;
  result.t_hit = t_hit;
  result.hit = true;

  float3 p = O + d * t_hit;

  /* Transmittance. */
  float mediaSample = takeMediaSample2DHighLOD(p, geometry);
  float attenuation = geometry.densityAttenuation(p, _cloudDensityAttenuationDistance[i], _cloudDensityAttenuationBias[i]);
  float density = (attenuation * _cloudDensity[i]);
  float opticalDepth = mediaSample * _cloudThickness[i] * density;
  result.transmittance = exp(-_cloudAbsorptionCoefficients[i].xyz * opticalDepth);

  /* Lighting. */
  result.color = lightCloudLayer2D(p, d, geometry, density, i);

  /* Atmospheric blend. */
  result.blend = computeAtmosphericBlend(O, d, p, t_hit, skyIntersection);

  return result;
}

/******************************************************************************/
/********************************** END 2D ************************************/
/******************************************************************************/







/******************************************************************************/
/************************************ 3D **************************************/
/******************************************************************************/
//
// float3 positionToUVBoxVolume(float3 position, float2 xExtent, float2 yExtent,
//   float2 zExtent) {
//   // y uv coordinate spans distance of x extent, so we can store textures
//   // as a cube
//   float3 minimum = float3(xExtent.x, yExtent.x, zExtent.x);
//   float3 maximum = float3(xExtent.y, yExtent.x + (xExtent.y - xExtent.x), zExtent.y);
//   return (position - minimum) / (maximum - minimum);
// }
//
// float3 computeMSModifiedTransmittance(float3 absorptionCoefficients,
//   float opticalDepth, float MSAmount, float MSBias) {
//   return max(exp(-absorptionCoefficients * opticalDepth), exp(-absorptionCoefficients * opticalDepth * MSBias) * MSAmount);
// }
//
// float computeDensity3DHighLOD(float3 uv, int mipLevel) {
//   /* NOTE/TODO: we HAVE to use the linear repeat sampler to avoid seams in
//    * the tileable textures! */
//
//   /* Get the warp noises, use them to advect the texture coordinates. */
//   float3 baseWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseWarpNoise3D, s_linear_repeat_sampler,
//     frac(uv * _cloudBaseWarpTile), mipLevel).xyz;
//   float3 detailWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailWarpNoise3D, s_linear_repeat_sampler,
//     frac(uv * _cloudDetailWarpTile), mipLevel).xyz;
//   float3 baseUV = frac(frac(uv * _cloudBaseTile) - baseWarpNoise * _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX);
//   float3 detailUV = frac(frac(uv*_cloudDetailTile) - detailWarpNoise * _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX);
//
//   /* Remap the base noise according to coverage. */
//   float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
//     frac(uv.xz * _cloudCoverageTile), mipLevel).x;
//   float baseNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseNoise3D, s_linear_repeat_sampler,
//     baseUV, mipLevel).x;
//   float noise = max(0, remap(saturate(baseNoise), min(0.99, max(0.0, _cloudCoverageIntensity * coverageNoise * 2)), 1.0, 0.0, 1.0));
//
//   /* Compute the height gradient and remap accordingly. HACK: should be based on x/y ratio */
//   float heightGradient = saturate(remap(uv.y, 0, 0.01, 0, 1)) * saturate(remap(uv.y, 0.01, 0.2, 1, 0));
//   noise *= heightGradient;
//
//   /* Remap that result using the tiled structure noise. */
//   float structureNoise = SAMPLE_TEXTURE3D_LOD(_cloudStructureNoise3D, s_linear_repeat_sampler,
//     frac(uv * _cloudStructureTile), mipLevel).x;
//   noise = max(0, remap(noise, _cloudStructureIntensity * structureNoise, 1.0, 0.0, 1.0));
//
//   /* Finally, remap that result using the tiled detail noise. */
//   float detailNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailNoise3D, s_linear_repeat_sampler,
//     detailUV, mipLevel).x;
//   noise = max(0, remap(noise, _cloudDetailIntensity * detailNoise, 1.0, 0.0, 1.0));
//
//   noise = max(0, remap(noise, _cloudBaseWarpIntensity * baseWarpNoise, 1.0, 0.0, 1.0));
//
//   return noise;
// }
//
// float computeDensity3DLowLOD(float3 uv, int mipLevel) {
//   /* Get the warp noises, use them to advect the texture coordinates. */
//   float3 baseWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseWarpNoise3D, s_linear_repeat_sampler,
//     frac(uv * _cloudBaseWarpTile), mipLevel).xyz;
//   float3 detailWarpNoise = SAMPLE_TEXTURE3D_LOD(_cloudDetailWarpNoise3D, s_linear_repeat_sampler,
//     frac(uv * _cloudDetailWarpTile), mipLevel).xyz;
//   float3 baseUV = frac(frac(uv * _cloudBaseTile) - baseWarpNoise * _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX);
//   float3 detailUV = frac(frac(uv * _cloudDetailTile) - detailWarpNoise * _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX);
//
//   /* Remap the base noise according to coverage. */
//   float coverageNoise = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
//     frac(uv.xz * _cloudCoverageTile), mipLevel).x;
//   float baseNoise = SAMPLE_TEXTURE3D_LOD(_cloudBaseNoise3D, s_linear_repeat_sampler,
//     baseUV, mipLevel).x;
//   float noise = max(0, remap(saturate(baseNoise), min(0.99, max(0.0, _cloudCoverageIntensity * coverageNoise * 2)), 1.0, 0.0, 1.0));
//
//   /* Compute the height gradient and remap accordingly. HACK: should be based on x/y ratio */
//   float heightGradient = saturate(remap(uv.y, 0, 0.01, 0, 1)) * saturate(remap(uv.y, 0.01, 0.2, 1, 0));
//   noise *= heightGradient;
//   return noise;
// }
//
// float computeShadowBlur(float r, float mu, float thickness, float sharpness) {
//   float h = r - _planetRadius;
//   float cos_h = -safeSqrt(h * (2 * _planetRadius + h)) / (_planetRadius + h);
//   return 1 - pow(saturate(thickness / abs(cos_h - mu)), sharpness);
// }
//
// float3 getVolumetricShadowBoxVolumeGeometry(float3 samplePoint, float3 d,
//   float3 L, SkyIntersectionData lightIntersection, float densityModifier,
//   float density, float noiseVal, float3 absorptionCoefficients, float3 uvw, float2 xExtent, float2 yExtent,
//   float2 zExtent) {
//   /* Compute the transmittance through the atmosphere. */
//   float r = length(samplePoint);
//   float mu = dot(normalize(samplePoint), L);
//   float t_lightHit = lightIntersection.endT - lightIntersection.startT;
//   float2 lightTransmittanceCoord = mapSky2DCoord(r, mu,
//     _atmosphereRadius, _planetRadius, t_lightHit,
//     false, _resT.y);
//   float3 T_sampleL = exp(sampleSkyTTextureRaw(lightTransmittanceCoord));
//
//   /* To soften the shadow due to the horizon line, blur the occlusion. */
//   float shadowBlur = computeShadowBlur(r, mu, 0.001, 0.25);
//
//   /* Use the "powdered sugar" hack to get some detail around the edges.
//    * TODO: probably tweakable. */
//   /* HACK: both should be based on x/y ratio instead of raw uvw.y */
//   float depthProbability = saturate(0.05 + pow(saturate(noiseVal * 3), remap(clamp(uvw.y*3+0.2, 0.0, 0.5), 0.0, 0.5, 0.5, 2)));
//   float verticalProbability = pow(remap(clamp(uvw.y*3, 0.0, 0.5), 0.0, 0.5, 0.1, 1.0), 0.8);
//
//   /* Finally, raymarch toward the light and accumulate self-shadowing. */
//   const int numShadowSamples = 5;
//   float marchedDist = 0;
//   float opticalDepth = 0;
//   float randomOffset = 0.5;//random_3_1(d + _tick * 0.1);
//   for (int i = 0; i < numShadowSamples; i++) {
//     float t = pow(6, i);
//     float ds = t + marchedDist;
//     float3 shadowSamplePoint = samplePoint + L * (t * randomOffset);
//     if (boundsCheck(shadowSamplePoint.x, xExtent) &&
//         boundsCheck(shadowSamplePoint.y, yExtent) &&
//         boundsCheck(shadowSamplePoint.z, zExtent)) {
//       float3 uvwShadow = positionToUVBoxVolume(shadowSamplePoint,
//         xExtent, yExtent, zExtent);
//       float shadowSample = computeDensity3DHighLOD(uvwShadow, 0); // for some reason, can't sample mipmaps
//       opticalDepth += t * shadowSample;
//     }
//   }
//   opticalDepth *= densityModifier;
//   float3 shadowT = computeMSModifiedTransmittance(absorptionCoefficients,
//     opticalDepth, _cloudMSAmount, _cloudMSBias);
//
//   return T_sampleL * shadowBlur * shadowT * depthProbability * verticalProbability;
// }
//
// float3 lightCloudLayerBoxVolumeGeometry(float3 samplePoint, float3 d, float3
//   transmittance, float3 totalTransmittance, float densityModifier, float density,
//   float noiseVal, float3 absorptionCoefficients, float3 scatteringCoefficients, float3 uvw, float2 xExtent,
//   float2 yExtent, float2 zExtent) {
//   int i; // Loop variable
//
//   float3 color = float3(0, 0, 0); // Final result.
//
//   d = normalize(d);
//
//   /* Precompute the phase function for all bodies. */
//   float lightPhases[MAX_BODIES];
//   for (i = 0; i < _numActiveBodies; i++) {
//     float3 L = normalize(_bodyDirection[i]);
//     lightPhases[i] = cloudPhaseFunction(clampCosine(dot(L, d)), _cloudAnisotropy,
//       _cloudSilverIntensity, _cloudSilverSpread);
//   }
//
//   /* Light the clouds according to each body. */
//   for (i = 0; i < _numActiveBodies; i++) {
//     float3 L = normalize(_bodyDirection[i]);
//     /* Check occlusion. */
//     SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint, L,
//       _planetRadius, _atmosphereRadius);
//     if (!lightIntersection.groundHit) {
//       /* Get the body luminance. */
//       float3 luminance = _bodyLightColor[i].xyz
//         * getVolumetricShadowBoxVolumeGeometry(samplePoint, d, L, lightIntersection,
//           densityModifier, density, noiseVal, absorptionCoefficients, uvw, xExtent, yExtent, zExtent);
//
//       /* Integrate the in-scattered luminance. */
//       float3 inScatter = scatteringCoefficients * density
//         * (luminance - luminance * transmittance)
//         / max(0.000001, density * absorptionCoefficients);
//
//       color += totalTransmittance * inScatter * 5 * lightPhases[i];
//     }
//   }
//
//   /* HACK: ambient */
//   // color += totalTransmittance * scatteringCoefficients * density * 40000 * float3(0.5, 0.7, 0.9);
//
//   return color;
// }
//
// CloudShadingResult raymarchCloudLayerBoxVolumeGeometry(float3 startPoint, float3 d, float dist,
//   float2 xExtent, float2 yExtent, float2 zExtent, float density,
//   float3 absorptionCoefficients, float3 scatteringCoefficients, float distToStart) {
//
//   /* Final result. */
//   CloudShadingResult result = cloudNoIntersectionResult();
//   result.transmittance = float3(1, 1, 1);
//   result.color = float3(0, 0, 0);
//   result.t_hit = dist; /* Initialize at dist and decrease if necessary. */
//   result.hit = true;
//   float3 totalLightingTransmittance = float3(1, 1, 1);
//
//   /* Constants that could be tweakable. */
//   const float detailStep = 50/dist;// max(10, 10 * distToStart/2000)/dist;
//   const float coarseStep = 200/dist;//max(200, 200 * distToStart/5000)/dist;
//
//   /* Marching state. */
//   float marchedFraction = 0;
//   float stepSize = coarseStep;
//   int consecutiveZeroSamples = 0;
//
//   int samples = 0;
//   const int maxNumSamples = 256;
//
//   float randomOffset = random_3_1(d * + 0.001 * _tick);
//
//   while (marchedFraction < 1 && averageFloat3(result.transmittance) > 0.001 && samples < maxNumSamples) {
//
//     /* Switch back to coarse marching if we've taken enough zero samples. */
//     if (consecutiveZeroSamples > 10) {
//       consecutiveZeroSamples = 0;
//       stepSize = coarseStep;
//     }
//
//     /* March coarse. */
//     if (floatEq(stepSize, coarseStep)) {
//       /* Sample low LOD density. */
//       float t = (marchedFraction + stepSize * randomOffset) * dist;
//       float3 samplePoint = startPoint + d * t;
//       float3 sampleUVW = positionToUVBoxVolume(samplePoint, xExtent, yExtent, zExtent);
//       float coarseDensity = computeDensity3DLowLOD(sampleUVW, 0);
//       if (coarseDensity < 0.00000001) {
//         /* Keep marching coarse. */
//         marchedFraction += stepSize;
//         samples++;
//         continue;
//       }
//       /* Switch to detail march, backtracking a step. */
//       stepSize = detailStep;
//     }
//
//     /* Otherwise, march detail. */
//     float t = (marchedFraction + stepSize * randomOffset) * dist;
//     float3 samplePoint = startPoint + d * t;
//     float3 sampleUVW = positionToUVBoxVolume(samplePoint, xExtent, yExtent, zExtent);
//     float detailDensityNoise = computeDensity3DHighLOD(sampleUVW, 0);
//
//     if (detailDensityNoise == 0) {
//       /* Skip and note that we did. */
//       marchedFraction += stepSize;
//       consecutiveZeroSamples++;
//       samples++;
//     }
//     consecutiveZeroSamples = 0;
//
//     /* Compute the optical depth. */
//     float detailDensity = density * detailDensityNoise;
//     float opticalDepth = detailDensity * stepSize * dist;
//
//     /* Compute the transmittance and accumulate. */
//     float3 sampleTransmittance = exp(-absorptionCoefficients * opticalDepth);
//     // float3 lightingTransmittance = computeMSModifiedTransmittance(absorptionCoefficients,
//       // opticalDepth, _cloudMSAmount, _cloudMSBias);
//     result.transmittance *= sampleTransmittance;
//     // totalLightingTransmittance *= lightingTransmittance;
//
//     /* Light the clouds. */
//     result.color += lightCloudLayerBoxVolumeGeometry(samplePoint, d, sampleTransmittance,
//       result.transmittance, density, detailDensity, detailDensityNoise, absorptionCoefficients,
//       scatteringCoefficients, sampleUVW, xExtent, yExtent, zExtent); // TODO: extinction needs to be cased out according to ms approximation
//
//     /* If transmittance is less than 0.5, write t_hit for the blend. */
//     if (result.t_hit > dist-0.01 && averageFloat3(result.transmittance) < 0.5) {
//       result.t_hit = t;
//     }
//
//     marchedFraction += stepSize;
//     samples++;
//   }
//
//   return result;
// }
//
// CloudShadingResult shadeCloudLayerBoxVolumeGeometry(float3 O, float3 d, int i,
//   float depth, bool geoHit, SkyIntersectionData skyIntersection) {
//   /* Final result. */
//   CloudShadingResult result;
//
//   /* Transform the cloud plane to planet space. */
//   float2 xExtent = float2(transformXToPlanetSpace(_cloudGeometryXMin[i]),
//     transformXToPlanetSpace(_cloudGeometryXMax[i]));
//   float2 yExtent = float2(transformYToPlanetSpace(_cloudGeometryYMin[i]),
//     transformYToPlanetSpace(_cloudGeometryYMax[i]));
//   float2 zExtent = float2(transformZToPlanetSpace(_cloudGeometryZMin[i]),
//     transformZToPlanetSpace(_cloudGeometryZMax[i]));
//   /* Intersect it. */
//   float2 t_hit = intersectAxisAlignedBoxVolume(O, d, xExtent, yExtent, zExtent);
//
//   /* We hit nothing. */
//   if (t_hit.x < 0 && t_hit.y < 0) {
//     return cloudNoIntersectionResult();
//   }
//
//   /* If we're inside the cloud volume, just set the start t to zero. */
//   t_hit.x = max(0, t_hit.x);
//
//   result.hit = true;
//
//   /* Light the clouds. */
//   CloudShadingResult litResult = raymarchCloudLayerBoxVolumeGeometry(O + d * t_hit.x, d,
//     t_hit.y - t_hit.x, xExtent, yExtent, zExtent, _cloudDensity[i],
//     _cloudAbsorptionCoefficients[i].xyz, _cloudScatteringCoefficients[i].xyz,
//     t_hit.x);
//   result.color = litResult.color;
//   result.transmittance = litResult.transmittance;
//   result.t_hit = litResult.t_hit + t_hit.x;
//
//   /* TODO: compute the blend to t_hit. */
//   float2 oToSample = mapSky2DCoord(length(O), dot(normalize(O), d),
//     _atmosphereRadius, _planetRadius, skyIntersection.endT,
//     skyIntersection.groundHit, _resT.y);
//   float3 samplePoint = O + d * result.t_hit;
//   float2 sampleOut = mapSky2DCoord(length(samplePoint), dot(normalize(samplePoint), d),
//     _atmosphereRadius, _planetRadius, skyIntersection.endT - result.t_hit,
//     skyIntersection.groundHit, _resT.y);
//   float3 t_oToSample = sampleSkyTTextureRaw(oToSample);
//   float3 t_sampleOut = sampleSkyTTextureRaw(sampleOut);
//   float3 blendTransmittanceColor = exp(t_oToSample - max(t_oToSample, t_sampleOut)
//    + computeTransmittanceDensityAttenuation(O, d, t_hit));
//   result.blend = dot(blendTransmittanceColor, float3(1, 1, 1) / 3.0); // TODO: put back
//
//   return result;
// }

float3 lightCloudLayer3D(float3 p, float3 d, ICloudGeometry geometry,
  float3 lightingTransmittance, float3 totalLightingTransmittance, int i) {
  float3 cumulativeLighting = float3(0, 0, 0);
  /* Loop over all celestial bodies. */
  for (int l = 0; l < _numActiveBodies; l++) {
    float3 L = _bodyDirection[l];
    float3 lightColor = _bodyLightColor[l].xyz;

    /* The cloud lighting model is purely attenuative. We start with the
     * full brightness of the light. */
    float3 lighting = lightColor;

    /* The first obvious attenuation comes from checking if we are in the
     * planet's shadow. */
    SkyIntersectionData lightIntersection = traceSkyVolume(p, L,
        _planetRadius, _atmosphereRadius);
    if (lightIntersection.groundHit) {
      continue;
    }

    /* For shadow blur and sky transmittance. */
    float r = length(p);
    float mu = dot(normalize(p), L);

    /* We then multiply by a planet "shadow blur" to smooth out the transition
     * from day to night.
     * TODO: tweakable, not necessary when using self-shadowing. */
    const bool _useShadowBlur = false;
    if (_useShadowBlur) {
      const float blurOffset = 0.001;
      const float blurDistance = 0.01;
      lighting *= computeShadowBlur(r, mu, blurOffset, blurDistance);
    }

    /* The next attenuation accounts for the transmittance through the
     * atmosphere to the sample point. */
    float2 lightTransmittanceCoord = mapSky2DCoord(r, mu,
      _atmosphereRadius, _planetRadius, lightIntersection.endT - lightIntersection.startT,
      false, _resT.y);
    lighting *= exp(sampleSkyTTextureRaw(lightTransmittanceCoord));

    /* Next, we attenuate according to the cloud's phase function. */
    float dot_L_d = dot(L, d);
    lighting *= cloudPhaseFunction(dot_L_d, _cloudAnisotropy, _cloudSilverIntensity, _cloudSilverSpread);

    /* In order to account for in-scattering probability, we use the lodded
     * density hack proposed by Andrew Schneider in his Nubis system.
     * TODO: HEAVY tweaking to be done here. */
    // TODO: density attenuation?
    float loddedDensity = takeMediaSample3DHighLOD(p, geometry, 0);
    float height = geometry.heightGradient(p);
    float depthProbability = saturate(0.05 + pow(loddedDensity*15, remap(clamp(height, 0.3, 0.85), 0.3, 0.85, 0.5, 2.0)));
    float verticalProbability = pow(remap(clamp(height, 0.07, 0.3), 0.07, 0.3, 0.1, 1.0), 0.8);
    lighting *= verticalProbability * depthProbability;


    /* If we are permitted, take a few samples at increasing mip levels to
     * model self-shadowing. */
    const bool _useSelfShadowing = true;
    if (_useSelfShadowing) {
      float3 selfShadow = float3(1, 1, 1);
      /* Figure out where the light direction would leave the
       * cloud volume. */
      float2 lightExit = geometry.intersect(p, L);
      // TODO: probably tweakable.
      const int numShadowSamples = 4;
      const float shadowDistance = max(0, min(5000, lightExit.y));
      for (int j = 0; j < numShadowSamples; j++) {
        float2 t_ds = generateNthPowerSampleFromIndex(j, numShadowSamples, 3);
        float3 shadowSamplePoint = p + L * t_ds.x * shadowDistance;
        if (geometry.inBounds(shadowSamplePoint)) {
          // TODO: density attenuation
          float shadowSample = takeMediaSample3DHighLOD(shadowSamplePoint, geometry, 0);
          float attenuation = geometry.densityAttenuation(shadowSamplePoint, _cloudDensityAttenuationDistance[i], _cloudDensityAttenuationBias[i]);
          float opticalDepth = _cloudDensity[i] * attenuation * shadowSample * t_ds.y * shadowDistance;
          float3 shadowTransmittance = computeMSModifiedTransmittance(_cloudAbsorptionCoefficients[i].xyz, opticalDepth);
          selfShadow *= shadowTransmittance;
        }
      }
      lighting *= selfShadow;
    }


    /* The final step is to integrate the attenuated luminance we've
     * calculated according to the cloud's density. We do this using
     * Sebastien Hillaire's improved integration method. */
    float3 clampedAbsorption = clampAboveZero(_cloudAbsorptionCoefficients[i].xyz);
    lighting = totalLightingTransmittance * (lighting - lightingTransmittance * lighting) * (_cloudScatteringCoefficients[i].xyz/clampedAbsorption);

    cumulativeLighting += lighting;
  }
  return cumulativeLighting;
}

CloudShadingResult raymarchCloudLayer3D(float3 start, float3 d, float t,
  ICloudGeometry geometry, int i) {
  CloudShadingResult result;
  result.transmittance = float3(1, 1, 1);
  result.color = float3(0, 0, 0);
  result.t_hit = t; /* Initialize at dist and decrease if necessary. */
  result.hit = true;
  result.blend = 1;

  /* Marching parameters: TODO: tweakable in sampling. */
  const float detailStep = 1.0/128.0;
  const float coarseStep = 1.0/16.0;
  const int maxSamples = 128;
  const float mediaZeroThreshold = 0.00001;
  const float transmittanceZeroThreshold = 0.001;
  const int maxConsecutiveZeroSamples = 10;

  /* Marching state. */
  float tMarched = 0.0;
  bool marchCoarse = true;
  int samplesTaken = 0;
  int consecutiveZeroSamples = 0;
  float3 totalLightingTransmittance = float3(1, 1, 1);

  while (averageFloat3(result.transmittance) > transmittanceZeroThreshold
    && tMarched < t && samplesTaken < maxSamples) {

    if (marchCoarse) {
      /* Take a test coarse sample. */
      float ds = coarseStep * t;
      float3 testPoint = start + d * (tMarched + 0.5 * ds);
      float testSample = takeMediaSample3DLowLOD(testPoint, geometry);
      /* If it's zero, keep up with the coarse marching. */
      if (testSample < mediaZeroThreshold) {
        tMarched += ds;
        samplesTaken++;
        continue;
      }
      /* Otherwise switch to detailed marching. */
      marchCoarse = false;
    }

    /* Take a detailed sample. */
    float ds = detailStep * t;
    float3 p = start + d * (tMarched + 0.5 * ds);
    float mediaSample = takeMediaSample3DHighLOD(p, geometry);

    /* If it's zero, skip. If it's been zero for a while, switch back to
     * coarse marching. */
    if (mediaSample < mediaZeroThreshold) {
      consecutiveZeroSamples++;
      marchCoarse = consecutiveZeroSamples > maxConsecutiveZeroSamples;
      tMarched += ds;
      samplesTaken++;
      continue;
    }

    /* Accumulate transmittance---including a modified transmittance used
     * in the lighting calculation to simulate multiple scattering. */
    float attenuation = geometry.densityAttenuation(p, _cloudDensityAttenuationDistance[i], _cloudDensityAttenuationBias[i]);
    float attenuatedDensity = (attenuation * _cloudDensity[i]);
    float opticalDepth = attenuatedDensity * mediaSample * ds;
    result.transmittance *= exp(-_cloudAbsorptionCoefficients[i].xyz * opticalDepth);
    float3 lightingTransmittance = computeMSModifiedTransmittance(_cloudAbsorptionCoefficients[i].xyz, opticalDepth);

    /* Accumulate lighting. */
    result.color += lightCloudLayer3D(p, d, geometry, lightingTransmittance, totalLightingTransmittance, i);
    totalLightingTransmittance *= lightingTransmittance;

    /* If transmittance has crossed the 0.5 mark, record this as our volumetric
     * hit point, which we'll use for computing atmospheric blend. */
    if (averageFloat3(result.transmittance) < 0.5 && floatGT(result.t_hit, t)) {
      result.t_hit = tMarched + 0.5 * ds;
    }

    tMarched += ds;
    samplesTaken++;

  };

  return result;
}

CloudShadingResult shadeCloudLayer3D(float3 O, float3 d, ICloudGeometry geometry,
  SkyIntersectionData skyIntersection, bool geoHit, float depth, int i) {
  float2 t_hit = geometry.intersect(O, d);
  /* We hit nothing. */
  if ((t_hit.x < 0 && t_hit.y < 0) || (geoHit && t_hit.x > depth)) {
    return cloudNoIntersectionResult();
  }

  float3 start = O + d * t_hit.x;
  float3 end = O + d * t_hit.y;

  /* Raymarch to accumulate transmittance and lighting, and to determine
   * the volumetric hit point. */
  CloudShadingResult result = raymarchCloudLayer3D(start, d, t_hit.y - t_hit.x,
    geometry, i);
  result.t_hit += t_hit.x;

  /* Atmospheric blend. */
  result.blend = computeAtmosphericBlend(O, d, O + d * result.t_hit,
    result.t_hit, skyIntersection);

  return result;
}

/******************************************************************************/
/********************************** END 3D ************************************/
/******************************************************************************/









CloudShadingResult shadeCloudLayer(float3 O, float3 d, int i, float depth,
  bool geoHit) {

  /* Intersect the sky to pass along to the shading function. */
  SkyIntersectionData skyIntersection = traceSkyVolume(O, d,
   _planetRadius, _atmosphereRadius);

  /* Transform geometry info to planet space. */
  float2 xExtent = float2(transformXToPlanetSpace(_cloudGeometryXMin[i]),
  transformXToPlanetSpace(_cloudGeometryXMax[i]));
  float2 yExtent = float2(transformYToPlanetSpace(_cloudGeometryYMin[i]),
  transformYToPlanetSpace(_cloudGeometryYMax[i]));
  float2 zExtent = float2(transformZToPlanetSpace(_cloudGeometryZMin[i]),
  transformZToPlanetSpace(_cloudGeometryZMax[i]));
  float height = transformYToPlanetSpace(_cloudGeometryHeight[i]);
  float apparentThickness = _cloudThickness[i];

  /* Create the right geometry and call the shading function that
   * corresponds with that geometry's dimension. */
  switch (_cloudGeometryType[i]) {
    case CloudGeometryType_Plane: {
      CloudPlane geometry = CreateCloudPlane(xExtent, zExtent, height, apparentThickness);
      return shadeCloudLayer2D(O, d, geometry, skyIntersection, geoHit, depth, i);
    }
    case CloudGeometryType_Sphere: {
      // TODO
      break;
    }
    case CloudGeometryType_BoxVolume: {
      CloudBoxVolume geometry = CreateCloudBoxVolume(xExtent, yExtent, zExtent);
      return shadeCloudLayer3D(O, d, geometry, skyIntersection, geoHit, depth, i);
    }
  }

  /* Default case. Return something noticeably bad. */
  CloudShadingResult result;
  result.t_hit = 1000;
  result.hit = true;
  result.transmittance = 0.5;
  result.color = float3(1000000000, 0, 0);
  result.blend = 1;
  return result;
}
