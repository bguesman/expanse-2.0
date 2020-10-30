#ifndef EXPANSE_SKY_COMMON_INCLUDED
#define EXPANSE_SKY_COMMON_INCLUDED

/******************************************************************************/
/***************************** GLOBAL VARIABLES *******************************/
/******************************************************************************/

CBUFFER_START(ExpanseSky) // Expanse Sky

/* All of these things have to be in a cbuffer, so we can access them across
 * different shaders. */

/* Planet. */
float _atmosphereRadius;
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
float4 _layerCoefficientsA[MAX_LAYERS]; /* Absorption */
float4 _layerCoefficientsS[MAX_LAYERS]; /* Scattering */
float _layerDensityDistribution[MAX_LAYERS];
float _layerHeight[MAX_LAYERS];
float _layerThickness[MAX_LAYERS];
float _layerPhaseFunction[MAX_LAYERS];
float _layerAnisotropy[MAX_LAYERS];
float _layerDensity[MAX_LAYERS];
bool _layerUseDensityAttenuation[MAX_LAYERS];
float _layerAttenuationDistance[MAX_LAYERS];
float _layerAttenuationBias[MAX_LAYERS];
float4 _layerTint[MAX_LAYERS];
float _layerMultipleScatteringMultiplier[MAX_LAYERS];

/* Celestial Bodies. */
#define MAX_BODIES 8
int _numActiveBodies;
float4 _bodyLightColor[MAX_BODIES];
float3 _bodyDirection[MAX_BODIES];

/* Quality. */
int _numTSamples;
int _numLPSamples;
int _numSSSamples;
int _numGISamples;
int _numMSSamples;
int _numMSAccumulationSamples;
bool _useImportanceSampling;
bool _useAntiAliasing;
float _ditherAmount;

float3 _WorldSpaceCameraPos1;
float4 _currentScreenSize;
float4x4 _ViewMatrix1;
float4x4 _pCoordToViewDir;
float _farClip;
#undef UNITY_MATRIX_V
#define UNITY_MATRIX_V _ViewMatrix1

/* Precomputed sky tables. */

/* Transmittance. */
float4 _resT; /* Table resolution. */
TEXTURE2D(_T);

/* Light Pollution. */
float4 _resLP; /* Table resolution. */
TEXTURE2D(_LP);

/* Single scattering, with and without shadows. */
float4 _resSS; /* Table resolution. */
TEXTURE2D(_SS);

/* Multiple scattering. */
float4 _resMS; /* Table resolution. */
TEXTURE2D(_MS);

/* Multiple scattering accumulation. */
float4 _resMSAcc; /* Table resolution. */
TEXTURE2D(_MSAcc);

/* Aerial perspective. */
float4 _resAP; /* Table resolution. */
TEXTURE3D(_AP);

/* Ground Irradiance. */
int _resGI; /* Table resolution. */
TEXTURE2D(_GI);

CBUFFER_END // Expanse Sky

#ifndef GOLDEN_RATIO
#define GOLDEN_RATIO 1.6180339887498948482
#endif

/* Sampler for tables. */
#ifndef UNITY_SHADER_VARIABLES_INCLUDED
    SAMPLER(s_linear_clamp_sampler);
    SAMPLER(s_trilinear_clamp_sampler);
    SAMPLER(s_point_clamp_sampler);
#endif

/* Time tick variable for random seeding. */
float _tick;

/******************************************************************************/
/*************************** END GLOBAL VARIABLES *****************************/
/******************************************************************************/



/******************************************************************************/
/***************************** UTILITY FUNCTIONS ******************************/
/******************************************************************************/

#define FLT_EPSILON 0.000001

float clampCosine(float c) {
  return clamp(c, -1.0, 1.0);
}

float safeSqrt(float s) {
  return sqrt(max(0.0, s));
}

/* Returns minimum non-negative number, given that one number is
 * non-negative. If both numbers are negative, returns a negative number. */
