#ifndef EXPANSE_CLOUD_COMMON_INCLUDED
#define EXPANSE_CLOUD_COMMON_INCLUDED

#include "../common/shaders/ExpanseSkyCommon.hlsl"
#include "../sky/ExpanseSkyMapping.hlsl"
#include "ExpanseCloudsGeometry.hlsl"

/******************************************************************************/
/***************************** GLOBAL VARIABLES *******************************/
/******************************************************************************/

CBUFFER_START(ExpanseCloud) // Expanse Cloud

/* General. */
int _numActiveCloudLayers;

/* Geometry. */
#define MAX_CLOUD_LAYERS 8
float _cloudGeometryType[MAX_CLOUD_LAYERS];
float _cloudGeometryXMin[MAX_CLOUD_LAYERS];
float _cloudGeometryXMax[MAX_CLOUD_LAYERS];
float _cloudGeometryYMin[MAX_CLOUD_LAYERS];
float _cloudGeometryYMax[MAX_CLOUD_LAYERS];
float _cloudGeometryZMin[MAX_CLOUD_LAYERS];
float _cloudGeometryZMax[MAX_CLOUD_LAYERS];
float _cloudGeometryHeight[MAX_CLOUD_LAYERS];

/* Noise. */
/* Coverage. */
int _cloudCoverageTile;
float _cloudCoverageIntensity;
/* Base. */
int _cloudBaseTile;
/* Structure. */
int _cloudStructureTile;
float _cloudStructureIntensity;
/* Detail. */
int _cloudDetailTile;
float _cloudDetailIntensity;
/* Base Warp. */
int _cloudBaseWarpTile;
float _cloudBaseWarpIntensity;
/* Detail Warp. */
int _cloudDetailWarpTile;
float _cloudDetailWarpIntensity;

#define CLOUD_BASE_WARP_MAX 0.25
#define CLOUD_DETAIL_WARP_MAX 0.1

/* TODO: probably rename these in UI, but "intensity" is a good name
 * internally. */

/* Lighting. */
/* 2D. */
float _cloudThickness[MAX_CLOUD_LAYERS];
/* 3D. */
/* 2D and 3D. */
float _cloudDensity[MAX_CLOUD_LAYERS];
float _cloudDensityAttenuationDistance[MAX_CLOUD_LAYERS];
float _cloudDensityAttenuationBias[MAX_CLOUD_LAYERS];
float4 _cloudAbsorptionCoefficients[MAX_CLOUD_LAYERS];
float4 _cloudScatteringCoefficients[MAX_CLOUD_LAYERS];
float _cloudMSAmount;
float _cloudMSBias;
float _cloudSilverSpread;
float _cloudSilverIntensity;
float _cloudAnisotropy;

/* Noise textures defining the cloud densities. */
TEXTURE2D(_cloudCoverageNoise); /* Coverage is always 2D. */
TEXTURE2D(_cloudBaseNoise2D);
TEXTURE2D(_cloudStructureNoise2D);
TEXTURE2D(_cloudDetailNoise2D);
TEXTURE2D(_cloudBaseWarpNoise2D);
TEXTURE2D(_cloudDetailWarpNoise2D);
TEXTURE3D(_cloudBaseNoise3D);
TEXTURE3D(_cloudStructureNoise3D);
TEXTURE3D(_cloudDetailNoise3D);
TEXTURE3D(_cloudBaseWarpNoise3D);
TEXTURE3D(_cloudDetailWarpNoise3D);

CBUFFER_END // Expanse Cloud

/******************************************************************************/
/*************************** END GLOBAL VARIABLES *****************************/
/******************************************************************************/

/* As a note---all cloud volume computations are done still done in
 * planet space. */

struct CloudShadingResult {
  float3 color;             /* Color result. */
  float3 transmittance;     /* Transmittance of the clouds. */
  float blend;              /* Transmittance to the clouds. */
  bool hit;                 /* Whether or not we intersected the clouds. */
  float t_hit;              /* Hit point to use in sorting. */
};

CloudShadingResult cloudNoIntersectionResult() {
  CloudShadingResult result;
  result.color = float3(0, 0, 0);
  result.transmittance = float3(1, 1, 1);
  result.blend = 1;
  result.hit = false;
  result.t_hit = -1;
  return result;
}

float henyeyGreensteinPhase(float dLd, float e) {
  return ((1 - e * e) / pow(abs(1 + e * e - 2 * e * dLd), 3.0/2.0)) / (4 * PI);
}

float cloudPhaseFunction(float dot_L_d, float cloudAnisotropy, float cloudSilverIntensity, float cloudSilverSpread) {
  return max(henyeyGreensteinPhase(dot_L_d, cloudAnisotropy), cloudSilverIntensity * henyeyGreensteinPhase(dot_L_d, 0.99 - cloudSilverSpread));
}

