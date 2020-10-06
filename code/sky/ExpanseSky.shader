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
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/CookieSampling.hlsl"

/******************************************************************************/
/******************************** END INCLUDES ********************************/
/******************************************************************************/



/******************************************************************************/
/****************************** INPUT VARIABLES *******************************/
/******************************************************************************/

/* Planet parameters. */

// float _atmosphereThickness;
// float _planetRadius;
// TEXTURECUBE(_groundAlbedoTexture);
// float4 _groundTint;
// TEXTURECUBE(_groundEmissionTexture);
// float _groundEmissionMultiplier;
// float4x4 _planetRotation;

/* Atmosphere layers. */

// bool _layerEnabled;
// bool _layerCoefficients;
// int _layerDensityDistribution;
// float _layerHeight;
// float _layerThickness;
// float _layerPhaseFunction;
// float _layerAnisotropy;
// float _layerDensity;
// bool _layerUseDensityAttenuation;
// float _layerAttenuationDistance;
// float _layerAttenuationBias;
// float4 _layerTint;
// float _layerMultipleScatteringMultiplier;

/* Celestial Bodies. TODO */

#define MAX_BODIES 8

// need a num active bodies parameter
int _numActiveBodies;
bool _bodyEnabled[MAX_BODIES];
float3 _bodyDirection[MAX_BODIES];
float _bodyAngularRadius[MAX_BODIES];
float _bodyDistance[MAX_BODIES];
bool _bodyReceivesLight[MAX_BODIES];

/* TODO: texcube array...? commenting out for now*/
// _bodyAlbedoTexture0, bodyAlbedoTexture1, bodyAlbedoTexture2,
//   bodyAlbedoTexture3, bodyAlbedoTexture4, bodyAlbedoTexture5, bodyAlbedoTexture6, bodyAlbedoTexture7;
/* Displayed on null check of body albedo texture. */

float4x4 _bodyAlbedoTextureRotation[MAX_BODIES];
float4 _bodyAlbedoTint[MAX_BODIES];
bool _bodyEmissive[MAX_BODIES];

/* TODO: condense to precalculated color. */
float4 _bodyLightColor[MAX_BODIES];
float _bodyLimbDarkening[MAX_BODIES];

/* TODO: texcube array...? commenting out for now. */
// _bodyEmissionTexture0, bodyEmissionTexture1, bodyEmissionTexture2,
//   bodyEmissionTexture3, bodyEmissionTexture4, bodyEmissionTexture5, bodyEmissionTexture6, bodyEmissionTexture7;
/* Displayed on null check of body albedo texture. */

float4x4 _bodyEmissionTextureRotation[MAX_BODIES];

/* TODO: could condense to one float4 multiplied together. */
float4 _bodyEmissionTint[MAX_BODIES];

/* Night Sky. TODO */

/* Quality. */

// needed for resolution offsets
// _skyTextureQuality;

// _numberOfTransmittanceSamples;

// _numberOfLightPollutionSamples;

// _numberOfSingleScatteringSamples;

// _numberOfGroundIrradianceSamples;

// _numberOfMultipleScatteringSamples;

// _numberOfMultipleScatteringAccumulationSamples;

// _useImportanceSampling;

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
TEXTURE2D(_depthBuffer);

/* Sampler for tables. */
#ifndef UNITY_SHADER_VARIABLES_INCLUDED
    SAMPLER(s_linear_clamp_sampler);
    SAMPLER(s_trilinear_clamp_sampler);
#endif

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
  UNITY_VERTEX_OUTPUT_STEREO
};

struct SkyResult {
  float4 color : SV_Target0;
  float4 transmittance : SV_Target0;
};

struct CloudResult {
  float4 color : SV_Target0;
  float4 transmittance : SV_Target0;
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
    return output;
}

/******************************************************************************/
/***************************** END VERTEX SHADER ******************************/
/******************************************************************************/



/******************************************************************************/
/**************************** SKY FRAGMENT SHADER *****************************/
/******************************************************************************/

SkyResult RenderSky(Varyings input, bool cubemap) {
  /* Final result. */
  SkyResult r;
  r.color = float4(0, 0, 0.5, 1);
  r.transmittance = float4(0, 0.0, 0.5, 1);
  return r;
}

SkyResult SkyCubemap(Varyings input) : SV_Target {
  return RenderSky(input, true);
}

SkyResult SkyFullscreen(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
  return RenderSky(input, false);
}

/******************************************************************************/
/************************** END SKY FRAGMENT SHADER ***************************/
/******************************************************************************/



/******************************************************************************/
/*************************** CLOUDS FRAGMENT SHADER ***************************/
/******************************************************************************/

SkyResult RenderClouds(Varyings input, bool cubemap) {
  /* Final result. */
  SkyResult r;
  r.color = float4(0, 0.3, 0.0, 1);
  r.transmittance = float4(0, 0.3, 0.0, 1);
  return r;
}

SkyResult CloudsCubemap(Varyings input) : SV_Target {
  return RenderSky(input, true);
}

SkyResult CloudsFullscreen(Varyings input) : SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
  return RenderSky(input, false);
}

/******************************************************************************/
/************************* END CLOUDS FRAGMENT SHADER *************************/
/******************************************************************************/



/******************************************************************************/
/************************ COMPOSITING FRAGMENT SHADER *************************/
/******************************************************************************/

float4 Composite(Varyings input, bool cubemap, float exposure) {
  float4 skyColor = float4(0.5, 0.0, 0.0, 1.0);
  if (_bodyEnabled[0]) {
    skyColor = _bodyLightColor[0];
  }
  // float2 textureCoordinate = float2(0.21,0.21);
  // skyColor += SAMPLE_TEXTURE2D_LOD(_fullscreenSkyColorRT,
  //   s_linear_clamp_sampler, textureCoordinate, 0);
  // skyColor += SAMPLE_TEXTURE2D_LOD(_currFullscreenCloudColorRT,
  //   s_linear_clamp_sampler, textureCoordinate, 0);
  return skyColor;
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
    ZTest LEqual /* TODO: maybe turn off but turn on alpha blending for transmission? */
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
    ZTest LEqual /* TODO: maybe turn off but turn on alpha blending for transmission? */
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
    ZTest LEqual
    Blend Off
    Cull Off

    HLSLPROGRAM
        #pragma fragment CompositeFullscreen
    ENDHLSL
  }
}
Fallback Off
}