float minNonNegative(float a, float b) {
  return (a < 0.0) ? b : ((b < 0.0) ? a : min(a, b));
}

/* True if a is greater than b within tolerance FLT_EPSILON, false
 * otherwise. */
bool floatGT(float a, float b) {
  return a > b - FLT_EPSILON;
}
bool floatGT(float a, float b, float eps) {
  return a > b - eps;
}

/* True if a is less than b within tolerance FLT_EPSILON, false
 * otherwise. */
bool floatLT(float a, float b) {
  return a < b + FLT_EPSILON;
}
bool floatLT(float a, float b, float eps) {
  return a < b + eps;
}

/******************************************************************************/
/*************************** END UTILITY FUNCTIONS ****************************/
/******************************************************************************/



/******************************************************************************/
/****************************** GEOMETRY TESTS ********************************/
/******************************************************************************/

/* Returns t values of ray intersection with sphere. Third value indicates
 * if there was an intersection at all; if negative, there was no
 * intersection. */
float3 intersectSphere(float3 p, float3 d, float r) {
  float A = dot(d, d);
  float B = 2.f * dot(d, p);
  float C = dot(p, p) - (r * r);
  float det = (B * B) - 4.f * A * C;
  if (floatGT(det, 0.0)) {
    det = safeSqrt(det);
    return float3((-B + det) / (2.f * A), (-B - det) / (2.f * A), 1.0);
  }
  return float3(0, 0, -1.0);
}

/* Struct containing data for ray intersection queries. */
struct SkyIntersectionData {
  float startT, endT;
  bool groundHit, atmoHit;
};


/* Traces a ray starting at point O in direction d. Returns information
 * about where the ray hit on the ground/on the boundary of the atmosphere. */
SkyIntersectionData traceSkyVolume(float3 O, float3 d, float planetRadius,
  float atmosphereRadius) {
  /* Perform raw sphere intersections. */
  float3 t_ground = intersectSphere(O, d, planetRadius);
  float3 t_atmo = intersectSphere(O, d, atmosphereRadius);

  SkyIntersectionData toRet = {0, 0, false, false};

  /* We have a hit if the intersection was succesful and if either point
   * is greater than zero (meaning we are in front of the ray, and not
   * behind it). */
  toRet.groundHit = t_ground.z >= 0.0 && (t_ground.x >= 0.0 || t_ground.y >= 0.0);
  toRet.atmoHit = t_atmo.z >= 0.0 && (t_atmo.x >= 0.0 || t_atmo.y >= 0.0);

  if (floatLT(length(O), atmosphereRadius)) {
    /* We are below the atmosphere boundary, and we will start our raymarch
     * at the origin point. */
    toRet.startT = 0;
    if (toRet.groundHit) {
      /* We have hit the ground, and will end our raymarch at the first
       * positive ground hit. */
      toRet.endT = minNonNegative(t_ground.x, t_ground.y);
    } else {
      /* We will end our raymarch at the first positive atmosphere hit. */
      toRet.endT = minNonNegative(t_atmo.x, t_atmo.y);
    }
  } else {
    /* We are outside the atmosphere, and, if we intersect the atmosphere
     * at all, we will start our raymarch at the first atmosphere
     * intersection point. We don't need to be concerned about negative
     * t values, since it's a geometric impossibility to be outside a sphere
     * and intersect both in front of and behind a ray. */
    if (toRet.atmoHit) {
      toRet.startT = min(t_atmo.x, t_atmo.y);
      if (toRet.groundHit) {
        /* If we hit the ground at all, we'll end our ray at the first ground
         * intersection point. */
        toRet.endT = min(t_ground.x, t_ground.y);
      } else {
        /* Otherwise, we'll end our ray at the second atmosphere
         * intersection point. */
        toRet.endT = max(t_atmo.x, t_atmo.y);
      }
    }
    /* If we haven't hit the atmosphere, we leave everything uninitialized,
     * since this ray just goes out into space. */
  }

  return toRet;
}

