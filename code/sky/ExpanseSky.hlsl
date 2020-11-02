#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseRandom.hlsl"

/******************************************************************************/
/******************************* TRANSMITTANCE ********************************/
/******************************************************************************/

float3 computeTransmittance(float3 O, float3 d, float endT) {
  /* Compute optical depth for all enabled atmosphere layers. */
  float3 endPoint = O + d * endT;
  float3 power = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    if (!_layerUseDensityAttenuation[i]) {
      float opticalDepth = computeOpticalDepth(_layerDensityDistribution[i],
        O, endPoint, _layerHeight[i], _layerThickness[i], _layerDensity[i],
        _layerAttenuationBias[i], _layerAttenuationDistance[i],
        _layerUseDensityAttenuation[i], O, _numTSamples);
      power += opticalDepth * _layerCoefficientsA[i].xyz;
    }
  }
  return -power;
}

/* P is the point to compute distance to attenuate from. */
float3 computeTransmittanceDensityAttenuation(float3 O, float3 d, float endT, float P) {
  /* Integrate analytically. TODO: doesn't work for tent function yet. */
  float3 power = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    if (_layerUseDensityAttenuation[i]) {
      float m = _layerAttenuationDistance[i];
      float k = _layerAttenuationBias[i];
      float H = _layerThickness[i];
      float3 deltaPO = O - P;
      float a = 1 / (m * m);
      float b = ((-2 * dot(deltaPO, d)) / (m * m)) + (dot(d, normalize(O)) / H);
      float c = ((_planetRadius - length(O)) / H) + ((k * k - dot(deltaPO, deltaPO)) / (m * m));
      float prefactor = exp(c) * sqrt(PI) * exp((b * b) / (4 * a)) / (2 * sqrt(a));
      float erf_f = erf((2 * a * endT - b) / (2 * sqrt(a)));
      float erf0 = erf((-b) / (2 * sqrt(a)));
      float opticalDepth = max(0, _layerDensity[i] * prefactor * (erf_f - erf0));

      power += opticalDepth * _layerCoefficientsA[i].xyz;
    }
  }

  return -power;

  // float3 endPoint = O + d * endT;
  // float3 power = float3(0, 0, 0);
  // for (int i = 0; i < _numActiveLayers; i++) {
  //   if (_layerUseDensityAttenuation[i]) {
  //     float opticalDepth = computeOpticalDepth(_layerDensityDistribution[i],
  //       O, endPoint, _layerHeight[i], _layerThickness[i], _layerDensity[i],
  //       _layerAttenuationBias[i], _layerAttenuationDistance[i],
  //       _layerUseDensityAttenuation[i], P, _numTSamples);
  //     power += opticalDepth * _layerCoefficientsA[i].xyz;
  //   }
  // }
  // return -power;
}

/******************************************************************************/
/***************************** END TRANSMITTANCE ******************************/
/******************************************************************************/



/******************************************************************************/
/***************************** SINGLE SCATTERING ******************************/
/******************************************************************************/

struct SSLayersResult {
  float3 shadows[MAX_LAYERS];
  float3 noShadows[MAX_LAYERS];
};

