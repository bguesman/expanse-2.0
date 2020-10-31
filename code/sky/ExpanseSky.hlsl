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
    float opticalDepth = computeOpticalDepth(_layerDensityDistribution[i],
      O, endPoint, _layerHeight[i], _layerThickness[i], _layerDensity[i],
      _layerAttenuationBias[i], _layerAttenuationDistance[i],
      _layerUseDensityAttenuation[i], _numTSamples);
    power += opticalDepth * _layerCoefficientsA[i].xyz;
  }
  return -power;
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

SSLayersResult computeSSLayers(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L) {

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
      float3 T = exp(T_oToSample + sampleSkyTTextureRaw(sampleToL));

      for (int j = 0; j < _numActiveLayers; j++) {
        result.shadows[j] += scaledDensity[j] * T;
      }
    }
  }

  return result;

}

struct SSResult {
  float3 shadows;
  float3 noShadows;
};

/* Doesn't use phase function or light color. TODO: pass in # of samples
 * to computeSSLayers and make tweakable. */
SSResult computeSSForMS(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L) {
  SSLayersResult ssLayers = computeSSLayers(O, d, dist, t_hit, groundHit, L);
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    result.shadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.shadows[i]);
    result.noShadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.noShadows[i]);
  }
  return result;
}

SSResult computeSSBody(float3 O, float3 d, float dist, float t_hit, bool groundHit, float3 L,
  float3 lightColor) {
  SSLayersResult ssLayers = computeSSLayers(O, d, dist, t_hit, groundHit, L);
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

SSResult computeSS(float3 O, float3 d, float dist, float t_hit, bool groundHit) {
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    SSResult bodySS = computeSSBody(O, d, dist, t_hit, groundHit, _bodyDirection[i], _bodyLightColor[i].xyz);
    result.shadows += bodySS.shadows;
    result.noShadows += bodySS.noShadows;
  }
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


float3 computeMS(float3 O, float3 d, float dist, float t_hit, bool groundHit) {
  float3 result = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    result += computeMSBody(O, d, dist, t_hit, groundHit, _bodyDirection[i], _bodyLightColor[i].xyz);
  }
  return result;
}

/******************************************************************************/
/************************** END MULTIPLE SCATTERING ***************************/
/******************************************************************************/
