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

/* Night Sky. TODO */
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
float _twinkleAmplitude;

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
  /* Sample the sky texture. */
  float3 nightSkyColor = _nightSkyTint.xyz;
  float3 starColor = float3(0, 0, 0);
  float3 textureCoordinate = mul(d, (float3x3)_nightSkyRotation);
  if (_hasNightSkyTexture) {
    starColor = SAMPLE_TEXTURECUBE_LOD(_nightSkyTexture,
      s_linear_clamp_sampler, textureCoordinate, 0).xyz;
  }

  /* Calculate the twinkling effect. */
  float twinkle = 1;
  if (_useTwinkle) {
    float magnitude = dot(starColor, starColor) / 3.0;
    if (magnitude > _twinkleThreshold) {
      float phase = 2 * PI * random_3_1(textureCoordinate);
      float frequency = _twinkleFrequencyMin +
        (_twinkleFrequencyMax - _twinkleFrequencyMin) * random_3_1(textureCoordinate * 1.37);
      twinkle = max(0, _twinkleAmplitude * pow(sin(frequency * _Time.y + phase), 2) + _twinkleBias);
    }
  }
  return twinkle * nightSkyColor * starColor;
}

struct SkyColor_t {
  float3 ss;
  float3 ms;
};

SkyColor_t computeSkyColorBody(float2 r_mu_uv, int i, float3 start, float3 d,
  float t_hit, bool groundHit, bool geoHit) {
  /* Final reuslt. */
  SkyColor_t result;
  result.ss = float3(0, 0, 0);
  result.ms = float3(0, 0, 0);

  /* Get the body's direction. */
  float3 L = _bodyDirection[i];
  float3 lightColor = _bodyLightColor[i].xyz;
  float dot_L_d = clampCosine(dot(L, d));

  /* Now, technically, this is a hack. But it's a good hack for far
   * away geo. We basically see how "behind" the geo the light is by
   * checking how parallel the view and light vectors are. If they're
   * really parallel, that means the light is totally behind the geo.
   * If they're less parallel, then the light is less behind the geo. */
  float occlusionMultiplier = (geoHit) ? pow(1-saturate(dot_L_d), 1) : 1.0;

  /* TODO: how to figure out if body is occluded? Could use global bool array.
   * But need to do better. Need to figure out HOW occluded and attenuate
   * light accordingly. May need to do this with horizon too. */

  /* Compute 4D tex coords. */
  float3 startNormalized = normalize(start);
  float mu_l = clampCosine(dot(startNormalized, L));
  float3 proj_L = normalize(L - startNormalized * mu_l);
  float3 proj_d = normalize(d - startNormalized * dot(startNormalized, d));
  float nu = clampCosine(dot(proj_L, proj_d));
  TexCoord4D uvSS = mapSky4DCoord(r_mu_uv, mu_l, nu,
    _atmosphereRadius, _planetRadius, t_hit, groundHit,
    _resSS.x, _resSS.y, _resSS.z, _resSS.w);
  TexCoord4D uvMSAcc = mapSky4DCoord(r_mu_uv, mu_l, nu,
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
    result.ss += _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ss * phase);
    result.ms += _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ms * _layerMultipleScatteringMultiplier[j]);
  }
  result.ss *= t_hit * occlusionMultiplier * lightColor;
  result.ms *= t_hit * occlusionMultiplier * lightColor;
  return result;
}

/* Given uv coordinate representing direction, computes sky color. */
SkyColor_t computeSkyColor(float2 r_mu_uv, float3 start, float3 d, float t_hit,
  bool groundHit, bool geoHit) {
  SkyColor_t result;
  result.ss = float3(0, 0, 0);
  result.ms = float3(0, 0, 0);
  /* Loop through all the celestial bodies. */
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    SkyColor_t bodyResult = computeSkyColorBody(r_mu_uv, i, start, d, t_hit,
      groundHit, geoHit);
    result.ss += bodyResult.ss;
    result.ms += bodyResult.ms;
  }
  return result;
}

