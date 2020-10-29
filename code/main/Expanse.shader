Shader "Hidden/HDRP/Sky/Expanse"
{

/******************************************************************************/
/********************************** INCLUDES **********************************/
/******************************************************************************/

HLSLINCLUDE

#pragma vertex Vert

#pragma editor_sync_compilation
#pragma target 4.5
#pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/CookieSampling.hlsl"

#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../common/shaders/ExpanseRandom.hlsl"
#include "../sky/ExpanseSkyMapping.hlsl"
#include "../sky/ExpanseStarCommon.hlsl"

/******************************************************************************/
/******************************** END INCLUDES ********************************/
/******************************************************************************/



/******************************************************************************/
/****************************** INPUT VARIABLES *******************************/
/******************************************************************************/

/* Atmosphere Layers. */
float _layerMultipleScatteringMultiplier[MAX_LAYERS];

/* Celestial Bodies. */

#define MAX_BODIES 8

// need a num active bodies parameter
int _numActiveBodies;
float3 _bodyDirection[MAX_BODIES];
float _bodyAngularRadius[MAX_BODIES];
float _bodyDistance[MAX_BODIES];
bool _bodyReceivesLight[MAX_BODIES];
float4x4 _bodyAlbedoTextureRotation[MAX_BODIES];
float4 _bodyAlbedoTint[MAX_BODIES];
bool _bodyEmissive[MAX_BODIES];
float4 _bodyLightColor[MAX_BODIES];
float _bodyLimbDarkening[MAX_BODIES];
float4x4 _bodyEmissionTextureRotation[MAX_BODIES];
float4 _bodyEmissionTint[MAX_BODIES];

// Textures can't be array, so declare them individually.
bool _bodyAlbedoTextureEnabled[MAX_BODIES];
TEXTURECUBE(_bodyAlbedoTexture0);
TEXTURECUBE(_bodyAlbedoTexture1);
TEXTURECUBE(_bodyAlbedoTexture2);
TEXTURECUBE(_bodyAlbedoTexture3);
TEXTURECUBE(_bodyAlbedoTexture4);
TEXTURECUBE(_bodyAlbedoTexture5);
TEXTURECUBE(_bodyAlbedoTexture6);
TEXTURECUBE(_bodyAlbedoTexture7);
bool _bodyEmissionTextureEnabled[MAX_BODIES];
TEXTURECUBE(_bodyEmissionTexture0);
TEXTURECUBE(_bodyEmissionTexture1);
TEXTURECUBE(_bodyEmissionTexture2);
TEXTURECUBE(_bodyEmissionTexture3);
TEXTURECUBE(_bodyEmissionTexture4);
TEXTURECUBE(_bodyEmissionTexture5);
TEXTURECUBE(_bodyEmissionTexture6);
TEXTURECUBE(_bodyEmissionTexture7);

/* Night Sky. */
bool _useProceduralNightSky;
TEXTURE2D_ARRAY(_Star);
float4 _lightPollutionTint;   /* Tint and intensity. */
bool _hasNightSkyTexture;
TEXTURECUBE(_nightSkyTexture);
float4 _nightSkyTint;         /* Tint and intensity. */
float4x4 _nightSkyRotation;
float4 _averageNightSkyColor;
float4 _nightSkyScatterTint;  /* Tint and intensity. */
bool _useTwinkle;
float _twinkleThreshold;
float _twinkleFrequencyMin;
float _twinkleFrequencyMax;
float _twinkleBias;
float _twinkleSmoothAmplitude;
float _twinkleChaoticAmplitude;

/* Aerial Perspective. */
float _aerialPerspectiveOcclusionBiasUniform;
float _aerialPerspectiveOcclusionPowerUniform;
float _aerialPerspectiveOcclusionBiasDirectional;
float _aerialPerspectiveOcclusionPowerDirectional;

/* Quality. */
bool _useAntiAliasing;
float _ditherAmount;

/* Render textures. */
TEXTURE2D(_fullscreenSkyColorRT);
TEXTURE2D(_cubemapSkyColorRT);
TEXTURE2D(_lastFullscreenCloudColorRT);
TEXTURE2D(_lastFullscreenCloudTransmittanceRT);
TEXTURE2D(_lastCubemapCloudColorRT);
TEXTURE2D(_lastCubemapCloudTransmittanceRT);
TEXTURE2D(_currFullscreenCloudColorRT);
TEXTURE2D(_currFullscreenCloudTransmittanceRT);
TEXTURE2D(_currCubemapCloudColorRT);
TEXTURE2D(_currCubemapCloudTransmittanceRT);

float3 _WorldSpaceCameraPos1;
float4x4 _ViewMatrix1;
#undef UNITY_MATRIX_V
#define UNITY_MATRIX_V _ViewMatrix1

/******************************************************************************/
/**************************** END INPUT VARIABLES *****************************/
/******************************************************************************/



/******************************************************************************/
/********************************* I/O TYPES **********************************/
/******************************************************************************/

struct Attributes
{
  uint vertexID : SV_VertexID;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
  float4 positionCS : SV_POSITION;
  float2 screenPosition : TEXCOORD0;
  UNITY_VERTEX_OUTPUT_STEREO
};

struct CloudResult {
  float4 color : SV_Target0;
  float4 transmittance : SV_Target1;
};

/******************************************************************************/
/******************************* END I/O TYPES ********************************/
/******************************************************************************/



/******************************************************************************/
/******************************* VERTEX SHADER ********************************/
/******************************************************************************/

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
    output.screenPosition = GetFullScreenTriangleTexCoord(input.vertexID);
    return output;
}

