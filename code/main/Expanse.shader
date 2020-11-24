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
#include "../sky/ExpanseSky.hlsl"

#include "../clouds/ExpanseClouds.hlsl"

/******************************************************************************/
/******************************** END INCLUDES ********************************/
/******************************************************************************/



/******************************************************************************/
/****************************** INPUT VARIABLES *******************************/
/******************************************************************************/

/* Celestial bodies. */
float _bodyDistance[MAX_BODIES];
bool _bodyReceivesLight[MAX_BODIES];
float4x4 _bodyAlbedoTextureRotation[MAX_BODIES];
float4 _bodyAlbedoTint[MAX_BODIES];
bool _bodyEmissive[MAX_BODIES];
float _bodyLimbDarkening[MAX_BODIES];
float4x4 _bodyEmissionTextureRotation[MAX_BODIES];
float4 _bodyEmissionTint[MAX_BODIES];
// Textures can't be array, since they could be different resolutions,
// so declare them individually.
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
bool _hasNightSkyTexture;
TEXTURECUBE(_nightSkyTexture);
float4 _nightSkyTint;         /* Tint and intensity. */
float _nightSkyAmbientMultiplier;
float4x4 _nightSkyRotation;
bool _useTwinkle;
float _twinkleThreshold;
float _twinkleFrequencyMin;
float _twinkleFrequencyMax;
float _twinkleBias;
float _twinkleSmoothAmplitude;
float _twinkleChaoticAmplitude;
float4 _starTint;

/* Clouds. */
int _cloudLayerToDraw;
TEXTURE2D(_cloudColorLayer0);
TEXTURE2D(_cloudColorLayer1);
TEXTURE2D(_cloudColorLayer2);
TEXTURE2D(_cloudColorLayer3);
TEXTURE2D(_cloudColorLayer4);
TEXTURE2D(_cloudColorLayer5);
TEXTURE2D(_cloudColorLayer6);
TEXTURE2D(_cloudColorLayer7);
TEXTURE2D(_cloudTransmittanceLayer0);
TEXTURE2D(_cloudTransmittanceLayer1);
TEXTURE2D(_cloudTransmittanceLayer2);
TEXTURE2D(_cloudTransmittanceLayer3);
TEXTURE2D(_cloudTransmittanceLayer4);
TEXTURE2D(_cloudTransmittanceLayer5);
TEXTURE2D(_cloudTransmittanceLayer6);
TEXTURE2D(_cloudTransmittanceLayer7);

/* Render textures. */
TEXTURE2D(_fullscreenSkyColorRT);
TEXTURE2D(_cubemapSkyColorRT);
TEXTURE2D(_fullscreenSkyDirectLightRT);
TEXTURE2D(_cubemapSkyDirectLightRT);
TEXTURE2D(_lastFullscreenCloudColorRT);
TEXTURE2D(_lastFullscreenCloudTransmittanceRT);
TEXTURE2D(_lastCubemapCloudColorRT);
TEXTURE2D(_lastCubemapCloudTransmittanceRT);
TEXTURE2D(_currFullscreenCloudColorRT);
TEXTURE2D(_currFullscreenCloudTransmittanceRT);
TEXTURE2D(_currCubemapCloudColorRT);
TEXTURE2D(_currCubemapCloudTransmittanceRT);

/* For reprojection. */
float4x4 _previousPCoordToViewDirMatrix;
float4x4 _inversePCoordToViewDirMatrix;

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
  float4 directLight : SV_Target1;
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

