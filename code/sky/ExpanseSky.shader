Shader "Hidden/HDRP/Sky/ExpanseSky"
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

#include "ExpanseSkyCommon.hlsl"
#include "ExpanseSkyMapping.hlsl"

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

/* Quality. */
bool _useAntiAliasing;
float _ditherAmount;

/* Render textures. */
TEXTURE2D(_fullscreenSkyColorRT);
TEXTURE2D(_fullscreenSkyTransmittanceRT);
TEXTURE2D(_cubemapSkyColorRT);
TEXTURE2D(_cubemapSkyTransmittanceRT);
TEXTURE2D(_lastFullscreenCloudColorRT);
TEXTURE2D(_lastFullscreenCloudTransmittanceRT);
TEXTURE2D(_lastCubemapCloudColorRT);
TEXTURE2D(_lastCubemapCloudTransmittanceRT);
TEXTURE2D(_currFullscreenCloudColorRT);
TEXTURE2D(_currFullscreenCloudTransmittanceRT);
TEXTURE2D(_currCubemapCloudColorRT);
TEXTURE2D(_currCubemapCloudTransmittanceRT);
TEXTURE2D(_geometryColor);
TEXTURE2D(_aerialPerspective);

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

struct SkyResult {
  float4 color : SV_Target0;
  float4 transmittance : SV_Target1;
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
 * as a texture array, but unity doesn't support setting it. So we need to
 * do something hacky. */

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
  /* There's only one transmittance table to sample. */
  return SAMPLE_TEXTURE2D_LOD(_T, s_linear_clamp_sampler, uv, 0).xyz;
}

float3 shadeGround(float3 endPoint) {
  float3 color = float3(0, 0, 0);
  float3 endPointNormalized = normalize(endPoint);

  /* Compute albedo, emission. */
  float3 albedo = (_groundTint * 2.0) / PI;
  float3 uv = mul(endPointNormalized, (float3x3) _planetRotation).xyz;
  if (_groundAlbedoTextureEnabled) {
    albedo = (_groundTint * 2.0) *
      SAMPLE_TEXTURECUBE_LOD(_groundAlbedoTexture, s_linear_clamp_sampler, uv, 0).xyz;
  }

  float3 emission = float3(0, 0, 0);
  if (_groundEmissionTextureEnabled) {
    emission = _groundEmissionMultiplier *
      SAMPLE_TEXTURECUBE_LOD(_groundEmissionTexture, s_linear_clamp_sampler, uv, 0).xyz;
  }

  /* Loop over all the celestial bodies. */
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

/* Compute the luminance of a Celestial given the illuminance and the cosine
 * of half the angular extent. */
float3 computeCelestialBodyLuminanceMultiplier(float cosTheta) {
  /* Compute solid angle. */
  float solidAngle = 2.0 * PI * (1.0 - cosTheta);
  return 1.0 / solidAngle;
}


float3 limbDarkening(float dot_L_d, float cosTheta, float amount) {
  /* amount = max(FLT_EPS, amount); */
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

/* Given direction to sample in, shades closest celestial body.
 * TODO: luminance, not illuminance.
 * TODO: limb darkening. */
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

  /* If we didn't hit anything, there's nothing to shade. */
  if (minIdx < 0) {
    return float3(0, 0, 0);
  }

  /* Otherwise, compute lighting. */
  float3 directLighting = float3(0, 0, 0);
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

    float3 albedo = float3(0, 0, 0);
    if (_bodyAlbedoTextureEnabled[minIdx]) {
      float3 albedoTex = sampleBodyAlbedoTexture(
        mul(surfaceNormal, (float3x3) _bodyAlbedoTextureRotation[minIdx]), minIdx);
      albedo = albedoTex * 2 * _bodyAlbedoTint[minIdx].xyz / PI;
    } else {
      albedo = _bodyAlbedoTint[minIdx].xyz / PI;
    }

    /* Now, have to loop over bodies again to illuminate. */
    for (int i = 0; i < _numActiveBodies; i++) {
      if (i != minIdx && _bodyEmissive[i]) {
        /* No interreflections between bodies---only emissive bodies can
         * light non-emissive bodies. Also, for now we won't model
         * occlusion by the earth; though TODO: it wouldn't be so bad. */
         float3 emissivePosition = _bodyDistance[i] * _bodyDirection[i];
         float3 bodyPosition = _bodyDirection[minIdx] * _bodyDistance[minIdx];
         float3 emissiveDir = normalize(emissivePosition - bodyPosition);
         directLighting += saturate(dot(emissiveDir, surfaceNormal))
           * _bodyLightColor[i].xyz * albedo;
      }
    }
  }

  if (_bodyEmissive[minIdx]) {
    /* We have to do some work to compute the intersection. */
    float3 L = _bodyDirection[minIdx];
    float3 emission = float3(0, 0, 0);
    if (_bodyEmissionTextureEnabled[minIdx]) {
      float dist = _bodyDistance[minIdx];
      float sinInner = sin(_bodyAngularRadius[minIdx]);
      float bodyRadius = sinInner * dist;
      float3 planetOriginInBodyFrame = -(L * dist);
      /* Intersect the body at the point we're looking at. */
      float3 bodyIntersection = intersectSphere(planetOriginInBodyFrame, d, bodyRadius);
      float3 bodyIntersectionPoint = planetOriginInBodyFrame
        + minNonNegative(bodyIntersection.x, bodyIntersection.y) * d;
      float3 surfaceNormal = normalize(bodyIntersectionPoint);
      float3 emissionTex = sampleBodyEmissionTexture(
        mul(surfaceNormal, (float3x3) _bodyEmissionTextureRotation[minIdx]), minIdx);
      emission += emissionTex * _bodyEmissionTint[minIdx].xyz;
    } else {
      emission += _bodyLightColor[minIdx].xyz * _bodyEmissionTint[minIdx].xyz;
    }
    float cosInner = cos(_bodyAngularRadius[minIdx]);
    emission *= limbDarkening(dot(L, d), cosInner,
      _bodyLimbDarkening[minIdx]);
    emission *= computeCelestialBodyLuminanceMultiplier(cosInner);
    directLighting += emission;
  }


  return directLighting;
}