/******************************************************************************/
/**************************** END GEOMETRY TESTS ******************************/
/******************************************************************************/



/******************************************************************************/
/***************************** COORDINATE SYSTEM ******************************/
/******************************************************************************/

float3 GetCameraPositionPlanetSpace() {
  return _WorldSpaceCameraPos1 - float3(0, -_planetRadius, 0);
}

/******************************************************************************/
/*************************** END COORDINATE SYSTEM ****************************/
/******************************************************************************/


/******************************************************************************/
/********************************* SAMPLING ***********************************/
/******************************************************************************/

/* Given an index and total number of points, generates corresponding
 * point on fibonacci hemi-sphere. */
float3 fibonacciHemisphere(int i, int n) {
  float i_mid = i + 0.5;
  float cos_phi = 1 - i/float(n);
  float sin_phi = safeSqrt(1 - cos_phi * cos_phi);
  float theta = 2 * PI * i / GOLDEN_RATIO;
  float cos_theta = cos(theta);
  float sin_theta = safeSqrt(1 - cos_theta * cos_theta);
  return float3(cos_theta * sin_phi, cos_phi, sin_theta * sin_phi);
}

/* Given an index and total number of points, generates corresponding
 * point on fibonacci sphere. */
float3 fibonacciSphere(int i, int n) {
  float i_mid = i + 0.5;
  float cos_phi = 1 - 2 * i/float(n);
  float sin_phi = safeSqrt(1 - cos_phi * cos_phi);
  float theta = 2 * PI * i / GOLDEN_RATIO;
  float cos_theta = cos(theta);
  float sin_theta = safeSqrt(1 - cos_theta * cos_theta);
  return float3(cos_theta * sin_phi, cos_phi, sin_theta * sin_phi);
}

/* Generates linear location from a sample index.
 * Returns (sample, ds). */
float2 generateLinearSampleFromIndex(int i, int numberOfSamples) {
  return float2((float(i) + 0.5) / float(numberOfSamples),
    1.0 / ((float) numberOfSamples));
}

/* Generates cubed "importance sample" location from a sample index.
 * Returns (sample, ds). */
float2 generateCubicSampleFromIndex(int i, int numberOfSamples) {
  float t_left = float(i) / float(numberOfSamples);
  float t_middle = (float(i) + 0.5) / float(numberOfSamples);
  float t_right = (float(i) + 1.0) / float(numberOfSamples);
  t_left *= t_left * t_left;
  t_middle *= t_middle * t_middle;
  t_right *= t_right * t_right;
  return float2(t_middle, t_right - t_left);
}

/******************************************************************************/
/******************************* END SAMPLING *********************************/
/******************************************************************************/



/******************************************************************************/
/************************** DENSITY DISTRIBUTIONS *****************************/
/******************************************************************************/

/* Computes density at a point for exponentially distributed atmosphere.
 * Assumes the planet is centered at the origin. */
float computeDensityExponential(float3 p, float thickness, float density) {
  return density * exp((_planetRadius - length(p))/thickness);
}

/* Computes density at a point for tent distributed atmosphere.
 * Assumes the planet is centered at the origin. */
float computeDensityTent(float3 p, float height, float thickness, float density) {
  return density * max(0.0,
    1.0 - abs(length(p) - _planetRadius - height) / (0.5 * thickness));
}

float computeDensity(int densityDistribution, float3 p, float height,
  float thickness, float density, bool useAtten, float dist, float attenBias, float attenDistance) {
  float atten = 1.0;
  if (useAtten) {
    atten = saturate(exp(-(dist - attenBias)/attenDistance));
  }
  switch (densityDistribution) {
    case 0:
      return atten * computeDensityExponential(p, thickness, density);
    case 1:
      return atten * computeDensityTent(p, height, thickness, density);
    default:
      return 0;
  }
}