float3 sampleBodyEmissionTexture(float3 uv, int i, float angularRadius) {
  // HACK: not actually implementing proper mip selection, just this hack.
  float angularRadiusDegrees = (angularRadius / PI) * 180;
  int sampleLevel = (int) (pow(max(0, 18 - angularRadiusDegrees) / 18, 1.5) * 7);
  switch(i) {
    case 0:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture0, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 1:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture1, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 2:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture2, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 3:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture3, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 4:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture4, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 5:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture5, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 6:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture6, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 7:
      return SAMPLE_TEXTURECUBE_LOD(_bodyEmissionTexture7, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    default:
      return float3(0, 0, 0);
  }
}

float3 sampleBodyAlbedoTexture(float3 uv, int i, float angularRadius) {
  // HACK: not actually implementing proper mip selection, just this hack.
  float angularRadiusDegrees = (angularRadius / PI) * 180;
  int sampleLevel = (int) (pow(max(0, 18 - angularRadiusDegrees) / 18, 1.5) * 7);
  switch(i) {
    case 0:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture0, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 1:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture1, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 2:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture2, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 3:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture3, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 4:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture4, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 5:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture5, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 6:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture6, s_linear_clamp_sampler, uv, sampleLevel).xyz;
    case 7:
      return SAMPLE_TEXTURECUBE_LOD(_bodyAlbedoTexture7, s_linear_clamp_sampler, uv, sampleLevel).xyz;
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

float3 shadeGround(float3 endPoint) {
  float3 color = float3(0, 0, 0);
  float3 endPointNormalized = normalize(endPoint);

  /* Compute albedo, emission. */
  float3 albedo = _groundTint.xyz;
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
    float3 lightColor = _bodyLightColor[i].xyz;
    float3 cosTheta = dot(endPointNormalized, L);

    color += albedo * cosTheta * lightColor + emission;
  }
  return color;
}

/* Given direction to sample in, shades closest celestial body. Returns
 * negative number if nothing is hit. W coordinate specifies if the
 * direction was close to the edge of the body or not, for conditional MSAA.
 * If not close to edge, it's negative. If close to edge, it's positive. */
float4 shadeClosestCelestialBody(float3 d) {
  bool closeToEdge = false;
  const float closeToEdgeThreshold = 0.5;

  /* First, compute closest intersection. */
  int minIdx = -1;
  float minDist = FLT_MAX;
  for (int i = 0; i < _numActiveBodies; i++) {
    float3 L = _bodyDirection[i];
    float cosTheta = dot(L, d);
    float cosAngularRadius = cos(_bodyAngularRadius[i]);
    if (cosTheta > cosAngularRadius && _bodyDistance[i] < minDist) {
      minIdx = i;
      minDist = _bodyDistance[i];
    }
    closeToEdge = abs(cosTheta - cosAngularRadius) < (closeToEdgeThreshold * (_bodyAngularRadius[i]/90)) || closeToEdge;
  }

  /* If we didn't hit anything, there's nothing to shade. Return negative
   * number to indicate that we hit nothing. */
  if (minIdx < 0) {
    return float4(-1, -1, -1, closeToEdge ? 1 : -1);
  }


  /* And compute lighting. */
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
        mul(surfaceNormal, (float3x3) _bodyAlbedoTextureRotation[minIdx]), minIdx, _bodyAngularRadius[minIdx]);
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
        mul(surfaceNormal, (float3x3) _bodyEmissionTextureRotation[minIdx]), minIdx, _bodyAngularRadius[minIdx]);
      emission *= emissionTex;
    } else {
      emission *= _bodyLightColor[minIdx].xyz;
    }
    emission *= limbDarkening(dot(L, d), cosInner, _bodyLimbDarkening[minIdx]);
    emission *= computeCelestialBodyLuminanceMultiplier(cosInner);
    directLighting += emission;
  }

  return float4(directLighting, closeToEdge ? 1 : -1);
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
    float directionSeed = random_3_1(textureCoordinate);
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
      nebulaeColor = pow(abs(nebulaeColorAndAlpha.xyz), _nebulaOverallDefinition) * _nebulaOverallIntensity;
      float nebulaeAlpha = nebulaeColorAndAlpha.w;
      nebulaeColor *= nebulaeAlpha;
      // Blend probabilistically.
      float starNebulaeBlendAmount = random_1_1(starSeed * 3.92853);
      starColor *= 1-(starNebulaeBlendAmount * nebulaeAlpha);
    } else {
      if (_hasNebulaeTexture) {
        nebulaeColor = _nebulaOverallIntensity * SAMPLE_TEXTURECUBE_LOD(_nebulaeTexture,
          s_linear_clamp_sampler, textureCoordinate, 0).xyz;
      }
    }
    return ((twinkleCoarse + twinkleFine) * starColor * _starTint.xyz + nebulaeColor) * _nightSkyTint.xyz;
  } else {
    if (!_hasNightSkyTexture) {
      return _nightSkyTint.xyz;
    }
    float3 starColor = SAMPLE_TEXTURECUBE_LOD(_nightSkyTexture,
      s_linear_clamp_sampler, textureCoordinate, 0).xyz;
    float directionSeed = random_3_1(textureCoordinate);
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

struct SkyRenderResult {
  float4 color;
  float3 directLight;
  bool closeToEdge;
};

SkyRenderResult RenderSky(Varyings input, float3 O, float3 d, bool cubemap) {
  /* Final result. */
  SkyRenderResult result;
  result.closeToEdge = false;

  /* Trace a ray to see what we hit. */
  SkyIntersectionData intersection = traceSkyVolume(O, d, _planetRadius,
    _atmosphereRadius);

  /* Get start and end points of march. */
  float3 startPoint = O + d * intersection.startT;
  float3 endPoint = O + d * intersection.endT;
  float t_hit = intersection.endT - intersection.startT;

  /* Get the depth and see if we hit any geometry. */
  float linearDepth = Linear01Depth(LoadCameraDepth(input.positionCS.xy),
    _ZBufferParams) * _ProjectionParams.z;
  /* Make sure depth is distance to view aligned plane. */
  float3 cameraCenterD = -GetSkyViewDirWS(float2(_ScreenParams.x/2, _ScreenParams.y/2));
  float cosTheta = dot(cameraCenterD, d);
  float depth = linearDepth / max(cosTheta, 0.00001);
  float farClip = _ProjectionParams.z / max(cosTheta, 0.00001);
  bool geoHit = depth < t_hit && depth < farClip - 0.001;

  /* Compute direct illumination, but only if we don't hit any geometry and
   * if we're rendering fullscreen. */
  result.directLight = float3(0, 0, 0);
  if (!geoHit && !cubemap) {
    if (intersection.groundHit) {
      result.directLight = shadeGround(endPoint);
    } else {
      /* Shade the closest celestial body and the stars. */
      float4 directLightAndEdgeCloseness = shadeClosestCelestialBody(d);
      result.directLight = directLightAndEdgeCloseness.xyz;
      result.closeToEdge = (directLightAndEdgeCloseness.w > 0);
      if (result.directLight.x < 0) {
        /* If we didn't shade any celestial bodies, shade the stars. */
        result.directLight = shadeNightSky(d);
      }
    }
  }

  /* If we didn't hit the ground or the atmosphere, return just direct
   * light. */
  if (!intersection.groundHit && !intersection.atmoHit) {
    if (depth < farClip - 0.001) {
      result.color = float4(0, 0, 0, 1);
    } else {
      result.color = float4(0, 0, 0, 0);
    }
    return result;
  }

  /* Compute 2D texture coordinate. */
  float r = length(startPoint);
  float mu = dot(normalize(startPoint), d);
  float2 coord2D = mapSky2DCoord(r, mu, _atmosphereRadius, _planetRadius,
    t_hit, intersection.groundHit, _resT.y);

  /* Compute transmittance. */
  float3 transmittance = sampleSkyTTextureRaw(coord2D);
  transmittance += computeTransmittanceDensityAttenuation(startPoint, d, t_hit);
  transmittance = exp(transmittance);

  float3 skyColor = float3(0, 0, 0);
  float blendTransmittance = 0;
  if (geoHit) {
    /* Sample aerial perspective. */
    float3 apUV = mapFrustumCoordinate(input.positionCS.xy, linearDepth);
    float4 colorAndTransmittance = sampleSkyAPTexture(apUV);
    skyColor = colorAndTransmittance.xyz;
    blendTransmittance = saturate(exp(colorAndTransmittance.w));
  } else {
    /* Sample rendered sky tables. */
    /* Single scattering. */
    float theta = d_to_theta(d, O);
    float2 skyRenderCoordSS = mapSkyRenderCoordinate(r, mu, theta, _atmosphereRadius,
      _planetRadius, t_hit, intersection.groundHit, _resSS.x, _resSS.y);
    float3 ss = sampleSkySSTexture(skyRenderCoordSS);
    /* Multiple scattering. */
    float2 skyRenderCoordMS = mapSkyRenderCoordinate(r, mu, theta, _atmosphereRadius,
      _planetRadius, t_hit, intersection.groundHit, _resMSAcc.x, _resMSAcc.y);
    float3 ms = sampleSkyMSAccTexture(skyRenderCoordMS);

    skyColor = ss + ms;

    if (cubemap) {
      /* Add an estimate of the night sky color for ambient lighting. */
      skyColor += _nightSkyTint.xyz * _nightSkyAmbientMultiplier;
    }

    /* Figure out how close we are to the horizon for conditional MSAA. */
    float h = r - _planetRadius;
    float cos_h = -safeSqrt(h * (2 * _planetRadius + h)) / (_planetRadius + h);
    const float closeToHorizonEdgeThreshold = 0.005;
    result.closeToEdge = result.closeToEdge || (abs(cos_h - mu) < closeToHorizonEdgeThreshold);
  }

  result.directLight *= transmittance;
  result.color = float4(skyColor, blendTransmittance);
  return result;
}

SkyResult SkyCubemap(Varyings input) {
  /* Compute origin point and sample direction. */
  float3 O = GetCameraPositionPlanetSpace();
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);
  SkyRenderResult result = RenderSky(input, O, d, true);
  SkyResult r;
  r.color = result.color;
  r.directLight = float4(result.directLight, 0);
  return r;
}