// TODO: make it possible to pass in whether or not to importance sample.
// Then, have a checkbox for importance sampling for aerial perspective
// and for the rest of the sky, since frequently importance sampling for
// aerial perspective is a bad idea.
SSLayersResult computeSSLayers(float3 O, float3 d, float dist, float t_hit,
  bool groundHit, float3 L, bool useOcclusionMultiplier) {

  /* Final result */
  SSLayersResult result;

  /* Precompute transmittance in direction d to edge of atmosphere. */
  float2 oToSample = mapSky2DCoord(length(O), dot(normalize(O), d),
  _atmosphereRadius, _planetRadius, t_hit, groundHit, _resT.y);
  float3 T_oOut = sampleSkyTTextureRaw(oToSample);

  /* Initialize accumulators to zero. */
  float scaledDensity[MAX_LAYERS];
  for (int j = 0; j < _numActiveLayers; j++) {
    scaledDensity[j] = 0;
    result.shadows[j] = float3(0, 0, 0);
    result.noShadows[j] = float3(0, 0, 0);
  }

  for (int i = 0; i < _numSSSamples; i++) {
    /* Generate the sample. */
    float2 t_ds;
    if (_useImportanceSampling) {
      t_ds = generateCubicSampleFromIndex(i, _numSSSamples);
    } else {
      t_ds = generateLinearSampleFromIndex(i, _numSSSamples);
    }
    float sampleT = t_ds.x * dist;
    float ds = t_ds.y;
    float3 samplePoint = O + d * sampleT;
    float3 normalizedSamplePoint = normalize(samplePoint);

    /* Compute the scaled density of the layer at the sample point. */
    for (int j = 0; j < _numActiveLayers; j++) {
      scaledDensity[j] = ds * computeDensity(_layerDensityDistribution[j],
        samplePoint, _layerHeight[j], _layerThickness[j], _layerDensity[j],
        _layerUseDensityAttenuation[j], sampleT, _layerAttenuationBias[j],
        _layerAttenuationDistance[j]);
    }

    /* Our transmittance value for O to the sample point is too large---we
     * need to divide out the transmittance from the sample point to the
     * atmosphere edge (or ground). */
    float2 sampleOut = mapSky2DCoord(length(samplePoint),
      clampCosine(dot(normalizedSamplePoint, d)), _atmosphereRadius,
      _planetRadius, t_hit - sampleT, groundHit, _resT.y);
    float3 T_oToSample = T_oOut - sampleSkyTTextureRaw(sampleOut) +
      computeTransmittanceDensityAttenuation(O, d, sampleT, O);

    for (int j = 0; j < _numActiveLayers; j++) {
      result.noShadows[j] += scaledDensity[j] * saturate(exp(T_oToSample));
    }

    /* Trace a ray from the sample point to the light to check visibility. */
    SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint,
      L, _planetRadius, _atmosphereRadius);
    if (!lightIntersection.groundHit) {
      /* Compute transmittance from the sample to the light hit point and add
       * it to the total transmittance. */
      float2 sampleToL = mapSky2DCoord(length(samplePoint),
        clampCosine(dot(normalizedSamplePoint, L)), _atmosphereRadius,
        _planetRadius, lightIntersection.endT, lightIntersection.groundHit, _resT.y);
      float3 T = exp(T_oToSample + sampleSkyTTextureRaw(sampleToL) +
        computeTransmittanceDensityAttenuation(samplePoint, L, lightIntersection.endT, O));

      for (int j = 0; j < _numActiveLayers; j++) {
        result.shadows[j] += scaledDensity[j] * T;
      }
    }
  }

  if (useOcclusionMultiplier) {
    float dot_L_d = dot(L, d);
    float occlusionMultiplierUniform = _aerialPerspectiveOcclusionBiasUniform
      + (1-_aerialPerspectiveOcclusionBiasUniform) * pow(1-saturate(dot_L_d),
      _aerialPerspectiveOcclusionPowerUniform);
    float occlusionMultiplierDirectional = _aerialPerspectiveOcclusionBiasDirectional
      + (1-_aerialPerspectiveOcclusionBiasDirectional) * pow(1-saturate(dot_L_d),
      _aerialPerspectiveOcclusionPowerDirectional);

    for (int j = 0; j < _numActiveLayers; j++) {
      if (_layerPhaseFunction[j] == 2) {
        /* Special case for mie scattering. */
        result.shadows[j] *= occlusionMultiplierDirectional;
      } else {
        result.shadows[j] *= occlusionMultiplierUniform;
      }
    }
  }

  return result;

}

struct SSResult {
  float3 shadows;
  float3 noShadows;
};

SSResult computeSSBody(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L,
  float3 lightColor, bool useOcclusionMultiplier) {
  SSLayersResult ssLayers = computeSSLayers(O, d, dist, t_hit, groundHit, L, useOcclusionMultiplier);
  float dot_L_d = dot(L, d);
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    float phase = computePhase(dot_L_d, _layerAnisotropy[i], _layerPhaseFunction[i]);
    result.shadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.shadows[i] * phase);
    result.noShadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.noShadows[i] * phase);
  }
  result.shadows *= lightColor * dist;
  result.noShadows *= lightColor * dist;
  return result;
}

/* Doesn't use phase function or light color. TODO: pass in # of samples
 * to computeSSLayers and make tweakable. */
SSResult computeSSForMS(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L) {
  SSLayersResult ssLayers = computeSSLayers(O, d, dist, t_hit, groundHit, L, false);
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    result.shadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.shadows[i]);
    result.noShadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.noShadows[i]);
  }
  result.shadows *= dist;
  result.noShadows *= dist;
  return result;
}

/* Specific to light pollution, samples the emissive ground instead of a light
 * source. It would have been nice to roll this into the regular function,
 * but there's no way to cleanly avoid the light occlusion check without
 * another conditional. */