float computeOpticalDepth(int densityDistribution, float3 O, float3 endPoint,
  float height, float thickness, float density, float attenBias,
  float attenDistance, bool useAtten, int samples) {
  /* Only use importance sampling if we use exponential distribution. */
  bool importanceSample = _useImportanceSampling && (densityDistribution == 0);
  // Evaluate integral over curved planet with a midpoint integrator.
  float3 d = endPoint - O;
  float length_d = length(d);
  float acc = 0.0;
  for (int i = 0; i < samples; i++) {
    /* Compute where along the ray we're going to sample. */
    float2 t_ds = importanceSample ?
       (generateCubicSampleFromIndex(i, samples)) :
       (generateLinearSampleFromIndex(i, samples));

    /* Compute the point we're going to sample at. */
    float3 pt = O + (d * t_ds.x);

    /* Accumulate the density at that point. */
    acc += computeDensity(densityDistribution, pt, height, thickness,
      density, useAtten, length_d * t_ds.x, attenBias, attenDistance)
      * t_ds.y * length_d;
  }
  return acc;
}

/******************************************************************************/
/************************** DENSITY DISTRIBUTIONS *****************************/
/******************************************************************************/



/******************************************************************************/
/***************************** PHASE FUNCTIONS ********************************/
/******************************************************************************/

float isotropicPhase() {
  return 0.25 / PI;
}

float rayleighPhase(float dot_L_d) {
  return 3.0 / (16.0 * PI) * (1.0 + dot_L_d * dot_L_d);
}

float miePhase(float dot_L_d, float g) {
  return 3.0 / (8.0 * PI) * ((1.0 - g * g) * (1.0 + dot_L_d * dot_L_d))
    / ((2.0 + g * g) * pow(abs(1.0 + g * g - 2.0 * g * dot_L_d), 1.5));
}

float computePhase(float dot_L_d, float anisotropy, int type) {
  switch (type) {
    case 0: /* Isotropic. */
      return isotropicPhase();
    case 1: /* Rayleigh. */
      return rayleighPhase(dot_L_d);
    case 2: /* Mie. */
      return miePhase(dot_L_d, anisotropy);
    default:
      return isotropicPhase();
  }
}

/******************************************************************************/
/*************************** END PHASE FUNCTIONS ******************************/
/******************************************************************************/



/******************************************************************************/
/***************************** TEXTURE SAMPLERS *******************************/
/******************************************************************************/

/* Given uv coodinate representing direction, computes sky transmittance. */
float3 computeSkyTransmittance(float2 uv) {
  return exp(SAMPLE_TEXTURE2D_LOD(_T, s_point_clamp_sampler, uv, 0).xyz);
}

/* Given uv coodinate representing direction, computes sky transmittance. */
float3 computeSkyTransmittanceRaw(float2 uv) {
  return SAMPLE_TEXTURE2D_LOD(_T, s_linear_clamp_sampler, uv, 0).xyz;
}

float3 sampleSSTexture(float2 uv) {
  return SAMPLE_TEXTURE2D_LOD(_SS, s_linear_clamp_sampler, uv, 0).xyz;
}

float3 sampleMSAccTexture(float2 uv) {
  return SAMPLE_TEXTURE2D_LOD(_MSAcc, s_linear_clamp_sampler, uv, 0).xyz;
}

float4 sampleAPTexture(float3 uv) {
  return SAMPLE_TEXTURE3D_LOD(_AP, s_linear_clamp_sampler, uv, 0);
}

float3 sampleGITexture(float2 uv, int i) {
  return SAMPLE_TEXTURE2D_ARRAY_LOD(_GI, s_linear_clamp_sampler, uv, i, 0).xyz;
}

float3 sampleLPTexture(float2 uv, int i) {
  return SAMPLE_TEXTURE2D_ARRAY_LOD(_LP, s_linear_clamp_sampler, uv, i, 0).xyz;
}

/******************************************************************************/
/*************************** END TEXTURE SAMPLERS *****************************/
/******************************************************************************/

#endif // EXPANSE_SKY_COMMON_INCLUDED