float3 computeMSModifiedTransmittance(float3 absorptionCoefficients, float opticalDepth) {
  return max(exp(-absorptionCoefficients * opticalDepth), exp(-absorptionCoefficients * opticalDepth * _cloudMSBias) * _cloudMSAmount);
}

float computeShadowBlur(float r, float mu, float offset, float distance) {
  float h = r - _planetRadius;
  float cos_h = -safeSqrt(h * (2 * _planetRadius + h)) / (_planetRadius + h);
  return 1 - pow(saturate(distance / clampAboveZero(abs(cos_h - mu))), offset);
}

float computeHeightGradient(float y, float2 bottom, float2 top) {
  return saturate(remap(y, bottom.x, bottom.y, 0, 1)) * saturate(remap(y, top.x, top.y, 1, 0));
}

float computeAtmosphericBlend(float3 O, float3 d, float3 samplePoint, float t_hit,
  SkyIntersectionData skyIntersection) {
  float2 oToSample = mapSky2DCoord(length(O), dot(normalize(O), d),
    _atmosphereRadius, _planetRadius, skyIntersection.endT,
    skyIntersection.groundHit, _resT.y);
  float2 sampleOut = mapSky2DCoord(length(samplePoint), dot(normalize(samplePoint), d),
    _atmosphereRadius, _planetRadius, skyIntersection.endT - t_hit,
    skyIntersection.groundHit, _resT.y);
  float3 t_oToSample = sampleSkyTTextureRaw(oToSample);
  float3 t_sampleOut = sampleSkyTTextureRaw(sampleOut);
  float3 blendTransmittanceColor = exp(t_oToSample - max(t_oToSample, t_sampleOut)
   + computeTransmittanceDensityAttenuation(O, d, t_hit));
  return dot(blendTransmittanceColor, float3(1, 1, 1) / 3.0);
}

/******************************************************************************/
/********************************* SAMPLING ***********************************/
/******************************************************************************/

/* Samples density textures at point p at specified mip level. */
float takeMediaSample2DLowLOD(float3 p, ICloudGeometry geometry, int mipLevel) {
  /* Warp. */
  float2 baseWarp = float2(0, 0);
  if (_cloudBaseWarpIntensity > FLT_EPSILON) {
    float2 baseWarpUV = geometry.mapCoordinate(p, _cloudBaseWarpTile).xz;
    baseWarp = _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX
      * SAMPLE_TEXTURE2D_LOD(_cloudBaseWarpNoise2D, s_linear_repeat_sampler,
      baseWarpUV, mipLevel).xy;
  }

  /* Base. */
  float2 baseUV = frac(geometry.mapCoordinate(p, _cloudBaseTile).xz - baseWarp);
  float sample = SAMPLE_TEXTURE2D_LOD(_cloudBaseNoise2D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;

  /* Coverage. */
  if (_cloudCoverageIntensity > FLT_EPSILON) {
    float2 coverageUV = geometry.mapCoordinate(p, _cloudCoverageTile).xz;
    float coverage = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
      coverageUV, mipLevel).x;
    coverage = clamp(_cloudCoverageIntensity * coverage * 5, 0.0, 0.99);
    // TODO: pow here? perhaps power remap? remap less for smaller values
    // Also, this is a much better remap function for wispier 2D clouds
    // than the usual remap(sample, coverage, 1, 0, 1)
    sample = powerRemap(sample, 0, 1, 0, 1-coverage, 1);
  }

  return sample;
}

float takeMediaSample2DLowLOD(float3 p, ICloudGeometry geometry) {
  return takeMediaSample2DLowLOD(p, geometry, 0);
}

/* Samples density textures at point p at specified mip level. Assumes p is in
 * bounds. */
float takeMediaSample2DHighLOD(float3 p, ICloudGeometry geometry, int mipLevel) {
  /* Base. */
  float sample = takeMediaSample2DLowLOD(p, geometry, mipLevel);

  /* Structure. */
  if (_cloudStructureIntensity > FLT_EPSILON) {
    float2 cloudStructureUV = geometry.mapCoordinate(p, _cloudStructureTile).xz;
    float structure = _cloudStructureIntensity
      * SAMPLE_TEXTURE2D_LOD(_cloudStructureNoise2D, s_linear_repeat_sampler,
      cloudStructureUV, mipLevel).x;
    sample = remap(sample, structure, 1, 0, 1);
  }

  /* Detail. */
  if (_cloudDetailIntensity > FLT_EPSILON) {
    float2 detailWarp = float2(0, 0);
    if (floatGT(_cloudDetailWarpIntensity, 0.0)) {
      float2 detailWarpUV = geometry.mapCoordinate(p, _cloudDetailWarpTile).xz;
      detailWarp = _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX
        * SAMPLE_TEXTURE2D_LOD(_cloudDetailWarpNoise2D, s_linear_repeat_sampler,
        detailWarpUV, mipLevel).xy;
    }

    float2 detailUV = frac(geometry.mapCoordinate(p, _cloudDetailTile).xz - detailWarp);
    float detail = _cloudDetailIntensity * SAMPLE_TEXTURE2D_LOD(_cloudDetailNoise2D, s_linear_repeat_sampler,
      detailUV, mipLevel).x;

    sample = remap(sample, detail, 1, 0, 1);
  }

  return sample;
}