SSLayersResult computeSSLPLayers(float3 O, float3 d, float dist, float t_hit, bool groundHit) {
  /* Final result */
  SSLayersResult result;

  /* Precompute transmittance in direction d to edge of atmosphere. */
  float2 oToSample = mapSky2DCoord(length(O), dot(normalize(O), d),
  _atmosphereRadius, _planetRadius, t_hit, groundHit, _resT.y);
  float3 T_oOut = sampleSkyTTextureRaw(oToSample);

  /* Initialize accumulators to zero. */
  float scaledDensity[MAX_LAYERS];
  for (int j = 0; j < _numActiveLayers; j++) {
    scaledDensity[j] = 0;
    result.shadows[j] = float3(0, 0, 0);
    result.noShadows[j] = float3(0, 0, 0);
  }

  for (int i = 0; i < _numSSSamples; i++) {
    /* Generate the sample. */
    float2 t_ds;
    if (_useImportanceSampling) {
      t_ds = generateCubicSampleFromIndex(i, _numSSSamples);
    } else {
      t_ds = generateLinearSampleFromIndex(i, _numSSSamples);
    }
    float sampleT = t_ds.x * dist;
    float ds = t_ds.y;
    float3 samplePoint = O + d * sampleT;
    float3 normalizedSamplePoint = normalize(samplePoint);

    /* Compute the scaled density of the layer at the sample point. */
    for (int j = 0; j < _numActiveLayers; j++) {
      scaledDensity[j] = ds * computeDensity(_layerDensityDistribution[j],
        samplePoint, _layerHeight[j], _layerThickness[j], _layerDensity[j],
        _layerUseDensityAttenuation[j], sampleT, _layerAttenuationBias[j],
        _layerAttenuationDistance[j]);
    }

    /* Our transmittance value for O to the sample point is too large---we
     * need to divide out the transmittance from the sample point to the
     * atmosphere edge (or ground). */
    float2 sampleOut = mapSky2DCoord(length(samplePoint),
      clampCosine(dot(normalizedSamplePoint, d)), _atmosphereRadius,
      _planetRadius, t_hit - sampleT, groundHit, _resT.y);
    float3 T_oToSample = T_oOut - sampleSkyTTextureRaw(sampleOut);

    /* Compute the transmittance to the ground. */
    float2 sampleToGround = mapSky2DCoord(length(samplePoint), -1, _atmosphereRadius,
      _planetRadius, length(samplePoint) - _planetRadius, true, _resT.y);
    float3 T = exp(T_oToSample + sampleSkyTTextureRaw(sampleToGround));
    for (int j = 0; j < _numActiveLayers; j++) {
      result.noShadows[j] += scaledDensity[j] * T;
      result.shadows[j] += scaledDensity[j] * T;
    }
  }

  return result;
}

SSResult computeSSLP(float3 O, float3 d, float dist, float t_hit, bool groundHit,
  float3 groundEmission) {
  SSLayersResult ssLayers = computeSSLPLayers(O, d, dist, t_hit, groundHit);
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    result.shadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * ssLayers.shadows[i];
    result.noShadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * ssLayers.noShadows[i];
  }
  result.shadows *= groundEmission * dist;
  result.noShadows *= groundEmission * dist;
  return result;
}

SSResult computeSS(float3 O, float3 d, float dist, float t_hit, bool groundHit,
  float nightScatterMultiplier, bool useOcclusionMultiplier) {
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    SSResult bodySS = computeSSBody(O, d, dist, t_hit, groundHit, _bodyDirection[i], _bodyLightColor[i].xyz, useOcclusionMultiplier);
    result.shadows += bodySS.shadows;
    result.noShadows += bodySS.noShadows;
  }

  /* Fake some scattering from the stars texture using the sky's average color. */
  SSResult nightSkySS = computeSSForMS(O, d, dist, t_hit, groundHit, normalize(O));
  result.shadows += nightSkySS.shadows * _nightSkyScatterTint.xyz * _averageNightSkyColor.xyz * nightScatterMultiplier;
  result.noShadows += nightSkySS.noShadows * _nightSkyScatterTint.xyz * _averageNightSkyColor.xyz * nightScatterMultiplier;

  /* Integrate the light pollution. */
  SSResult lightPollutionSS = computeSSLP(O, d, dist, t_hit, groundHit, _lightPollutionTint.xyz);
  result.shadows += lightPollutionSS.shadows;
  result.noShadows += lightPollutionSS.noShadows;

  return result;
}

/******************************************************************************/
/*************************** END SINGLE SCATTERING ****************************/
/******************************************************************************/



/******************************************************************************/
/**************************** MULTIPLE SCATTERING *****************************/
/******************************************************************************/

struct MSLayersResult {
  float3 shadows[MAX_LAYERS];
};

