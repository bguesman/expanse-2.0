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

#include "ExpanseSkyCommon.hlsl"

/******************************************************************************/
/******************************** END INCLUDES ********************************/
/******************************************************************************/



/******************************************************************************/
/****************************** INPUT VARIABLES *******************************/
/******************************************************************************/

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
TEXTURE2D(_depthBuffer);

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
  if (_numActiveBodies > 0) {
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
