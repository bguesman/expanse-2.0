#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseRandom.hlsl"

struct SSLayersResult {
  float3 shadows[MAX_LAYERS];
  float3 noShadows[MAX_LAYERS];
};

SSLayersResult computeSSLayers(float3 O, float3 d, float t_hit, bool groundHit, float3 L) {
  SSLayersResult result; // final result

  float mu = dot(normalize(O), d);

  float2 oToSample = mapSky2DCoord(length(O), mu, _atmosphereRadius,
    _planetRadius, t_hit, groundHit, _resT.y);

  float scaledDensity[MAX_LAYERS];
  for (int j = 0; j < _numActiveLayers; j++) {
    scaledDensity[j] = 0;
    result.shadows[j] = float3(0, 0, 0);
    result.noShadows[j] = float3(0, 0, 0);
  }

  for (int i = 0; i < _numSSSamples; i++) {
    float sampleT = 0.0;
    float ds = 0.0;
    if (_useImportanceSampling) {
      float2 t_ds = generateCubicSampleFromIndex(i, _numSSSamples);
      sampleT = t_ds.x * t_hit;
      ds = t_ds.y;
    } else {
      /* Distribute linearly. */
      float2 t_ds = generateLinearSampleFromIndex(i, _numSSSamples);
      sampleT = t_ds.x * t_hit;
      ds = t_ds.y;
    }

    float3 samplePoint = O + d * sampleT;
    float3 normalizedSamplePoint = normalize(samplePoint);

    /* Compute the scaled density of the layer at the sample point. */
    for (int j = 0; j < _numActiveLayers; j++) {
      scaledDensity[j] = ds * computeDensity(_layerDensityDistribution[j],
        samplePoint, _layerHeight[j], _layerThickness[j], _layerDensity[j],
        _layerUseDensityAttenuation[j], sampleT, _layerAttenuationBias[j],
        _layerAttenuationDistance[j]);
    }

    /* Compute transmittance from O to sample point, and then from sample
     * point through to the light hit. */
    float3 T_oToSample = SAMPLE_TEXTURE2D_LOD(_T,
      s_linear_clamp_sampler, oToSample, 0).xyz;

    /* Our transmittance value for O to the sample point is too large---we
     * need to divide out the transmittance from the sample point to the
     * atmosphere, or ground, depending on what we hit. */
    float2 sampleOut = mapSky2DCoord(length(samplePoint),
      clampCosine(dot(normalizedSamplePoint, d)), _atmosphereRadius,
      _planetRadius, t_hit - sampleT, groundHit, _resT.y);
    float3 T_sampleOut = SAMPLE_TEXTURE2D_LOD(_T,
      s_linear_clamp_sampler, sampleOut, 0).xyz;
    T_oToSample -= T_sampleOut;

    for (int j = 0; j < _numActiveLayers; j++) {
      result.noShadows[j] += scaledDensity[j] * saturate(exp(T_oToSample));
    }

    /* Trace a ray from the sample point to the light to check visibility. */
    SkyIntersectionData lightIntersection = traceSkyVolume(samplePoint,
      L, _planetRadius, _atmosphereRadius);

    if (!lightIntersection.groundHit) {
      float3 lightEndPoint = samplePoint + L * lightIntersection.endT;
      float t_light_hit = lightIntersection.endT;

      /* Compute the light transmittance to the sample point. */
      float2 sampleToL = mapSky2DCoord(length(samplePoint),
        clampCosine(dot(normalizedSamplePoint, L)), _atmosphereRadius,
        _planetRadius, t_light_hit, lightIntersection.groundHit, _resT.y);
      /* Compute transmittance through sample to light hit point. */
      float3 T_sampleToL = SAMPLE_TEXTURE2D_LOD(_T,
        s_linear_clamp_sampler, sampleToL, 0).xyz;

      float3 T = saturate(exp(T_oToSample + T_sampleToL));
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

SSResult computeSSBody(float3 O, float3 d, float t_hit, bool groundHit, float3 L,
  float3 lightColor) {
  SSLayersResult ssLayers = computeSSLayers(O, d, t_hit, groundHit, L);
  float dot_L_d = dot(L, d);
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    float phase = computePhase(dot_L_d, _layerAnisotropy[i], _layerPhaseFunction[i]);
    result.shadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.shadows[i] * phase);
    result.noShadows += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * (ssLayers.noShadows[i] * phase);
  }
  result.shadows *= lightColor;
  result.noShadows *= lightColor;
  return result;
}

SSResult computeSS(float3 O, float3 d, float t_hit, bool groundHit) {
  SSResult result;
  result.shadows = float3(0, 0, 0);
  result.noShadows = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    SSResult bodySS = computeSSBody(O, d, t_hit, groundHit, _bodyDirection[i], _bodyLightColor[i]);
    result.shadows += bodySS.shadows;
    // result.shadows += 500 * (d+1); // DEBUG
    result.noShadows += bodySS.noShadows;
  }
  return result;
}