/******************************************************************************/
/***************************** END VERTEX SHADER ******************************/
/******************************************************************************/



/******************************************************************************/
/***************************** TEXTURE SAMPLERS *******************************/
/******************************************************************************/

/* Sadly, there's no way to have an array of textures in hlsl. Well, there is
 * as a texture array, but in that case all textures need to be the same
 * resolution. So we have to do something hacky. */

float3 sampleBodyEmissionTexture(float3 uv, int i) {
  switch(i) {
    case 0:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture0, s_linear_clamp_sampler, uv, 0).xyz;
    case 1:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture1, s_linear_clamp_sampler, uv, 0).xyz;
    case 2:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture2, s_linear_clamp_sampler, uv, 0).xyz;
    case 3:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture3, s_linear_clamp_sampler, uv, 0).xyz;
    case 4:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture4, s_linear_clamp_sampler, uv, 0).xyz;
    case 5:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture5, s_linear_clamp_sampler, uv, 0).xyz;
    case 6:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture6, s_linear_clamp_sampler, uv, 0).xyz;
    case 7:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture7, s_linear_clamp_sampler, uv, 0).xyz;
    default:
      return float3(0, 0, 0);
  }
}

float3 sampleBodyAlbedoTexture(float3 uv, int i) {
  switch(i) {
    case 0:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture0, s_linear_clamp_sampler, uv, 0).xyz;
    case 1:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture1, s_linear_clamp_sampler, uv, 0).xyz;
    case 2:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture2, s_linear_clamp_sampler, uv, 0).xyz;
    case 3:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture3, s_linear_clamp_sampler, uv, 0).xyz;
    case 4:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture4, s_linear_clamp_sampler, uv, 0).xyz;
    case 5:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture5, s_linear_clamp_sampler, uv, 0).xyz;
    case 6:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture6, s_linear_clamp_sampler, uv, 0).xyz;
    case 7:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture7, s_linear_clamp_sampler, uv, 0).xyz;
    default:
      return float3(0, 0, 0);
  }
}

/******************************************************************************/
/*************************** END TEXTURE SAMPLERS *****************************/
/******************************************************************************/



/******************************************************************************/
/**************************** SKY FRAGMENT SHADER *****************************/
/******************************************************************************/

/* Given uv coodinate representing direction, computes sky transmittance. */
float3 computeSkyTransmittance(float2 uv) {
  return exp(SAMPLE_TEXTURE2D_LOD(_T, s_linear_clamp_sampler, uv, 0).xyz);
}

/* Given uv coodinate representing direction, computes sky transmittance. */
float3 computeSkyTransmittanceRaw(float2 uv) {
  return SAMPLE_TEXTURE2D_LOD(_T, s_linear_clamp_sampler, uv, 0).xyz;
}

float3 shadeGround(float3 endPoint) {
  float3 color = float3(0, 0, 0);
  float3 endPointNormalized = normalize(endPoint);

  /* Compute albedo, emission. */
  float3 albedo = _groundTint;
  float3 uv = mul(endPointNormalized, (float3x3) _planetRotation).xyz;
  if (_groundAlbedoTextureEnabled) {
    albedo *= 2.0 *
      SAMPLE_TEXTURECUBE_LOD(_groundAlbedoTexture, s_linear_clamp_sampler, uv, 0).xyz;
  } else {
    albedo /= PI;
  }

  float3 emission = float3(0, 0, 0);
  if (_groundEmissionTextureEnabled) {
    emission = _groundEmissionMultiplier *
      SAMPLE_TEXTURECUBE_LOD(_groundEmissionTexture, s_linear_clamp_sampler, uv, 0).xyz;
  }

  /* Loop over all the celestial bodies. TODO: could check occlusion for
   * celestial bodies. */
  for (int i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    float3 lightColor = _bodyLightColor[i];
    float3 cosTheta = dot(endPointNormalized, L);

    /* Loop over all the layers to accumulate ground irradiance. */
    float mu_L = dot(L, endPointNormalized);
    float2 uvGI = mapSky1DCoord(mu_L);
    float3 giAcc = float3(0, 0, 0);
    for (int j = 0; j < _numActiveLayers; j++) {
      float3 gi = sampleGITexture(uvGI, j);
      giAcc += _layerCoefficientsS[j].xyz * 2.0 * _layerTint[j].xyz * gi;
    }

    color += albedo * (giAcc * lightColor + cosTheta * lightColor) + emission;
  }
  return color;
}

