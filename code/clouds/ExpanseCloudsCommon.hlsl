#ifndef EXPANSE_CLOUD_COMMON_INCLUDED
#define EXPANSE_CLOUD_COMMON_INCLUDED

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

/* Noise textures defining the cloud densities. */
TEXTURE2D(_cloudCoverageNoise); /* Coverage is always 2D. */
TEXTURE2D(_cloudBaseNoise2D);
TEXTURE2D(_cloudStructureNoise2D);
TEXTURE2D(_cloudDetailNoise2D);
TEXTURE2D(_cloudBaseWarpNoise2D);
TEXTURE2D(_cloudDetailWarpNoise2D);

CBUFFER_END // Expanse Cloud

/******************************************************************************/
/*************************** END GLOBAL VARIABLES *****************************/
/******************************************************************************/

#endif // EXPANSE_CLOUD_COMMON_INCLUDED