float3 computeSkyColorBody(float2 r_mu_uv, int i, float3 start, float3 d,
  float t_hit, bool groundHit, bool geoHit) {
  /* Get the body's direction. */
  float3 L = _bodyDirection[i];
  float3 lightColor = _bodyLightColor[i].xyz;
  float dot_L_d = dot(L, d);

  /* Now, technically, this is a hack. But it's a good hack for far
   * away geo. We basically treat the geo like a piece of cardboard.
   * If the light is coming from behind the geo, don't compute SS.
   * If it's in front of it, then do.
   * TODO: it's actually a bad hack. */
  bool computeSS = true;//!(geoHit && dot_L_d > 0.0);

  /* TODO: how to figure out if body is occluded? Could use global bool array.
   * But need to do better. Need to figure out HOW occluded and attenuate
   * light accordingly. May need to do this with horizon too. */

  /* Compute 4D tex coords. */
  float3 startNormalized = normalize(start);
  float mu_l = clampCosine(dot(normalize(startNormalized), L));
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
  float3 color = float3(0, 0, 0);
  for (int j = 0; j < _numActiveLayers; j++) {
    /* Single scattering. */
    float3 ss = float3(0, 0, 0);
    float phase = 0;
    if (computeSS) {
      phase = computePhase(dot_L_d, _layerAnisotropy[j], _layerPhaseFunction[j]);
      ss = sampleSSTexture(uvSS, j);
    }

    /* Multiple scattering. */
    float3 ms = sampleMSAccTexture(uvMSAcc, j);

    /* Final color. */
    color += lightColor * _layerCoefficientsS[j].xyz * (2.0 * _layerTint[j].xyz)
      * (ss * phase + ms * _layerMultipleScatteringMultiplier[j]);
  }

  return color;
}

/* Given uv coordinate representing direction, computes sky transmittance. */
float3 computeSkyColor(float2 r_mu_uv, float3 start, float3 d, float t_hit, bool groundHit, bool geoHit) {
  /* Loop through all the celestial bodies. */
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveBodies; i++) {
    color += computeSkyColorBody(r_mu_uv, i, start, d, t_hit, groundHit, geoHit);
  }
  return color;
}

float3 computeLightPollutionColor(float2 uv) {
  float3 color = float3(0, 0, 0);
  for (int i = 0; i < _numActiveLayers; i++) {
    float3 lp = sampleLPTexture(uv, i);
    color += _layerCoefficientsS[i].xyz * (2.0 * _layerTint[i].xyz)
      * lp;
  }
  color *= 0.0; // TODO: light pollution intensity and tint controls
  return color;
}

float3 computeStarScatteringColor() {
  /* TODO */
  float3 color = float3(0, 0, 0);
  return color;
}

float3 computeSkyBlendTransmittance(float depth, float t_hit,
  bool groundHit, float3 startPoint, float3 d, float3 outT) {
  /* If our hit point is further than the recorded depth, there's no object
   * to blend with, so our blend is entirely sky. */
  if (!(depth < t_hit && depth < _ProjectionParams.z - 0.001)) {
    return float3(0, 0, 0);
  }

  /* Otherwise, we have to compute the transmittance to that point. */
  float3 samplePoint = startPoint + depth * d;
  float r = length(samplePoint);
  float mu = dot(normalize(samplePoint), d);
  float2 coord2D = mapSky2DCoord(r, mu, _atmosphereRadius, _planetRadius,
    t_hit - depth, groundHit);
  float3 T_sampleOut = computeSkyTransmittance(coord2D);
  return outT / T_sampleOut;
}