/* Compute the luminance multiplier of a celestial body given the illuminance
 * and the cosine of half the angular extent. */
float3 computeCelestialBodyLuminanceMultiplier(float cosTheta) {
  /* Compute solid angle. */
  float solidAngle = 2.0 * PI * (1.0 - cosTheta);
  return 1.0 / solidAngle;
}

float3 limbDarkening(float dot_L_d, float cosTheta, float amount) {
  float centerToEdge = 1.0 - abs((dot_L_d - cosTheta) / (1.0 - cosTheta));
  float mu = safeSqrt(1.0 - centerToEdge * centerToEdge);
  float mu2 = mu * mu;
  float mu3 = mu2 * mu;
  float mu4 = mu2 * mu2;
  float mu5 = mu3 * mu2;
  float3 a0 = float3 (0.34685, 0.26073, 0.15248);
  float3 a1 = float3 (1.37539, 1.27428, 1.38517);
  float3 a2 = float3 (-2.04425, -1.30352, -1.49615);
  float3 a3 = float3 (2.70493, 1.47085, 1.99886);
  float3 a4 = float3 (-1.94290, -0.96618, -1.48155);
  float3 a5 = float3 (0.55999, 0.26384, 0.44119);
  return max(0.0, pow(a0 + a1 * mu + a2 * mu2 + a3 * mu3 + a4 * mu4 + a5 * mu5, amount));
}

/* Given direction to sample in, shades closest celestial body. Returns
 * negative number if nothing is hit. */
float3 shadeClosestCelestialBody(float3 d) {
  /* First, compute closest intersection. */
  int minIdx = -1;
  float minDist = FLT_MAX;
  for (int i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    float cosTheta = dot(L, d);
    if (cosTheta > cos(_bodyAngularRadius[i]) && _bodyDistance[i] < minDist) {
      minIdx = i;
      minDist = _bodyDistance[i];
    }
  }

  /* If we didn't hit anything, there's nothing to shade. Return negative
   * number to indicate that we hit nothing. */
  if (minIdx < 0) {
    return float3(-1, -1, -1);
  }

  /* Otherwise, compute lighting. */
  float3 directLighting = float3(0, 0, 0);

  /* Compute illumination. */
  if (_bodyReceivesLight[minIdx]) {
    /* We have to do some work to compute the surface normal of the body
     * to light it. */
    float3 L = _bodyDirection[minIdx];
    float dist = _bodyDistance[minIdx];
    float sinInner = sin(_bodyAngularRadius[minIdx]);
    float bodyRadius = sinInner * dist;
    float3 planetOriginInBodyFrame = -(L * dist);

    /* Intersect the body at the point we're looking at. */
    float3 bodyIntersection = intersectSphere(planetOriginInBodyFrame, d, bodyRadius);
    float3 bodyIntersectionPoint = planetOriginInBodyFrame
      + minNonNegative(bodyIntersection.x, bodyIntersection.y) * d;
    float3 surfaceNormal = normalize(bodyIntersectionPoint);

    /* Compute the body's albedo. */
    float3 albedo = _bodyAlbedoTint[minIdx].xyz;
    if (_bodyAlbedoTextureEnabled[minIdx]) {
      float3 albedoTex = sampleBodyAlbedoTexture(
        mul(surfaceNormal, (float3x3) _bodyAlbedoTextureRotation[minIdx]), minIdx);
      albedo *= albedoTex * 2;
    } else {
      albedo /= PI;
    }

    /* Now, have to loop over bodies again to illuminate. */
    float3 bodyPosition = _bodyDirection[minIdx] * _bodyDistance[minIdx];
    for (int i = 0; i < _numActiveBodies; i++) {
      if (i != minIdx && _bodyEmissive[i]) {
        /* No interreflections between bodies---only emissive bodies can
         * light non-emissive bodies. Also, for now we won't model
         * occlusion by the earth; though TODO: it wouldn't be so bad. */
         float3 emissivePosition = _bodyDistance[i] * _bodyDirection[i];
         float3 emissiveDir = normalize(emissivePosition - bodyPosition);
         directLighting += saturate(dot(emissiveDir, surfaceNormal))
           * _bodyLightColor[i].xyz * albedo;
      }
    }
  }

  /* Compute emission. */
  if (_bodyEmissive[minIdx]) {
    /* We have to do some work to compute the intersection. */
    float3 L = _bodyDirection[minIdx];
    float3 emission = _bodyEmissionTint[minIdx].xyz;
    float cosInner = cos(_bodyAngularRadius[minIdx]);
    if (_bodyEmissionTextureEnabled[minIdx]) {
      float dist = _bodyDistance[minIdx];
      float sinInner = safeSqrt(1 - cosInner * cosInner);
      float bodyRadius = sinInner * dist;
      float3 planetOriginInBodyFrame = -(L * dist);
      /* Intersect the body at the point we're looking at. */
      float3 bodyIntersection = intersectSphere(planetOriginInBodyFrame, d, bodyRadius);
      float3 bodyIntersectionPoint = planetOriginInBodyFrame
        + minNonNegative(bodyIntersection.x, bodyIntersection.y) * d;
      float3 surfaceNormal = normalize(bodyIntersectionPoint);
      float3 emissionTex = sampleBodyEmissionTexture(
        mul(surfaceNormal, (float3x3) _bodyEmissionTextureRotation[minIdx]), minIdx);
      emission *= emissionTex;
    } else {
      emission *= _bodyLightColor[minIdx].xyz;
    }
    emission *= limbDarkening(dot(L, d), cosInner, _bodyLimbDarkening[minIdx]);
    emission *= computeCelestialBodyLuminanceMultiplier(cosInner);
    directLighting += emission;
  }

  return directLighting;
}

