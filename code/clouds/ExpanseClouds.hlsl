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
      const float blurOffset = 0.5;
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
    float loddedDensity = takeMediaSample3DHighLOD(p, geometry, 1);
    float height = geometry.heightGradient(p);
    lighting *= computeVerticalInScatterProbability(height,
      _cloudVerticalProbability.xy, _cloudVerticalProbability.z);
    lighting *= computeDepthInScatterProbability(loddedDensity, height,
      _cloudDepthProbabilityHeightStrength.xy, _cloudDepthProbabilityHeightStrength.zw,
      _cloudDepthProbabilityDensityMultiplier, _cloudDepthProbabilityBias);

    /* If we are permitted, take a few samples at increasing mip levels to
     * model self-shadowing. */
    const bool _useSelfShadowing = true;
    if (_useSelfShadowing) {
      float3 selfShadow = float3(1, 1, 1);
      /* Figure out where the light direction would leave the
       * cloud volume. */
      float2 lightExit = geometry.intersect(p, L);
      // TODO: probably tweakable.
      // TODO: cone sample?
      const int numShadowSamples = 4;
      const float shadowDistance = max(0, min(5000, lightExit.y));
      for (int j = 0; j < numShadowSamples; j++) {
        float2 t_ds = generateNthPowerSampleFromIndex(j, numShadowSamples, 3);
        float3 shadowSamplePoint = p + L * t_ds.x * shadowDistance;
        if (geometry.inBounds(shadowSamplePoint)) {
          // TODO: density attenuation
          float shadowSample = takeMediaSample3DHighLOD(shadowSamplePoint, geometry, j*1.5);
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
  result.t_hit = 0;
  result.hit = true;
  result.blend = 1;

  /* Marching parameters: TODO: tweakable in sampling. 64 is a safe number,
   * 32 seems to be ok, but introduce the possibility of a little flickering. */
  const float detailStep = 1.0/32.0;
  const float coarseStep = 1.0/16.0;
  const int maxSamples = 32;
  const float mediaZeroThreshold = 0.0001;
  const float transmittanceZeroThreshold = 0.0001;
  const int maxConsecutiveZeroSamples = 4;

  /* Marching state. */
  float tMarched = 0.0;
  bool marchCoarse = true;
  int samplesTaken = 0;
  int consecutiveZeroSamples = 0;
  float3 totalLightingTransmittance = float3(1, 1, 1);
  float summedMonochromeTransmittance = 0;

  while (averageFloat3(result.transmittance) > transmittanceZeroThreshold
    && tMarched < t && samplesTaken < maxSamples) {

    if (marchCoarse) {
      /* Take a test coarse sample. */
      float ds = coarseStep * t;
      float3 testPoint = start + d * (tMarched + getBlueNoiseOffset() * ds);
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
    float3 p = start + d * (tMarched + getBlueNoiseOffset() * ds);
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

    /* Compute transmittance---including a modified transmittance used
     * in the lighting calculation to simulate multiple scattering. */
    float attenuation = geometry.densityAttenuation(p, _cloudDensityAttenuationDistance[i], _cloudDensityAttenuationBias[i]);
    float attenuatedDensity = (attenuation * _cloudDensity[i]);
    float opticalDepth = attenuatedDensity * mediaSample * ds;
    float3 transmittance = exp(-_cloudAbsorptionCoefficients[i].xyz * opticalDepth);
    float3 lightingTransmittance = computeMSModifiedTransmittance(_cloudAbsorptionCoefficients[i].xyz, opticalDepth);

    /* Accumulate lighting. */
    result.color += lightCloudLayer3D(p, d, geometry, lightingTransmittance, totalLightingTransmittance, i);

    /* Accumulate transmittance. */
    result.transmittance *= transmittance;
    totalLightingTransmittance *= lightingTransmittance;

    /* Accumulate our weighted average for t_hit. */
    float monochromeTransmittance = averageFloat3(result.transmittance);
    result.t_hit += monochromeTransmittance * (tMarched + 0.5 * ds);
    summedMonochromeTransmittance += monochromeTransmittance;

    tMarched += ds;
    samplesTaken++;

  };

  result.t_hit /= clampAboveZero(summedMonochromeTransmittance);

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
  // This is a super HACK -ey way to get rid of the splotchy edges. It
  // significantly improves the look of the composite. I really don't like
  // hacks like this at all, so I consider this a temporary solution to the
  // problem.
  float avgTransmittance = averageFloat3(result.transmittance);
  result.transmittance = pow(result.transmittance, 1 - 0.9 * pow(avgTransmittance, 1.5));

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
    case CloudGeometryType_CurvedPlane: {
      CloudCurvedPlane geometry = CreateCloudCurvedPlane(xExtent, zExtent, height, apparentThickness);
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