SkyResult SkyFullscreen(Varyings input) {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

  /* Compute origin point and sample direction. */
  float3 O = GetCameraPositionPlanetSpace();
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);

  /* Render. */
  SkyRenderResult result = RenderSky(input, O, d, false);

  /* Use AA if we are close enough to an edge */
  if (_useAntiAliasing && result.closeToEdge) {
    float MSAA_8X_OFFSETS_X[8] = {1.0/16.0, -1.0/16.0, 5.0/16.0, -3.0/16.0, -5.0/16.0, -7.0/16.0, 3.0/16.0, 7.0/16.0};
    float MSAA_8X_OFFSETS_Y[8] =  {-3.0/16.0, 3.0/16.0, 1.0/16.0, -5.0/16.0, 5.0/16.0, -1.0/16.0, 7.0/16.0, -7.0/16.0};
    for (int i = 0; i < 8; i++) {
      float3 dOffset = -GetSkyViewDirWS(input.positionCS.xy
        + float2(MSAA_8X_OFFSETS_X[i], MSAA_8X_OFFSETS_Y[i]));
      SkyRenderResult sample = RenderSky(input, O, dOffset, false);
      result.color += sample.color;
      result.directLight += sample.directLight;
    }
    result.color /= 9.0;
    result.directLight /= 9.0;
  }

  SkyResult r;
  r.color = result.color;
  r.directLight = float4(result.directLight, 0);
  return r;
}