/* Given direction to sample in, shades direct light from night sky. */
float3 shadeNightSky(float3 d) {
  float3 textureCoordinate = mul(d, (float3x3)_nightSkyRotation);
  /* Special case things out for procedural and texture options. */
  if (_useProceduralNightSky) {
    /* Stars. */
    float3 proceduralTextureCoordinate = directionToTex2DArrayCubemapUV(textureCoordinate);
    float4 colorAndSeed = SAMPLE_TEXTURE2D_ARRAY_LOD(_Star, s_linear_clamp_sampler,
      proceduralTextureCoordinate.xy, proceduralTextureCoordinate.z, 0);
    float3 starColor = colorAndSeed.xyz;
    float starSeed = colorAndSeed.w;
    float3 directionSeed = random_3_1(textureCoordinate);
    float twinkleCoarse = 1;
    float twinkleFine = 1;
    if (_useTwinkle) {
      float magnitude = dot(starColor, starColor) / 3.0;
      if (magnitude > _twinkleThreshold) {
        /* Coarse-grained twinkle. */
        float phase = 2 * PI * starSeed;
        float frequency = _twinkleFrequencyMin +
          (_twinkleFrequencyMax - _twinkleFrequencyMin) * random_1_1(starSeed * 1.37);
        twinkleCoarse = max(0, _twinkleSmoothAmplitude * pow(sin(frequency * _Time.y + phase), 2) + _twinkleBias);
        /* Fine-grained twinkle. */
        phase = 2 * PI * directionSeed;
        frequency = _twinkleFrequencyMin +
          (_twinkleFrequencyMax - _twinkleFrequencyMin) * random_1_1(directionSeed * 1.37);
        twinkleFine = max(0, _twinkleChaoticAmplitude * pow(sin(frequency * _Time.y + phase), 2) + _twinkleBias);
      }
    }

    /* Nebulae. */
    float3 nebulaeColor = float3(0, 0, 0);
    if (_useProceduralNebulae) {
      float4 nebulaeColorAndAlpha = SAMPLE_TEXTURE2D_ARRAY_LOD(_proceduralNebulae,
        s_linear_clamp_sampler, proceduralTextureCoordinate.xy, proceduralTextureCoordinate.z, 0);
      nebulaeColor = pow(nebulaeColorAndAlpha.xyz, _nebulaOverallDefinition) * _nebulaOverallIntensity;
      float nebulaeAlpha = nebulaeColorAndAlpha.w;
      nebulaeColor *= nebulaeAlpha;
      // Blend probabilistically.
      float starNebulaeBlendAmount = random_1_1(starSeed * 3.92853);
      starColor *= 1-(starNebulaeBlendAmount * nebulaeAlpha);
    } else {
      if (_hasNebulaeTexture) {
        nebulaeColor = _nebulaOverallIntensity * SAMPLE_TEXTURECUBE_LOD(_nebulaeTexture,
          s_linear_clamp_sampler, textureCoordinate, 0);
      }
    }
    return ((twinkleCoarse + twinkleFine) * starColor + nebulaeColor) * _nightSkyTint.xyz;
  } else {
    if (!_hasNightSkyTexture) {
      return _nightSkyTint.xyz;
    }
    float3 starColor = SAMPLE_TEXTURECUBE_LOD(_nightSkyTexture,
      s_linear_clamp_sampler, textureCoordinate, 0).xyz;
    float3 directionSeed = random_3_1(textureCoordinate);
    float twinkle = 1;
    if (_useTwinkle) {
      float magnitude = dot(starColor, starColor) / 3.0;
      if (magnitude > _twinkleThreshold) {
        float phase = 2 * PI * directionSeed;
        float frequency = _twinkleFrequencyMin +
          (_twinkleFrequencyMax - _twinkleFrequencyMin) * random_1_1(directionSeed * 1.37);
        twinkle = max(0, _twinkleSmoothAmplitude * pow(sin(frequency * _Time.y + phase), 2) + _twinkleBias);
      }
    }
    return twinkle * _nightSkyTint.xyz * starColor;
  }
}

