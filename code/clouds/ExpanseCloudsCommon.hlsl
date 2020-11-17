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
float _cloudMSAmount;// = 0.75;
float _cloudMSBias;// = 0.25;
float _cloudSilverSpread;// = 0.5;
float _cloudSilverIntensity;// = 1;
float _cloudAnisotropy;// = 0.3;

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

#endif // EXPANSE_CLOUD_COMMON_INCLUDED