/******************************************************************************/
/************************** END SKY FRAGMENT SHADER ***************************/
/******************************************************************************/



/******************************************************************************/
/*************************** CLOUDS FRAGMENT SHADER ***************************/
/******************************************************************************/

CloudResult RenderClouds(Varyings input, float3 O, float3 d, bool cubemap) {
  /* Get the depth and see if we hit any geometry. */
  float linearDepth = Linear01Depth(LoadCameraDepth(input.positionCS.xy),
    _ZBufferParams) * _ProjectionParams.z;
  /* Make sure depth is distance to view aligned plane. */
  float3 cameraCenterD = -GetSkyViewDirWS(float2(_ScreenParams.x/2, _ScreenParams.y/2));
  float cosTheta = dot(cameraCenterD, d);
  float depth = linearDepth / max(cosTheta, 0.00001);
  float farClip = _ProjectionParams.z / max(cosTheta, 0.00001);
  bool geoHit = depth < farClip - 0.001;

  /* Shade the clouds. */
  CloudShadingResult result = shadeCloudLayer(O, d, _cloudLayerToDraw, depth,
    geoHit);

  /* This should already be set, but just to be sure. */
  if (!result.hit) {
    result.t_hit = -1;
  }

  /* Final result. */
  CloudResult r;
  r.color = float4(result.color, result.blend);
  r.transmittance = float4(result.transmittance, result.t_hit);
  return r;
}

CloudResult CloudsCubemap(Varyings input) {
  float3 O = GetCameraPositionPlanetSpace();
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);
  return RenderClouds(input, O, d, true);
}

CloudResult CloudsFullscreen(Varyings input) {
  float3 O = GetCameraPositionPlanetSpace();
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
  return RenderClouds(input, O, d, false);
}

/******************************************************************************/
/************************* END CLOUDS FRAGMENT SHADER *************************/
/******************************************************************************/



/******************************************************************************/
/********************** CLOUD COMPOSITING FRAGMENT SHADER *********************/
/******************************************************************************/