float3 computeLightPollutionColor(float2 uv, float t_hit) {
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    float3 lp = sampleLPTexture(uv, i);
    color += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz) * lp;
  }
  color *= _lightPollutionTint; // TODO: light pollution intensity and tint controls
  return t_hit * color;
}

float3 computeStarScatteringColor(float2 r_mu_uv, float mu, float3 directLight,
  float t_hit, bool groundHit) {
  /* HACK: to get some sort of approximation of rayleigh scattering
   * for the ambient night color of the sky,  */
  TexCoord4D uvSS = mapSky4DCoord(r_mu_uv, mu, 1, _atmosphereRadius,
    _planetRadius, t_hit, groundHit, _resSS.x, _resSS.y, _resSS.z, _resSS.w);
  TexCoord4D uvMSAcc = mapSky4DCoord(r_mu_uv, mu, 1, _atmosphereRadius,
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
    color +=  t_hit * _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ss + ms * _layerMultipleScatteringMultiplier[j]);
  }

  return color * _nightSkyScatterTint * _averageNightSkyColor;
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

  /* Sample the depth buffer and figure out if we hit anything. */
  float depth = LoadCameraDepth(input.positionCS.xy);
  depth = LinearEyeDepth(depth, _ZBufferParams);
  bool geoHit = depth < t_hit && depth < _ProjectionParams.z - 0.001;

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
    t_hit, intersection.groundHit);

  /* Compute transmittance. */
  float3 transmittanceRaw = computeSkyTransmittanceRaw(coord2D);
  float3 transmittance = exp(transmittanceRaw);

  /* Compute sky color. */
  SkyColor_t skyColor = computeSkyColor(coord2D, startPoint, d, t_hit,
    intersection.groundHit, geoHit);

  /* Attenuate sky color and compute blend transmittance if we hit
   * something and are rendering fullscreen. For the cubemap, we just
   * want the sky, no geo. TODO: artifacts come from ground scattering!! */
  float3 blendTransmittance = float3(0, 0, 0);
  if (geoHit && !cubemap) {
    float3 depthSamplePoint = startPoint + d * depth;
    float depthR = length(depthSamplePoint);
    float depthMu = dot(normalize(depthSamplePoint), d);
    float2 depthCoord2D = mapSky2DCoord(depthR, depthMu, _atmosphereRadius,
      _planetRadius, t_hit-depth, intersection.groundHit);
    float3 aerialPerspectiveTransmittanceRaw = computeSkyTransmittanceRaw(depthCoord2D);
    SkyColor_t attenuatedSkyColor = computeSkyColor(depthCoord2D, depthSamplePoint, d, t_hit-depth,
      intersection.groundHit, geoHit);
    blendTransmittance = exp(transmittanceRaw - aerialPerspectiveTransmittanceRaw);
    skyColor.ss -= blendTransmittance * min(skyColor.ss, attenuatedSkyColor.ss); // TODO: ms fucks this up, just don't use, return struct from compute sky color
    skyColor.ss = max(0, skyColor.ss);
    skyColor.ms = float3(0, 0, 0); // Don't use MS if we hit geo.
  }

  /* Compute light pollution. TODO: attenuate for aerial perspective!!! or
   * maybe just don't render. */
  float3 lightPollution = float3(0, 0, 0);
  if (!geoHit || cubemap) {
    lightPollution = computeLightPollutionColor(coord2D, t_hit);
  }

  /* Compute star scattering. TODO: attenuate for aerial perspective!!! or
   * maybe just don't render. */
  float3 starScattering = float3(0, 0, 0);
  if (!geoHit || cubemap) {
    starScattering = computeStarScatteringColor(coord2D, mu,
      directLightNightSky, t_hit, intersection.groundHit);
  }

  /* Final result. */
  return float4(directLight * transmittance + skyColor.ss + skyColor.ms + lightPollution
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
