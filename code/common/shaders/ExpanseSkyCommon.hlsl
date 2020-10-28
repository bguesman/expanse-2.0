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
float4 _resT; /* Table resolution. */
TEXTURE2D(_T);

/* Light Pollution. */
float4 _resLP; /* Table resolution. */
TEXTURE2D_ARRAY(_LP);

/* Single scattering, with and without shadows. */
float4 _resSS; /* Table resolution. */
TEXTURE2D_ARRAY(_SS);
TEXTURE2D_ARRAY(_SSNoShadow);
TEXTURE2D_ARRAY(_SSAerialPerspectiveLOD0);
TEXTURE2D_ARRAY(_SSAerialPerspectiveLOD1);
float _aerialPerspectiveTableDistanceLOD0;
float _aerialPerspectiveTableDistanceLOD1;
#define AERIAL_PERPSECTIVE_LOD0 0
#define AERIAL_PERPSECTIVE_LOD1 1
#define AERIAL_PERPSECTIVE_LOD2 2

/* Multiple scattering. */
float4 _resMS; /* Table resolution. */
TEXTURE2D(_MS);

/* Multiple scattering accumulation. */
float4 _resMSAcc; /* Table resolution. */
TEXTURE2D_ARRAY(_MSAcc);

/* Ground Irradiance. */
int _resGI; /* Table resolution. */
TEXTURE2D_ARRAY(_GI);

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

/* The following implements the strategy used in physically based sky
 * to lerp between 2 4D texture lookups to solve the issue of uv-mapping for
 * a deep texture. */
 struct deepTexCoord {
   float c00;
   float c10;
   float c01;
   float c11;
   float xa;
   float ya;
 };
struct TexCoord4D {
  deepTexCoord coordXY;
  deepTexCoord coordZW;
};

// TODO: manually lerp and clamp 0.5's instead of using linear clamp sampler.
float3 sampleSSTexture(TexCoord4D uv, int i) {
  // float2 uvw00 = float2(uv.x, uv.z);
  // float2 uvw01 = float2(uv.x, uv.w);
  // float2 uvw10 = float2(uv.y, uv.z);
  // float2 uvw11 = float2(uv.y, uv.w);
  // float3 contrib00 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_linear_clamp_sampler, uvw00, i, 0).xyz;
  // float3 contrib01 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_linear_clamp_sampler, uvw01, i, 0).xyz;
  // float3 contrib10 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_linear_clamp_sampler, uvw10, i, 0).xyz;
  // float3 contrib11 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_linear_clamp_sampler, uvw11, i, 0).xyz;
  // float3 result0 = lerp(contrib00, contrib01, uv.b);
  // float3 result1 = lerp(contrib10, contrib11, uv.b);
  // return lerp(result0, result1, uv.a);

  // Have to compute 16 point samples. Ahhhh!!!!
  float2 uv0000 = float2(uv.coordXY.c00, uv.coordZW.c00);
  float2 uv0001 = float2(uv.coordXY.c00, uv.coordZW.c01);
  float2 uv0010 = float2(uv.coordXY.c00, uv.coordZW.c10);
  float2 uv0011 = float2(uv.coordXY.c00, uv.coordZW.c11);
  float2 uv0100 = float2(uv.coordXY.c01, uv.coordZW.c00);
  float2 uv0101 = float2(uv.coordXY.c01, uv.coordZW.c01);
  float2 uv0110 = float2(uv.coordXY.c01, uv.coordZW.c10);
  float2 uv0111 = float2(uv.coordXY.c01, uv.coordZW.c11);
  float2 uv1000 = float2(uv.coordXY.c10, uv.coordZW.c00);
  float2 uv1001 = float2(uv.coordXY.c10, uv.coordZW.c01);
  float2 uv1010 = float2(uv.coordXY.c10, uv.coordZW.c10);
  float2 uv1011 = float2(uv.coordXY.c10, uv.coordZW.c11);
  float2 uv1100 = float2(uv.coordXY.c11, uv.coordZW.c00);
  float2 uv1101 = float2(uv.coordXY.c11, uv.coordZW.c01);
  float2 uv1110 = float2(uv.coordXY.c11, uv.coordZW.c10);
  float2 uv1111 = float2(uv.coordXY.c11, uv.coordZW.c11);

  float3 c0000 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0000, i, 0).xyz;
  float3 c0001 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0001, i, 0).xyz;
  float3 c0010 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0010, i, 0).xyz;
  float3 c0011 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0011, i, 0).xyz;
  float3 c0100 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0100, i, 0).xyz;
  float3 c0101 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0101, i, 0).xyz;
  float3 c0110 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0110, i, 0).xyz;
  float3 c0111 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv0111, i, 0).xyz;
  float3 c1000 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1000, i, 0).xyz;
  float3 c1001 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1001, i, 0).xyz;
  float3 c1010 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1010, i, 0).xyz;
  float3 c1011 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1011, i, 0).xyz;
  float3 c1100 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1100, i, 0).xyz;
  float3 c1101 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1101, i, 0).xyz;
  float3 c1110 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1110, i, 0).xyz;
  float3 c1111 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SS, s_point_clamp_sampler, uv1111, i, 0).xyz;

  // Now, have to lerp between them all.
  // Lerp w.
  float3 c000 = lerp(c0000, c0001, uv.coordZW.ya);
  float3 c001 = lerp(c0010, c0011, uv.coordZW.ya);
  float3 c010 = lerp(c0100, c0101, uv.coordZW.ya);
  float3 c011 = lerp(c0110, c0111, uv.coordZW.ya);
  float3 c100 = lerp(c1000, c1001, uv.coordZW.ya);
  float3 c101 = lerp(c1010, c1011, uv.coordZW.ya);
  float3 c110 = lerp(c1100, c1101, uv.coordZW.ya);
  float3 c111 = lerp(c1110, c1111, uv.coordZW.ya);

  // Lerp z.
  float3 c00 = lerp(c000, c001, uv.coordZW.xa);
  float3 c01 = lerp(c010, c011, uv.coordZW.xa);
  float3 c10 = lerp(c100, c101, uv.coordZW.xa);
  float3 c11 = lerp(c110, c111, uv.coordZW.xa);

  // Lerp y.
  float3 c0 = lerp(c00, c01, uv.coordXY.ya);
  float3 c1 = lerp(c10, c11, uv.coordXY.ya);

  // Lerp x.
  return lerp(c0, c1, uv.coordXY.xa);
}