int computeAerialPerspectiveLOD(float depth) {
  if (depth < _aerialPerspectiveTableDistanceLOD0) {
    return AERIAL_PERPSECTIVE_LOD0;
  } else if (depth < _aerialPerspectiveTableDistanceLOD1) {
    return AERIAL_PERPSECTIVE_LOD1;
  } else {
    return AERIAL_PERPSECTIVE_LOD2;
  }
}

float computeAerialPerspectiveLODBlend(int LOD, float depth) {
  switch (LOD) {
    case AERIAL_PERPSECTIVE_LOD0:
      /* Lerp for last 25 percent of interval. */
      return 1 - saturate((_aerialPerspectiveTableDistanceLOD0 - depth) / (0.5 * _aerialPerspectiveTableDistanceLOD0));
    case AERIAL_PERPSECTIVE_LOD1:
      /* Lerp for last 25 percent of interval. */
      return 1 - saturate((_aerialPerspectiveTableDistanceLOD1 - depth) / (0.5 * _aerialPerspectiveTableDistanceLOD1));
    case AERIAL_PERPSECTIVE_LOD2:
      return 0;
    default:
      return 0;
  }
}

float computeAerialPerspectiveLODDistance(int LOD, float t_hit) {
  switch (LOD) {
    case AERIAL_PERPSECTIVE_LOD0:
      return min(t_hit, _aerialPerspectiveTableDistanceLOD0);
    case AERIAL_PERPSECTIVE_LOD1:
      return min(t_hit, _aerialPerspectiveTableDistanceLOD1);
    case AERIAL_PERPSECTIVE_LOD2:
      return t_hit;
    default:
      return 0;
  }
}

float3 computeSkyColorBody(float r, float mu, int i, float3 start, float3 d,
  float t_hit, bool groundHit, float interval_length) {
  /* Final result. */
  float3 result = float3(0, 0, 0);

  /* Get the body's direction. */
  float3 L = _bodyDirection[i];
  float3 lightColor = _bodyLightColor[i].xyz;
  float dot_L_d = clampCosine(dot(L, d));

  /* TODO: how to figure out if body is occluded? Could use global bool array.
   * But need to do better. Need to figure out HOW occluded and attenuate
   * light accordingly. May need to do this with horizon too. */

  /* Compute 4D tex coords. */
  float3 startNormalized = normalize(start);
  float mu_l = clampCosine(dot(startNormalized, L));
  float3 proj_L = normalize(L - startNormalized * mu_l);
  float3 proj_d = normalize(d - startNormalized * dot(startNormalized, d));
  float nu = clampCosine(dot(proj_L, proj_d));
  TexCoord4D uvSS = mapSky4DCoord(r,  mu, mu_l, nu,
    _atmosphereRadius, _planetRadius, t_hit, groundHit,
    _resSS.x, _resSS.y, _resSS.z, _resSS.w);
  TexCoord4D uvMSAcc = mapSky4DCoord(r, mu, mu_l, nu,
    _atmosphereRadius, _planetRadius, t_hit, groundHit,
    _resMSAcc.x, _resMSAcc.y, _resMSAcc.z, _resMSAcc.w);

  /* Loop through layers and accumulate contributions for this body. */
  for (int j = 0; j < _numActiveLayers; j++) {
    /* Single scattering. */
    float phase = computePhase(dot_L_d, _layerAnisotropy[j], _layerPhaseFunction[j]);
    float3 ss = sampleSSTexture(uvSS, j);

    /* Multiple scattering. */
    float3 ms = sampleMSAccTexture(uvMSAcc, j);

    /* Final color. */
    result += _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ss * phase + ms * _layerMultipleScatteringMultiplier[j]);
  }
  return result * interval_length * lightColor;
}

/* Given uv coordinate representing direction, computes sky color. */
float3 computeSkyColor(float r, float mu, float3 start, float3 d, float t_hit,
  bool groundHit, float interval_length) {
  float3 result = float3(0, 0, 0);
  /* Loop through all the celestial bodies. */
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    result += computeSkyColorBody(r, mu, i, start, d, t_hit,
      groundHit, interval_length);
  }
  return result;
}