CloudShadingResult sampleCloudLayerTexture(float2 uv, int i) {
  float4 colorAndBlend = float4(0, 0, 0, 0);
  float4 transmittanceAndHit = float4(0, 0, 0, 0);
  switch(i) {
    case 0: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer0,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer0,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 1: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer1,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer1,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 2: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer2,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer2,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 3: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer3,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer3,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 4: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer4,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer4,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 5: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer5,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer5,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 6: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer6,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer6,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    case 7: {
      colorAndBlend = SAMPLE_TEXTURE2D_LOD(_cloudColorLayer7,
        s_linear_clamp_sampler, uv, 0);
      transmittanceAndHit = SAMPLE_TEXTURE2D_LOD(_cloudTransmittanceLayer7,
        s_linear_clamp_sampler, uv, 0);
      break;
    }
    default: {
      colorAndBlend = float4(0, 0, 0, 0);
      transmittanceAndHit = float4(0, 0, 0, 0);
      break;
    }
  }

  CloudShadingResult result;
  result.color = colorAndBlend.xyz;
  result.transmittance = transmittanceAndHit.xyz;
  result.blend = colorAndBlend.w;
  result.t_hit = transmittanceAndHit.w;
  result.hit = result.t_hit < 0;
  return result;
}

CloudResult compositeClouds(float2 uv, float4 positionCS) {
  /* Loop counters. */
  int i, j;

  /* Shade every layer. */
  CloudShadingResult layerResult[MAX_CLOUD_LAYERS];
  for (i = 0; i < _numActiveCloudLayers; i++) {
    layerResult[i] = sampleCloudLayerTexture(uv, i);
  }

  /* Count how many didn't hit. */
  int numNoHit = 0;
  for (i = 0; i < _numActiveCloudLayers; i++) {
    if (layerResult[i].t_hit < 0) {
      numNoHit++;
    }
  }

  /* Sort the results by their hit points using bubble sort, which is fast
   * since we only have at most 8 results to sort. All the no hit layers
   * will end up at the front, and the rest will be sorted properly. */
  for (i = 0; i < _numActiveCloudLayers-1; i++) {
    for (j = 0; j < _numActiveCloudLayers - i - 1; j++) {
      if (layerResult[j].t_hit < layerResult[j+1].t_hit) {
        CloudShadingResult temp = layerResult[j+1];
        layerResult[j+1] = layerResult[j];
        layerResult[j] = temp;
      }
    }
  }

  /* TODO: this strategy of blend causes artifacts when transitioning
   * from intersecting two layers to intersecting one layer. Probably
   * to do with the fact that monochrome transmittance starts from 2.
   * for now, getting rid of it. recompile. */

  /* Now, composite the results, alpha-blending in order, ensuring to start at
   * numNoHit so we skip the layers where there was no intersection. */
  CloudShadingResult result;
  result.color = float3(0, 0, 0);
  result.transmittance = float3(1, 1, 1);
  result.blend = 0;
  result.t_hit = 0;
  float blendNormalization = 0.0;
  for (i = 0; i < _numActiveCloudLayers-numNoHit; i++) {
    CloudShadingResult layer = layerResult[i];
    /* HACK: why is this necessary? */
    layer.transmittance = min(1, layer.transmittance);
    result.color = result.color * layer.transmittance + layer.color * (1-layer.transmittance);
    result.transmittance *= layer.transmittance;
    float monochromeAlpha = saturate(dot(1-layer.transmittance, float3(1, 1, 1)/3));
    float monochromeTransmittance = averageFloat3(result.transmittance);
    result.blend += layer.blend * monochromeAlpha / max(0.001, monochromeTransmittance);
    result.t_hit += layer.t_hit * monochromeAlpha / max(0.001, monochromeTransmittance);
    blendNormalization += monochromeAlpha / max(0.001, monochromeTransmittance);
  }
  if (blendNormalization > 0) {
    result.blend /= blendNormalization;
  }

  /* Finally, reproject and blend with previous frame. */
  /* The question is, where was our previous sample in world space? We can
   * figure that out by taking our clip space position and multiplying it
   * by the previous camera transformation. */
  // float4 previousWorldspaceDirection = mul(_previousPCoordToViewDirMatrix, positionCS);
  // /* Then we can multiply it by the current viewing transformation. */
  // float4 currClipspacePosition = mul(_inversePCoordToViewDirMatrix, previousWorldspaceDirection);
  // /* Since we're in clip space, we have to divide by the screen width and
  //  * height to get to normalized device coordinates in the range [0, 1]. */
  // float2 reprojectedUV = saturate(currClipspacePosition.xy / _ScreenParams.xy);
  float2 reprojectedUV = uv;

  // CloudResult resultPacked;
  // resultPacked.color = float4(0, 0, 0, 1);
  // resultPacked.transmittance = float4(0, 0, 0, result.t_hit);
  // if (reprojectedUV.x < 0.5) {
  //   resultPacked.color += float4(100, 0, 0, 0);
  // }
  // if (reprojectedUV.y < 0.5) {
  //   resultPacked.color += float4(0, 0, 100, 0);
  // }
  // return resultPacked;

  float newProportion = 1.0/((float) CLOUD_REPROJECTION_FRAMES);
  float reuseProportion = 1-newProportion;
  float4 cloudColAndBlendPrev = SAMPLE_TEXTURE2D_LOD(_lastFullscreenCloudColorRT,
    s_linear_clamp_sampler, reprojectedUV, 0);
  float3 cloudColPrev = cloudColAndBlendPrev.xyz;
  float cloudBlendPrev = cloudColAndBlendPrev.w;
  float3 cloudTPrev = SAMPLE_TEXTURE2D_LOD(_lastFullscreenCloudTransmittanceRT,
    s_linear_clamp_sampler, reprojectedUV, 0).xyz;
  result.color = result.color * newProportion + cloudColPrev * reuseProportion;
  result.transmittance = result.transmittance * newProportion + cloudTPrev * reuseProportion;
  result.blend = result.blend * newProportion + cloudBlendPrev * reuseProportion;

  CloudResult resultPacked;
  resultPacked.color = float4(result.color, result.blend);
  resultPacked.transmittance = float4(result.transmittance, result.t_hit);

  return resultPacked;
}