SkyResult RenderSky(Varyings input, bool cubemap) {
  /* Compute origin point and sample direction. */
  float3 O = _WorldSpaceCameraPos1 - float3(0, -_planetRadius, 0);
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);

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
  if (!geoHit) {
    if (intersection.groundHit) {
      directLight = shadeGround(endPoint);
    } else {
      /* Shade the closest celestial body and the stars.
       * TODO: should make this return a "closeness to edge" parameter
       * that allows us to determine if we want to use MSAA to
       * anti-alias the edges. Or put this value as 4th in SS table. */
      directLight = shadeClosestCelestialBody(d);
    }
  }

  if (!intersection.groundHit && !intersection.atmoHit) {
    SkyResult result;
    result.color = float4(directLight, 1);
    result.transmittance = float4(1, 1, 1, 1);
    return result;
  }

  /* TODO: may want to precompute a bunch of texture coords here. */
  float r = length(startPoint);
  float mu = dot(normalize(startPoint), d);
  float2 coord2D = mapSky2DCoord(r, mu, _atmosphereRadius, _planetRadius,
    t_hit, intersection.groundHit);

  /* Compute transmittance. TODO: Sample against depth buffer if not
   * cubemap. Otherwise leave at 1. */
  float3 transmittance = computeSkyTransmittance(coord2D);

  /* Compute sky color. */
  float3 skyColor = computeSkyColor(coord2D, startPoint, d, t_hit,
    intersection.groundHit, geoHit);

  /* Attenuate it if we hit something in front of us. */
  float3 blendTransmittance = float3(0, 0, 0);
  if (geoHit) {
    float3 depthSamplePoint = startPoint + d * depth;
    float depthR = length(depthSamplePoint);
    float depthMu = dot(normalize(depthSamplePoint), d);
    float2 depthCoord2D = mapSky2DCoord(depthR, depthMu, _atmosphereRadius, _planetRadius,
      t_hit-depth, intersection.groundHit);
    float3 aerialPerspectiveTransmittance = computeSkyTransmittance(depthCoord2D);
    float3 attenuatedSkyColor = computeSkyColor(depthCoord2D, depthSamplePoint, d, t_hit-depth,
      intersection.groundHit, geoHit);
    blendTransmittance = transmittance/aerialPerspectiveTransmittance;
    skyColor -= blendTransmittance * attenuatedSkyColor;
    skyColor = max(0, skyColor);
  }

  /* Compute light pollution. */
  float3 lightPollution = computeLightPollutionColor(coord2D);

  /* Compute star scattering. */
  float3 starScattering = computeStarScatteringColor();

  /* Do depth buffer check to get blending value. */
  // if (!cubemap) {
  //   blendTransmittance = (geoHit) ? float3(1, 1, 1) : float3(0, 0, 0);//computeSkyBlendTransmittance(depth,
  //     //t_hit, intersection.groundHit, startPoint, d, transmittance);
  // }

  /* Final result. */
  SkyResult result;
  result.color = float4(directLight * transmittance + skyColor
    + lightPollution + starScattering, 1);
  result.transmittance = float4(blendTransmittance, 1);
  return result;
}

SkyResult SkyCubemap(Varyings input) : SV_Target {
  return RenderSky(input, true);
}

SkyResult SkyFullscreen(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
  return RenderSky(input, false);
}

float4 AerialPerspective(Varyings input) : SV_Target {
  return float4(0, 0, 0, 0);
}

/******************************************************************************/
/************************** END SKY FRAGMENT SHADER ***************************/
/******************************************************************************/



/******************************************************************************/
/*************************** CLOUDS FRAGMENT SHADER ***************************/
/******************************************************************************/

CloudResult RenderClouds(Varyings input, bool cubemap) {
  /* Final result. */
  SkyResult r;
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
  float3 skyCol = SAMPLE_TEXTURE2D_LOD(_fullscreenSkyColorRT,
    s_linear_clamp_sampler, textureCoordinate, 0).xyz;
  float3 skyT = SAMPLE_TEXTURE2D_LOD(_fullscreenSkyTransmittanceRT,
    s_linear_clamp_sampler, textureCoordinate, 0).xyz;
  float3 cloudCol = SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudColorRT,
    s_linear_clamp_sampler, textureCoordinate, 0).xyz;
  float4 cloudTAndBlend = SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudTransmittanceRT,
    s_linear_clamp_sampler, textureCoordinate, 0);
  float3 cloudT = cloudTAndBlend.xyz;
  float3 cloudBlend = cloudTAndBlend.w;

  /* Sample the color texture. */
  float3 geoCol = float3(0, 0, 0);
  if (!cubemap) {
    geoCol = LoadCameraColor(input.positionCS.xy);
  }

  /* Blend sky and geometry together so the fog affects the geometry. */
  float3 finalColor = (exposure * skyCol);// + skyT * geoCol;

  /* Blend them all together and return!
   * TODO: sky transmittance/cloud transmittance compositing. */
  // float3 finalColor = cloudT * skyCol
  //   + (1-cloudT) * (cloudBlend * skyCol + (1-cloudBlend) * cloudCol);

  return float4(finalColor, dot(skyT, skyT)/3.0);
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
    ZTest Always /* TODO: maybe turn off but turn on alpha blending for transmission? */
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
    ZTest Always /* TODO: maybe turn off but turn on alpha blending for transmission? */
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

  /* Aerial Perspective LUT. */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment AerialPerspective
    ENDHLSL
  }
}
Fallback Off
}