float3 computeAerialPerspectiveColorBody(float r, float mu, float depthR, float depthMu,
  float3 start, float3 depthSamplePoint, float3 d, float t_hit, float depth,
  bool groundHit, float3 blendTransmittance, int i) {
  /* Final result. */
  float3 result = float3(0, 0, 0);

  /* Get the body's direction. */
  float3 L = _bodyDirection[i];
  float3 lightColor = _bodyLightColor[i].xyz;
  float dot_L_d = clampCosine(dot(L, d));

  /* Now, technically, this is a hack. But it's a good hack for far
   * away geo. We basically see how "behind" the geo the light is by
   * checking how parallel the view and light vectors are. If they're
   * really parallel, that means the light is totally behind the geo.
   * If they're less parallel, then the light is less behind the geo.
   * Then, we allow the user to tweak parameters until they're happy
   * with the result. */
  float occlusionMultiplierUniform = _aerialPerspectiveOcclusionBiasUniform
    + (1-_aerialPerspectiveOcclusionBiasUniform) * pow(1-saturate(dot_L_d), _aerialPerspectiveOcclusionPowerUniform);
  float occlusionMultiplierDirectional = _aerialPerspectiveOcclusionBiasDirectional
    + (1-_aerialPerspectiveOcclusionBiasDirectional) * pow(1-saturate(dot_L_d), _aerialPerspectiveOcclusionPowerDirectional);

  /* TODO: how to figure out if body is occluded? Could use global bool array.
   * But need to do better. Need to figure out HOW occluded and attenuate
   * light accordingly. May need to do this with horizon too. */

  /* Compute 4D tex coords. */
  float3 startNormalized = normalize(start);
  float mu_l = clampCosine(dot(startNormalized, L));
  float3 proj_L = normalize(L - startNormalized * mu_l);
  float3 proj_d = normalize(d - startNormalized * dot(startNormalized, d));
  float nu = clampCosine(dot(proj_L, proj_d));
  TexCoord4D uvSS = mapSky4DCoord(r, mu, mu_l, nu,
    _atmosphereRadius, _planetRadius, t_hit, groundHit,
    _resSS.x, _resSS.y, _resSS.z, _resSS.w);
  TexCoord4D uvMSAcc = mapSky4DCoord(r, mu, mu_l, nu,
    _atmosphereRadius, _planetRadius, t_hit, groundHit,
    _resMSAcc.x, _resMSAcc.y, _resMSAcc.z, _resMSAcc.w);

  float3 depthStartNormalized = normalize(depthSamplePoint);
  float depth_mu_l = clampCosine(dot(depthStartNormalized, L));
  float3 depth_proj_L = normalize(L - depthStartNormalized * mu_l);
  float3 depth_proj_d = normalize(d - depthStartNormalized * dot(depthStartNormalized, d));
  float depth_nu = clampCosine(dot(proj_L, proj_d));
  TexCoord4D depth_uvSS = mapSky4DCoord(depthR, depthMu, depth_mu_l, depth_nu,
    _atmosphereRadius, _planetRadius, t_hit-depth, groundHit,
    _resSS.x, _resSS.y, _resSS.z, _resSS.w);
  TexCoord4D depth_uvMSAcc = mapSky4DCoord(r, mu, mu_l, nu,
    _atmosphereRadius, _planetRadius, t_hit-depth, groundHit,
    _resMSAcc.x, _resMSAcc.y, _resMSAcc.z, _resMSAcc.w);

  /* Loop through layers and accumulate contributions for this body. */
  for (int j = 0; j < _numActiveLayers; j++) {
    /* Single scattering. */
    float phase = computePhase(dot_L_d, _layerAnisotropy[j], _layerPhaseFunction[j]);
    float3 ss = t_hit * sampleSSTexture(uvSS, j);
    float3 depth_ss = (t_hit - depth) * sampleSSTexture(depth_uvSS, j);
    ss = ss - blendTransmittance * depth_ss;

    float3 ms = t_hit * sampleMSAccTexture(uvMSAcc, j);
    float3 depth_ms = (t_hit - depth) * sampleMSAccTexture(depth_uvMSAcc, j);
    ms = ms - blendTransmittance * depth_ms;

    /* Final color. HACK: == 2 here is for Mie phase. If this changes,
     * we will need to change it. */
    result += _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ss * phase * ((_layerPhaseFunction[j] == 2) ? occlusionMultiplierDirectional
      : occlusionMultiplierUniform) + ms * _layerMultipleScatteringMultiplier[j]);
  }
  return result * lightColor;
}

/* Given uv coordinate representing direction, computes aerial perspective color. */
float3 computeAerialPerspectiveColor(float r, float mu, float depthR, float depthMu,
  float3 start, float3 depthSamplePoint, float3 d, float t_hit, float depth,
  bool groundHit, float3 blendTransmittance) {
  float3 result = float3(0, 0, 0);
  /* Loop through all the celestial bodies. */
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    result += computeAerialPerspectiveColorBody(r, mu, depthR, depthMu, start,
      depthSamplePoint, d, t_hit, depth, groundHit, blendTransmittance, i);
  }
  return result;
}

float3 computeLightPollutionColor(float2 uv, float t_hit) {
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    float3 lp = sampleLPTexture(uv, i);
    color += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * lp;
  }
  color *= _lightPollutionTint;
  return t_hit * color;
}