float3 sampleAerialPerspectiveLOD0Texture(TexCoord4D uv, int i) {
  // float2 uvw00 = float2(uv.x, uv.z);
  // float2 uvw01 = float2(uv.x, uv.w);
  // float2 uvw10 = float2(uv.y, uv.z);
  // float2 uvw11 = float2(uv.y, uv.w);
  // float3 contrib00 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD0, s_linear_clamp_sampler, uvw00, i, 0).xyz;
  // float3 contrib01 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD0, s_linear_clamp_sampler, uvw01, i, 0).xyz;
  // float3 contrib10 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD0, s_linear_clamp_sampler, uvw10, i, 0).xyz;
  // float3 contrib11 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD0, s_linear_clamp_sampler, uvw11, i, 0).xyz;
  // float3 result0 = lerp(contrib00, contrib01, uv.b);
  // float3 result1 = lerp(contrib10, contrib11, uv.b);
  // return lerp(result0, result1, uv.a);
  return float3(0, 0, 0);
}

float3 sampleAerialPerspectiveLOD1Texture(TexCoord4D uv, int i) {
  // float2 uvw00 = float2(uv.x, uv.z);
  // float2 uvw01 = float2(uv.x, uv.w);
  // float2 uvw10 = float2(uv.y, uv.z);
  // float2 uvw11 = float2(uv.y, uv.w);
  // float3 contrib00 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD1, s_linear_clamp_sampler, uvw00, i, 0).xyz;
  // float3 contrib01 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD1, s_linear_clamp_sampler, uvw01, i, 0).xyz;
  // float3 contrib10 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD1, s_linear_clamp_sampler, uvw10, i, 0).xyz;
  // float3 contrib11 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSAerialPerspectiveLOD1, s_linear_clamp_sampler, uvw11, i, 0).xyz;
  // float3 result0 = lerp(contrib00, contrib01, uv.b);
  // float3 result1 = lerp(contrib10, contrib11, uv.b);
  // return lerp(result0, result1, uv.a);
  return float3(0, 0, 0);
}

float3 sampleAerialPerspectiveTexture(TexCoord4D uv, int i, int LOD) {
  // switch (LOD) {
  //   case AERIAL_PERPSECTIVE_LOD0: {
  //     return sampleAerialPerspectiveLOD0Texture(uv, i);
  //   }
  //   case AERIAL_PERPSECTIVE_LOD1: {
  //     return sampleAerialPerspectiveLOD1Texture(uv, i);
  //   }
  //   case AERIAL_PERPSECTIVE_LOD2:
  //     return sampleSSTexture(uv, i);
  //   default:
  //     return float3(0, 0, 0);
  // }
  return float3(0, 0, 0);
}

float3 sampleSSNoShadowTexture(TexCoord4D uv, int i) {
  // float2 uvw00 = float2(uv.x, uv.z);
  // float2 uvw01 = float2(uv.x, uv.w);
  // float2 uvw10 = float2(uv.y, uv.z);
  // float2 uvw11 = float2(uv.y, uv.w);
  // float3 contrib00 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSNoShadow, s_linear_clamp_sampler, uvw00, i, 0).xyz;
  // float3 contrib01 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSNoShadow, s_linear_clamp_sampler, uvw01, i, 0).xyz;
  // float3 contrib10 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSNoShadow, s_linear_clamp_sampler, uvw10, i, 0).xyz;
  // float3 contrib11 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SSNoShadow, s_linear_clamp_sampler, uvw11, i, 0).xyz;
  // float3 result0 = lerp(contrib00, contrib01, uv.b);
  // float3 result1 = lerp(contrib10, contrib11, uv.b);
  // return lerp(result0, result1, uv.a);
  return float3(0, 0, 0);
}

float3 sampleMSAccTexture(TexCoord4D uv, int i) {
  // float2 uvw00 = float2(uv.x, uv.z);
  // float2 uvw01 = float2(uv.x, uv.w);
  // float2 uvw10 = float2(uv.y, uv.z);
  // float2 uvw11 = float2(uv.y, uv.w);
  // float3 contrib00 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MSAcc, s_linear_clamp_sampler, uvw00, i, 0).xyz;
  // float3 contrib01 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MSAcc, s_linear_clamp_sampler, uvw01, i, 0).xyz;
  // float3 contrib10 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MSAcc, s_linear_clamp_sampler, uvw10, i, 0).xyz;
  // float3 contrib11 = SAMPLE_TEXTURE2D_ARRAY_LOD(_MSAcc, s_linear_clamp_sampler, uvw11, i, 0).xyz;
  // float3 result0 = lerp(contrib00, contrib01, uv.b);
  // float3 result1 = lerp(contrib10, contrib11, uv.b);
  // return lerp(result0, result1, uv.a);
  return float3(0, 0, 0);
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