MSLayersResult computeMSLayers(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L) {
  /* Final result. */
  MSLayersResult result;
  for (int j = 0; j < _numActiveLayers; j++) {
    result.shadows[j] = float3(0, 0, 0);
  }

  for (int i = 0; i < _numMSAccumulationSamples; i++) {
    /* Get the sample point. */
    float2 t_ds;
    if (_useImportanceSampling) {
      t_ds = generateCubicSampleFromIndex(i, _numMSAccumulationSamples);
    } else {
      t_ds = generateLinearSampleFromIndex(i, _numMSAccumulationSamples);
    }
    float sampleT = dist * t_ds.x;
    float ds = t_ds.y;

    /* Sample multiple scattering table. */
    float3 samplePoint = O + d * sampleT;
    float2 msUV = mapMSCoordinate(length(samplePoint),
      dot(normalize(samplePoint), L), _atmosphereRadius, _planetRadius);
    float3 msContrib = sampleSkyMSTexture(msUV);

    /* Compute the scaled density of the layer at the sample point. */
    for (int j = 0; j < _numActiveLayers; j++) {
      float scaledDensity = ds * computeDensity(_layerDensityDistribution[j],
        samplePoint, _layerHeight[j], _layerThickness[j], _layerDensity[j],
        _layerUseDensityAttenuation[j], sampleT, _layerAttenuationBias[j],
        _layerAttenuationDistance[j]);
      result.shadows[j] += msContrib * scaledDensity;
    }
  }

  return result;
}

float3 computeMSBody(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L,
  float3 lightColor) {
  MSLayersResult msLayers = computeMSLayers(O, d, dist, t_hit, groundHit, L);
  float3 result = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    result += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz)
      * (msLayers.shadows[i] * _layerMultipleScatteringMultiplier[i]) * dist;
  }
  return result * lightColor;
}

/* Specific to light pollution, samples the emissive ground instead of a light
 * source. It would have been nice to roll this into the regular function,
 * but there's no way to cleanly avoid the light occlusion check without
 * another conditional. */
MSLayersResult computeMSLPLayers(float3 O, float3 d, float dist, float t_hit, bool groundHit) {
  /* Final result. */
  MSLayersResult result;
  for (int j = 0; j < _numActiveLayers; j++) {
    result.shadows[j] = float3(0, 0, 0);
  }

  for (int i = 0; i < _numMSAccumulationSamples; i++) {
    /* Get the sample point. */
    float2 t_ds;
    if (_useImportanceSampling) {
      t_ds = generateCubicSampleFromIndex(i, _numMSAccumulationSamples);
    } else {
      t_ds = generateLinearSampleFromIndex(i, _numMSAccumulationSamples);
    }
    float sampleT = dist * t_ds.x;
    float ds = t_ds.y;

    /* Sample multiple scattering table. */
    float3 samplePoint = O + d * sampleT;
    float2 msUV = mapMSCoordinate(length(samplePoint),
      -1, _atmosphereRadius, _planetRadius);
    float3 msContrib = sampleSkyMSTexture(msUV);

    /* Compute the scaled density of the layer at the sample point. */
    for (int j = 0; j < _numActiveLayers; j++) {
      float scaledDensity = ds * computeDensity(_layerDensityDistribution[j],
        samplePoint, _layerHeight[j], _layerThickness[j], _layerDensity[j],
        _layerUseDensityAttenuation[j], sampleT, _layerAttenuationBias[j],
        _layerAttenuationDistance[j]);
      result.shadows[j] += msContrib * scaledDensity;
    }
  }

  return result;
}

float3 computeMSLP(float3 O, float3 d, float dist, float t_hit, bool groundHit,
  float3 groundEmission) {
  MSLayersResult msLayers = computeMSLPLayers(O, d, dist, t_hit, groundHit);
  float3 result = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    result += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz)
      * (msLayers.shadows[i] * _layerMultipleScatteringMultiplier[i]) * dist;
  }
  return result * groundEmission;
}


float3 computeMS(float3 O, float3 d, float dist, float t_hit, bool groundHit,
  float nightScatterMultiplier) {
  float3 result = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    result += computeMSBody(O, d, dist, t_hit, groundHit, _bodyDirection[i], _bodyLightColor[i].xyz);
  }

  /* Fake some scattering from the stars texture using the sky's average color. */
  result += computeMSBody(O, d, dist, t_hit, groundHit, normalize(O),
    _nightSkyScatterTint.xyz * _averageNightSkyColor.xyz * nightScatterMultiplier);

  /* Factor in the light pollution. HACK: commented out, because it's so
   * imperceptible it's just a waste of resources to compute. */
  // result += computeMSLP(O, d, dist, t_hit, groundHit, _lightPollutionTint.xyz);

  return result;
}

/******************************************************************************/
/************************** END MULTIPLE SCATTERING ***************************/
/******************************************************************************/