float3 computeStarScatteringColor(float r, float mu, float3 directLight,
  float t_hit, bool groundHit) {
  /* HACK: to get some sort of approximation of rayleigh scattering
   * for the ambient night color of the sky,  */
  TexCoord4D uvSS = mapSky4DCoord(r, mu, mu, 1, _atmosphereRadius,
    _planetRadius, t_hit, groundHit, _resSS.x, _resSS.y, _resSS.z, _resSS.w);
  TexCoord4D uvMSAcc = mapSky4DCoord(r, mu, mu, 1, _atmosphereRadius,
    _planetRadius, t_hit, groundHit, _resMSAcc.x, _resMSAcc.y, _resMSAcc.z, _resMSAcc.w);

  /* Accumulate contribution from each layer. */
  float3 color = float3(0, 0, 0);
  for (int j = 0; j < _numActiveLayers; j++) {
    /* Single scattering. Use isotropic phase, since this approximation
     * has no directionality. */
    float3 ss = sampleSSTexture(uvSS, j) * isotropicPhase();

    /* Multiple scattering. eyo */
    float3 ms = sampleMSAccTexture(uvMSAcc, j);

    /* Final color. */
    color += _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ss + ms * _layerMultipleScatteringMultiplier[j]);
  }

  return color * t_hit * _nightSkyScatterTint * _averageNightSkyColor;
}

float4 RenderSky(Varyings input, float3 O, float3 d, bool cubemap) {
  /* Trace a ray to see what we hit. */
  SkyIntersectionData intersection = traceSkyVolume(O, d, _planetRadius,
    _atmosphereRadius);

  /* You might think the start point is just O, but we may be looking
   * at the planet from space, in which case the start point is the point
   * that we hit the atmosphere. */
  float3 startPoint = O + d * intersection.startT;
  float3 endPoint = O + d * intersection.endT;
  float t_hit = intersection.endT - intersection.startT;

  /* Sample the depth buffer and figure out if we hit anything.
   * TODO: depth conversion here might not account for the fact that
   * depth changes across camera coordinate? no idea. */
  float depth = LoadCameraDepth(input.positionCS.xy);
  depth = Linear01Depth(depth, _ZBufferParams) * _ProjectionParams.z;
  /* Get camera center, and angle between direction and center. */
  float3 cameraCenterD = -GetSkyViewDirWS(float2(_ScreenParams.x/2, _ScreenParams.y/2));
  float cosTheta = dot(cameraCenterD, d);
  /* Divide depth through by cos theta. */
  depth /= max(cosTheta, 0.00001);
  float farClip = _ProjectionParams.z / cosTheta;

  bool geoHit = depth < t_hit && depth < farClip - 0.001;

  /* Compute direct illumination, but only if we don't hit anything. */
  float3 directLight = float3(0, 0, 0);
  float3 directLightNightSky = float3(0, 0, 0); /* Cached for scattering. */
  if (!geoHit) {
    if (intersection.groundHit) {
      directLight = shadeGround(endPoint);
    } else {
      /* Shade the closest celestial body and the stars. */
      directLight = shadeClosestCelestialBody(d);
      if (directLight.x < 0) {
        /* Shade the stars. */
        directLightNightSky = shadeNightSky(d);
        directLight = directLightNightSky;
      }
    }
  }

  /* If we didn't hit the ground or the atmosphere, return just direct
   * light. */
  if (!intersection.groundHit && !intersection.atmoHit) {
    return float4(directLight, 1);
  }

  /* Precompute 2D texture coordinate. */
  float r = length(startPoint);
  float mu = dot(normalize(startPoint), d);
  float2 coord2D = mapSky2DCoord(r, mu, _atmosphereRadius, _planetRadius,
    t_hit, intersection.groundHit, _resT.y);

  /* Compute transmittance. */
  float3 transmittanceRaw = computeSkyTransmittanceRaw(coord2D);
  float3 transmittance = exp(transmittanceRaw);

  /* Compute sky color and blend transmittance for aerial perspective. */
  float3 skyColor = float3(0, 0, 0);
  float3 blendTransmittance = float3(0, 0, 0);
  if (!geoHit || cubemap) {
    /* Just render the sky normally. */
    skyColor = computeSkyColor(r, mu, startPoint, d, t_hit,
      intersection.groundHit, t_hit);
  } else {
    /* We have to compute aerial perspective. First, compute blend
     * transmittance. */
    float3 depthSamplePoint = startPoint + d * depth;
    float depthR = length(depthSamplePoint);
    float depthMu = dot(normalize(depthSamplePoint), d);
    float2 depthCoord2D = mapSky2DCoord(depthR,
      depthMu, _atmosphereRadius, _planetRadius,
      t_hit-depth, intersection.groundHit, _resT.y);
    float3 aerialPerspectiveTransmittanceRaw = computeSkyTransmittanceRaw(depthCoord2D);
    blendTransmittance = saturate(exp(transmittanceRaw - aerialPerspectiveTransmittanceRaw));

    /* Now, compute sky color at correct LOD. */
    skyColor = computeAerialPerspectiveColor(r, mu, depthR, depthMu, startPoint,
      depthSamplePoint, d, t_hit, depth, intersection.groundHit, blendTransmittance);
  }

  /* Compute light pollution. */
  float3 lightPollution = float3(0, 0, 0);
  if (!geoHit || cubemap) {
    float2 coord2DLP = mapSky2DCoord(r, mu, _atmosphereRadius, _planetRadius,
      t_hit, intersection.groundHit, _resLP.y);
    lightPollution = computeLightPollutionColor(coord2DLP, t_hit);
  }

  /* Compute star scattering. */
  float3 starScattering = float3(0, 0, 0);
  if (!geoHit || cubemap) {
    starScattering = computeStarScatteringColor(r, mu,
      directLightNightSky, t_hit, intersection.groundHit);
  }

  /* Final result. */
  return float4(directLight * transmittance + skyColor + lightPollution
    + starScattering, dot(blendTransmittance, blendTransmittance)/3.0);
}

