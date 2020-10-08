#ifndef EXPANSE_SKY_COMMON_INCLUDED
#define EXPANSE_SKY_COMMON_INCLUDED

/******************************************************************************/
/***************************** GLOBAL VARIABLES *******************************/
/******************************************************************************/

CBUFFER_START(ExpanseSky) // Expanse Sky

/* All of these things have to be in a cbuffer, so we can access them across
 * different shaders. */

/* Planet. */
float _atmosphereThickness;
float _planetRadius;
float4 _groundTint;
float _groundEmissionMultiplier;
float4x4 _planetRotation;
bool _groundAlbedoTextureEnabled;
bool _groundEmissionTextureEnabled;
TEXTURECUBE(_groundAlbedoTexture);
TEXTURECUBE(_groundEmissionTexture);

/* Atmosphere layers. */
#define MAX_LAYERS 8
int _numActiveLayers;
float4 _layerCoefficients[MAX_LAYERS];
int _layerDensityDistribution[MAX_LAYERS]; /* todo: may get set wrong since we're setting float? */
float _layerHeight[MAX_LAYERS];
float _layerThickness[MAX_LAYERS];
int _layerPhaseFunction[MAX_LAYERS]; /* todo: may get set wrong since we're setting float? */
float _layerAnisotropy[MAX_LAYERS];
float _layerDensity[MAX_LAYERS];
bool _layerUseDensityAttenuation[MAX_LAYERS];
float _layerAttenuationDistance[MAX_LAYERS];
float _layerAttenuationBias[MAX_LAYERS];
float4 _layerTint[MAX_LAYERS];
float _layerMultipleScatteringMultiplier[MAX_LAYERS];

/* Quality. */
int _numTSamples;
int _numLPSamples;
int _numSSSamples;
int _numGISamples;
int _numMSSamples;
int _numMSAccumulationSamples;
bool _useImportanceSampling;

/* Precomputed sky tables. */

/* Transmittance. */
int2 _resT; /* Table resolution. */
TEXTURE2D(_T);

/* Light Pollution. */
int2 _resLP; /* Table resolution. */
TEXTURE2D(_LP0);
TEXTURE2D(_LP1);
TEXTURE2D(_LP2);
TEXTURE2D(_LP3);
TEXTURE2D(_LP4);
TEXTURE2D(_LP5);
TEXTURE2D(_LP6);
TEXTURE2D(_LP7);

/* Single scattering, with and without shadows. */
int4 _resSS; /* Table resolution. */
TEXTURE3D(_SS0);
TEXTURE3D(_SS1);
TEXTURE3D(_SS2);
TEXTURE3D(_SS3);
TEXTURE3D(_SS4);
TEXTURE3D(_SS5);
TEXTURE3D(_SS6);
TEXTURE3D(_SS7);
TEXTURE3D(_SSNoShadow0);
TEXTURE3D(_SSNoShadow1);
TEXTURE3D(_SSNoShadow2);
TEXTURE3D(_SSNoShadow3);
TEXTURE3D(_SSNoShadow4);
TEXTURE3D(_SSNoShadow5);
TEXTURE3D(_SSNoShadow6);
TEXTURE3D(_SSNoShadow7);

/* Multiple scattering. */
int2 _resMS; /* Table resolution. */
TEXTURE2D(_MS);

/* Multiple scattering accumulation. */
int4 _resMSAcc; /* Table resolution. */
TEXTURE3D(_MSAcc0);
TEXTURE3D(_MSAcc1);
TEXTURE3D(_MSAcc2);
TEXTURE3D(_MSAcc3);
TEXTURE3D(_MSAcc4);
TEXTURE3D(_MSAcc5);
TEXTURE3D(_MSAcc6);
TEXTURE3D(_MSAcc7);

/* Ground Irradiance. */
int _resGI; /* Table resolution. */
TEXTURE2D(_GI0);
TEXTURE2D(_GI1);
TEXTURE2D(_GI2);
TEXTURE2D(_GI3);
TEXTURE2D(_GI4);
TEXTURE2D(_GI5);
TEXTURE2D(_GI6);
TEXTURE2D(_GI7);

CBUFFER_END // Expanse Sky


/* Sampler for tables. */
#ifndef UNITY_SHADER_VARIABLES_INCLUDED
    SAMPLER(s_linear_clamp_sampler);
    SAMPLER(s_trilinear_clamp_sampler);
#endif

/******************************************************************************/
/*************************** END GLOBAL VARIABLES *****************************/
/******************************************************************************/

#endif // EXPANSE_SKY_COMMON_INCLUDED
