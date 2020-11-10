#ifndef EXPANSE_CLOUD_COMMON_INCLUDED
#define EXPANSE_CLOUD_COMMON_INCLUDED

/******************************************************************************/
/***************************** GLOBAL VARIABLES *******************************/
/******************************************************************************/

CBUFFER_START(ExpanseCloud) // Expanse Cloud

/* General. */
int _numActiveCloudLayers;

/* Geometry. */
float _cloudGeometryType[MAX_LAYERS];
float _cloudGeometryXMin[MAX_LAYERS];
float _cloudGeometryXMax[MAX_LAYERS];
float _cloudGeometryYMin[MAX_LAYERS];
float _cloudGeometryYMax[MAX_LAYERS];
float _cloudGeometryZMin[MAX_LAYERS];
float _cloudGeometryZMax[MAX_LAYERS];
float _cloudGeometryHeight[MAX_LAYERS];

CBUFFER_END // Expanse Cloud

/******************************************************************************/
/*************************** END GLOBAL VARIABLES *****************************/
/******************************************************************************/

#endif // EXPANSE_CLOUD_COMMON_INCLUDED