float4 SkyCubemap(Varyings input) : SV_Target {
  /* Compute origin point and sample direction. */
  float3 O = _WorldSpaceCameraPos1 - float3(0, -_planetRadius, 0);
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);
  return RenderSky(input, O, d, true);
}

float4 SkyFullscreen(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

  /* Compute origin point and sample direction. */
  float3 O = _WorldSpaceCameraPos1 - float3(0, -_planetRadius, 0);
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);

  /* If we aren't using anti-aliasing, just render. */
  if (!_useAntiAliasing) {
      return RenderSky(input, O, d, false);
  }

  /* Otherwise, see how close we are to the planet's edge, and if we are
   * close, then use MSAA 8x. TODO: do this. TODO: doesn't seem to work
   * for planet edge when close to ground---clue to why atmo is weird there too? */
  bool closeToEdge = true;
  if (closeToEdge) {
    float MSAA_8X_OFFSETS_X[8] = {1.0/16.0, -1.0/16.0, 5.0/16.0, -3.0/16.0, -5.0/16.0, -7.0/16.0, 3.0/16.0, 7.0/16.0};
    float MSAA_8X_OFFSETS_Y[8] =  {-3.0/16.0, 3.0/16.0, 1.0/16.0, -5.0/16.0, 5.0/16.0, -1.0/16.0, 7.0/16.0, -7.0/16.0};
    float4 result = float4(0, 0, 0, 0);
    for (int i = 0; i < 8; i++) {
      float3 dOffset = -GetSkyViewDirWS(input.positionCS.xy
        + float2(MSAA_8X_OFFSETS_X[i], MSAA_8X_OFFSETS_Y[i]));
      result += RenderSky(input, O, dOffset, false);
    }
    return result / 8.0;
  }

  return RenderSky(input, O, d, false);
}

/******************************************************************************/
/************************** END SKY FRAGMENT SHADER ***************************/
/******************************************************************************/



/******************************************************************************/
/*************************** CLOUDS FRAGMENT SHADER ***************************/
/******************************************************************************/

CloudResult RenderClouds(Varyings input, bool cubemap) {
  /* Final result. */
  CloudResult r;
  r.color = float4(1, 1, 1, 1);
  r.transmittance = float4(1, 1, 1, 1);
  return r;
}

CloudResult CloudsCubemap(Varyings input) : SV_Target {
  return RenderClouds(input, true);
}

CloudResult CloudsFullscreen(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
  return RenderClouds(input, false);
}

/******************************************************************************/
/************************* END CLOUDS FRAGMENT SHADER *************************/
/******************************************************************************/



/******************************************************************************/
/************************ COMPOSITING FRAGMENT SHADER *************************/
/******************************************************************************/

float4 Composite(Varyings input, bool cubemap, float exposure) {
  /* Get screenspace texture coordinate. */
  float2 textureCoordinate = input.screenPosition;

  /* Sample all of our fullscreen textures. */
  float4 skyCol = SAMPLE_TEXTURE2D_LOD(_fullscreenSkyColorRT,
    s_linear_clamp_sampler, textureCoordinate, 0);
  float3 cloudCol = SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudColorRT,
    s_linear_clamp_sampler, textureCoordinate, 0).xyz;
  float4 cloudTAndBlend = SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudTransmittanceRT,
    s_linear_clamp_sampler, textureCoordinate, 0);
  float3 cloudT = cloudTAndBlend.xyz;
  float3 cloudBlend = cloudTAndBlend.w;

  float3 finalColor = (exposure * skyCol.xyz);

  return float4(finalColor, skyCol.w);
}

float4 CompositeCubemap(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
  return Composite(input, true, 1.0);
}

float4 CompositeFullscreen(Varyings input) : SV_Target {
  return Composite(input, false, GetCurrentExposureMultiplier());
}

/******************************************************************************/
/********************** END COMPOSITING FRAGMENT SHADER ***********************/
/******************************************************************************/


ENDHLSL

SubShader
{
  /* Sky cubemap */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment SkyCubemap
    ENDHLSL
  }

  /* Sky fullscreen */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment SkyFullscreen
    ENDHLSL
  }

  /* Clouds cubemap */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment CloudsCubemap
    ENDHLSL
  }

  /* Clouds fullscreen */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment CloudsFullscreen
    ENDHLSL
  }

  /* Cubemap compositing. */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment CompositeCubemap
    ENDHLSL
  }

  /* Fullscreen compositing. */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend One SrcAlpha
    Cull Off

    HLSLPROGRAM
        #pragma fragment CompositeFullscreen
    ENDHLSL
  }

}
Fallback Off
}
