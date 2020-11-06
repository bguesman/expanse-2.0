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

/******************************************************************************/
/******************************** END INCLUDES ********************************/
/******************************************************************************/



/******************************************************************************/
/****************************** INPUT VARIABLES *******************************/
/******************************************************************************/

/* Celestial bodies. */
float _bodyAngularRadius[MAX_BODIES];
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
float4x4 _nightSkyRotation;
bool _useTwinkle;
float _twinkleThreshold;
float _twinkleFrequencyMin;
float _twinkleFrequencyMax;
float _twinkleBias;
float _twinkleSmoothAmplitude;
float _twinkleChaoticAmplitude;
float4 _starTint;

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

float4 RenderSky(Varyings input, float3 O, float3 d, bool cubemap) {
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
  float3 directLight = float3(0, 0, 0);
  if (!geoHit && !cubemap) {
    if (intersection.groundHit) {
      directLight = shadeGround(endPoint);
    } else {
      /* Shade the closest celestial body and the stars. */
      directLight = shadeClosestCelestialBody(d);
      if (directLight.x < 0) {
        /* If we didn't shade any celestial bodies, shade the stars. */
        directLight = shadeNightSky(d);
      }
    }
  }

  /* If we didn't hit the ground or the atmosphere, return just direct
   * light. */
  if (!intersection.groundHit && !intersection.atmoHit) {
    if (depth < farClip - 0.001) {
      return float4(0, 0, 0, 1);
    } else {
      return float4(directLight, 0);
    }
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
  }

  return float4(skyColor + transmittance * directLight, blendTransmittance);
}

float4 SkyCubemap(Varyings input) : SV_Target {
  /* Compute origin point and sample direction. */
  float3 O = GetCameraPositionPlanetSpace();
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);
  return RenderSky(input, O, d, true);
}

float4 SkyFullscreen(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

  /* Compute origin point and sample direction. */
  float3 O = GetCameraPositionPlanetSpace();
  float3 d = -GetSkyViewDirWS(input.positionCS.xy);

  /* If we aren't using anti-aliasing, just render. */
  if (!_useAntiAliasing) {
      return RenderSky(input, O, d, false);
  }

  /* Otherwise, see how close we are to the planet or celestial body's edge,
   * and if we are close, then use MSAA 8x. TODO: do this. */
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