CloudResult CloudsCompositeCubemap(Varyings input) {
  CloudResult r;
  r.color = float4(0, 0, 0, 0);
  r.transmittance = float4(0, 0, 0, 0);
  return r;
}

CloudResult CloudsCompositeFullscreen(Varyings input) {
  float2 textureCoordinate = input.screenPosition;
  return compositeClouds(textureCoordinate, input.positionCS);
}

/******************************************************************************/
/******************** END CLOUD COMPOSITING FRAGMENT SHADER *******************/
/******************************************************************************/



/******************************************************************************/
/************************ COMPOSITING FRAGMENT SHADER *************************/
/******************************************************************************/

float4 Composite(Varyings input, bool cubemap, float exposure) {
  /* Get screenspace texture coordinate. */
  float2 textureCoordinate = input.screenPosition;

  /* Sample the sky fullscreen texture. */
  float4 skyCol = SAMPLE_TEXTURE2D_LOD(_fullscreenSkyColorRT,
    s_linear_clamp_sampler, textureCoordinate, 0);
  float3 skyDirectLight = SAMPLE_TEXTURE2D_LOD(_fullscreenSkyDirectLightRT,
    s_linear_clamp_sampler, textureCoordinate, 0).xyz;

  /* Sample the cloud fullscreen textures. */
  float4 cloudColAndBlend = SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudColorRT,
    s_linear_clamp_sampler, textureCoordinate, 0);
  float3 cloudCol = cloudColAndBlend.xyz;
  float cloudBlend = cloudColAndBlend.w;
  float3 cloudT = SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudTransmittanceRT,
    s_linear_clamp_sampler, textureCoordinate, 0).xyz;

  /* TODO: blend clouds properly. */
  /* First, composite the clouds on top of the sky according to the cloud
   * transmittance. */
  float3 cloudsOnSky = cloudCol + skyCol.xyz * cloudT;
  /* Then, composite the sky color on top of the clouds according to the
   * blend transmittance to fake aerial perspective. */
  float3 finalColor = cloudsOnSky * cloudBlend + skyCol.xyz * (1 - cloudBlend) + skyDirectLight * cloudT;

  /* Optionally, dither. */
  if (_useDither) {
    finalColor *= 1 + (1.0/32.0) * random_3_1(input.positionCS.xyz);
  }

  /* Finally, multiply by exposure. */
  if (!cubemap) {
    finalColor *= exposure;
  }

  /* TODO: will have to account for cloud transmittance here. Probably
   * use skyCol.w * dot(cloudT, float3(1,1,1)/3). */
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

  /* Clouds compositing cubemap */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment CloudsCompositeCubemap
    ENDHLSL
  }

  /* Clouds compositing fullscreen */
  Pass
  {
    ZWrite Off
    ZTest Always
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment CloudsCompositeFullscreen
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