float takeMediaSample2DHighLOD(float3 p, ICloudGeometry geometry) {
  return takeMediaSample2DHighLOD(p, geometry, 0);
}





/* Samples density textures at point p at specified mip level. */
float takeMediaSample3DLowLOD(float3 p, ICloudGeometry geometry, int mipLevel) {
  /* Warp. */
  float3 baseWarp = float3(0, 0, 0);
  if (_cloudBaseWarpIntensity > FLT_EPSILON) {
    float3 baseWarpUV = geometry.mapCoordinate(p, _cloudBaseWarpTile);
    baseWarp = _cloudBaseWarpIntensity * CLOUD_BASE_WARP_MAX
      * SAMPLE_TEXTURE3D_LOD(_cloudBaseWarpNoise3D, s_linear_repeat_sampler,
      baseWarpUV, mipLevel).xyz;
  }

  /* Base. */
  float3 baseUV = frac(geometry.mapCoordinate(p, _cloudBaseTile) - baseWarp);
  float sample = SAMPLE_TEXTURE3D_LOD(_cloudBaseNoise3D, s_linear_repeat_sampler,
    baseUV, mipLevel).x;

  /* Coverage. */
  if (_cloudCoverageIntensity > FLT_EPSILON) {
    float2 coverageUV = geometry.mapCoordinate(p, _cloudCoverageTile).xz;
    float coverage = SAMPLE_TEXTURE2D_LOD(_cloudCoverageNoise, s_linear_repeat_sampler,
      coverageUV, mipLevel).x;
    coverage = clamp(_cloudCoverageIntensity * coverage * 2, 0.0, 0.99);
    sample = remap(sample, coverage, 1, 0, 1);
  }

  /* TODO: height gradient params tweakable. */
  const float2 bottom = float2(0, 0.1);
  const float2 top = float2(0.5, 1);
  float heightGradient = geometry.heightGradient(p);
  heightGradient = computeHeightGradient(heightGradient, bottom, top);
  sample *= heightGradient;

  // TODO: why is it below zero?
  return max(0, sample);
}

float takeMediaSample3DLowLOD(float3 p, ICloudGeometry geometry) {
  return takeMediaSample3DLowLOD(p, geometry, 0);
}

/* Samples density textures at point p at specified mip level. Assumes p is in
 * bounds. */
float takeMediaSample3DHighLOD(float3 p, ICloudGeometry geometry, int mipLevel) {
  /* Base. */
  float sample = takeMediaSample3DLowLOD(p, geometry, mipLevel);

  /* Structure. */
  if (_cloudStructureIntensity > FLT_EPSILON) {
    float3 cloudStructureUV = geometry.mapCoordinate(p, _cloudStructureTile);
    float structure = _cloudStructureIntensity
      * SAMPLE_TEXTURE3D_LOD(_cloudStructureNoise3D, s_linear_repeat_sampler,
      cloudStructureUV, mipLevel).x;
    sample = remap(sample, structure, 1, 0, 1);
  }

  /* Detail. */
  if (_cloudDetailIntensity > FLT_EPSILON) {
    float3 detailWarp = float3(0, 0, 0);
    if (floatGT(_cloudDetailWarpIntensity, 0.0)) {
      float3 detailWarpUV = geometry.mapCoordinate(p, _cloudDetailWarpTile);
      detailWarp = _cloudDetailWarpIntensity * CLOUD_DETAIL_WARP_MAX
        * SAMPLE_TEXTURE3D_LOD(_cloudDetailWarpNoise3D, s_linear_repeat_sampler,
        detailWarpUV, mipLevel).xyz;
    }

    float3 detailUV = frac(geometry.mapCoordinate(p, _cloudDetailTile) - detailWarp);
    float detail = _cloudDetailIntensity * SAMPLE_TEXTURE3D_LOD(_cloudDetailNoise3D, s_linear_repeat_sampler,
      detailUV, mipLevel).x;

    sample = remap(sample, detail, 1, 0, 1);
  }

  // TODO: why is it below zero?
  return max(0, sample);
}

float takeMediaSample3DHighLOD(float3 p, ICloudGeometry geometry) {
  return takeMediaSample3DHighLOD(p, geometry, 0);
}

/******************************************************************************/
/******************************* END SAMPLING *********************************/
/******************************************************************************/

#endif // EXPANSE_CLOUD_COMMON_INCLUDED
